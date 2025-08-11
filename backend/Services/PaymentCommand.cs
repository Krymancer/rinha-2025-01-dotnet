using Backend.Configuration;
using Backend.Models;

namespace Backend.Services;

public class PaymentCommand
{
  private readonly PaymentProcessorRouter _paymentRouter;
  private readonly DatabaseClient _databaseClient;
  private readonly QueueService<PaymentRequest> _queue;
  private readonly SemaphoreSlim _processingMutex;
  private readonly int _batchSize;

  public PaymentCommand(
      QueueService<PaymentRequest> queue,
      PaymentProcessorRouter paymentRouter,
      DatabaseClient databaseClient,
      AppConfig config)
  {
    _queue = queue;
    _paymentRouter = paymentRouter;
    _databaseClient = databaseClient;
    _batchSize = config.PaymentWorker.BatchSize;
    _processingMutex = new SemaphoreSlim(1, 1);
  }

  public async Task<List<ProcessedPayment>> ProcessPaymentBatchAsync(IReadOnlyList<PaymentRequest> payments)
  {
    var requestedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    var tasks = payments.Select(async payment =>
    {
      var result = await _paymentRouter.ProcessPaymentWithRetryAsync(payment, requestedAt);
      return new ProcessedPayment(
              payment.CorrelationId,
              payment.Amount,
              result.Processor,
              requestedAt
          );
    });

    var processedPayments = await Task.WhenAll(tasks);

    await _databaseClient.PersistPaymentsBatchAsync(processedPayments);

    return processedPayments.ToList();
  }

  public async Task ProcessPaymentsAsync()
  {
    if (!await _processingMutex.WaitAsync(0)) return;

    try
    {
      var remaining = _queue.Count;
      while (remaining > 0)
      {
        var batchSize = Math.Min(_batchSize, remaining);
        var batch = _queue.DequeueMultiple(batchSize);

        if (batch.Count == 0) break;

        await ProcessPaymentBatchAsync(batch);
        remaining -= batch.Count;
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[payment-command] processing error: {ex.Message}");
    }
    finally
    {
      _processingMutex.Release();
    }
  }

  public void Enqueue(PaymentRequest input)
  {
    _queue.Enqueue(input);

    // Fire and forget processing
    _ = Task.Run(ProcessPaymentsAsync);
  }

  public async Task PurgeAllAsync()
  {
    await _databaseClient.PurgeDatabaseAsync();
    Console.WriteLine("Complete purge successful");
  }

  public void Dispose()
  {
    _processingMutex.Dispose();
    _paymentRouter.Dispose();
    _databaseClient.Dispose();
  }
}
