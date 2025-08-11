using System.Text.Json;
using Backend.Configuration;
using Backend.Models;

namespace Backend.Services;

public class PaymentProcessorRouter
{
  private readonly Dictionary<ProcessorType, PaymentProcessor> _processors;
  private readonly PaymentProcessor _optimalProcessor;
  private readonly HttpClient _httpClient;
  private readonly int _requestTimeoutMs;
  private int _delayMs = 75;

  public PaymentProcessorRouter(AppConfig config)
  {
    _requestTimeoutMs = config.PaymentRouter.RequestTimeoutMs;

    _processors = new Dictionary<ProcessorType, PaymentProcessor>
    {
      [ProcessorType.Default] = new PaymentProcessor(config.PaymentProcessors.Default.Url, ProcessorType.Default),
      [ProcessorType.Fallback] = new PaymentProcessor(config.PaymentProcessors.Fallback.Url, ProcessorType.Fallback)
    };

    _optimalProcessor = _processors[ProcessorType.Default];
    _httpClient = new HttpClient();
  }

  private async Task<ProcessedPayment> MakePaymentRequestAsync(
      PaymentRequest payment,
      string requestedAt,
      PaymentProcessor? processor = null)
  {
    var currentProcessor = processor ?? _optimalProcessor;
    var paymentData = new PaymentProcessorRequest(
        payment.CorrelationId,
        payment.Amount,
        requestedAt
    );

    using var cts = new CancellationTokenSource(_requestTimeoutMs);

    var json = JsonSerializer.Serialize(paymentData, AppJsonContext.Default.PaymentProcessorRequest);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    var response = await _httpClient.PostAsync($"{currentProcessor.Url}/payments", content, cts.Token);
    response.EnsureSuccessStatusCode();

    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
    var data = JsonSerializer.Deserialize<PaymentProcessorRequest>(responseContent, AppJsonContext.Default.PaymentProcessorRequest)
        ?? throw new InvalidOperationException("Failed to deserialize payment processor response");

    return new ProcessedPayment(
        data.CorrelationId,
        data.Amount,
        currentProcessor.Type,
        data.RequestedAt
    );
  }

  public async Task<ProcessedPayment> ProcessPaymentWithRetryAsync(
      PaymentRequest payment,
      string requestedAt,
      PaymentProcessor? processor = null)
  {
    var currentProcessor = processor ?? _processors[ProcessorType.Default];

    try
    {
      var response = await MakePaymentRequestAsync(payment, requestedAt, currentProcessor);
      return response;
    }
    catch (Exception)
    {
      // Retry with delay using the same processor (as per original code)
      await Task.Delay(_delayMs);
      Interlocked.Add(ref _delayMs, 75);

      var alternativeProcessor = _processors[ProcessorType.Default];
      return await ProcessPaymentWithRetryAsync(payment, requestedAt, alternativeProcessor);
    }
  }

  public void Dispose()
  {
    _httpClient.Dispose();
  }
}
