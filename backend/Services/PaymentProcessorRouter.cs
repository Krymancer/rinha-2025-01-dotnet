using System.Text.Json;
using Backend.Configuration;
using Backend.Models;

namespace Backend.Services;

public class PaymentProcessorRouter : IDisposable
{
  private readonly Dictionary<ProcessorType, PaymentProcessor> _processors;
  private readonly HealthCheckService _healthCheckService;
  private readonly HttpClient _httpClient;
  private readonly int _requestTimeoutMs;
  private int _delayMs = 75;

  public PaymentProcessorRouter(AppConfig config, HealthCheckService healthCheckService)
  {
    _requestTimeoutMs = config.PaymentRouter.RequestTimeoutMs;
    _healthCheckService = healthCheckService;

    _processors = new Dictionary<ProcessorType, PaymentProcessor>
    {
      [ProcessorType.Default] = new PaymentProcessor(config.PaymentProcessors.Default.Url, ProcessorType.Default),
      [ProcessorType.Fallback] = new PaymentProcessor(config.PaymentProcessors.Fallback.Url, ProcessorType.Fallback)
    };

    _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_requestTimeoutMs + 500) };
  }

  private async Task<ProcessedPayment> MakePaymentRequestAsync(
      PaymentRequest payment,
      string requestedAt,
      PaymentProcessor processor)
  {
    var paymentData = new PaymentProcessorRequest(
        payment.CorrelationId,
        payment.Amount,
        requestedAt
    );

    using var cts = new CancellationTokenSource(_requestTimeoutMs);

    var json = JsonSerializer.Serialize(paymentData, AppJsonContext.Default.PaymentProcessorRequest);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    Console.WriteLine($"Sending to {processor.Url}/payments: {json}");

    var response = await _httpClient.PostAsync($"{processor.Url}/payments", content, cts.Token);

    if (!response.IsSuccessStatusCode)
    {
      var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
      Console.WriteLine($"Payment processor error ({response.StatusCode}): {errorContent}");
    }

    response.EnsureSuccessStatusCode();

    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
    var data = JsonSerializer.Deserialize<PaymentProcessorRequest>(responseContent, AppJsonContext.Default.PaymentProcessorRequest)
        ?? throw new InvalidOperationException("Failed to deserialize payment processor response");

    return new ProcessedPayment(
        data.CorrelationId,
        data.Amount,
        processor.Type,
        data.RequestedAt
    );
  }

  public async Task<ProcessedPayment> ProcessPaymentWithRetryAsync(
      PaymentRequest payment,
      string requestedAt,
      ProcessorType? preferredProcessor = null)
  {
    // CRITICAL FIX: Always try Default first, only fallback if it actually fails
    var processorType = preferredProcessor ?? ProcessorType.Default;
    var processor = _processors[processorType];
    var retryCount = 0;
    var maxRetries = 3;

    while (retryCount < maxRetries)
    {
      try
      {
        var response = await MakePaymentRequestAsync(payment, requestedAt, processor);
        Console.WriteLine($"Payment processed successfully via {processorType}");
        return response;
      }
      catch (Exception ex)
      {
        retryCount++;
        Console.WriteLine($"Payment failed via {processorType}: {ex.Message}");

        // Only try fallback if we've exhausted retries on default AND fallback is available
        if (retryCount >= maxRetries && processorType == ProcessorType.Default)
        {
          var fallbackHealth = await _healthCheckService.GetHealthAsync(ProcessorType.Fallback);

          if (!fallbackHealth.Failing)
          {
            try
            {
              Console.WriteLine($"Trying fallback processor: Fallback");
              processor = _processors[ProcessorType.Fallback];
              processorType = ProcessorType.Fallback;
              var fallbackResponse = await MakePaymentRequestAsync(payment, requestedAt, processor);
              Console.WriteLine($"Fallback processor Fallback succeeded");
              return fallbackResponse;
            }
            catch (Exception fallbackEx)
            {
              Console.WriteLine($"Fallback processor Fallback also failed: {fallbackEx.Message}");
              // Continue with default retry logic
            }
          }
        }

        if (retryCount < maxRetries)
        {
          // Exponential backoff but faster recovery
          var delay = Math.Min(50 * (int)Math.Pow(2, retryCount - 1), 500);
          Console.WriteLine($"Retrying with {processorType} after delay");
          await Task.Delay(delay);
        }
        else
        {
          throw; // Re-throw the last exception after all retries
        }
      }
    }

    throw new InvalidOperationException("Payment processing failed after all retries");
  }

  public void Dispose()
  {
    _httpClient.Dispose();
    _healthCheckService.Dispose();
  }
}
