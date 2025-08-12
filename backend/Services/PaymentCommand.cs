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

    // Process payments concurrently but limit concurrency to avoid overwhelming the payment processors
    var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
    var tasks = payments.Select(async payment =>
    {
      await semaphore.WaitAsync();
      try
      {
        var result = await _paymentRouter.ProcessPaymentWithRetryAsync(payment, requestedAt);
        return new ProcessedPayment(
                payment.CorrelationId,
                payment.Amount,
                result.Processor,
                requestedAt
            );
      }
      finally
      {
        semaphore.Release();
      }
    });

    var processedPayments = await Task.WhenAll(tasks);

    // CRITICAL: Ensure database persistence completes successfully
    await _databaseClient.PersistPaymentsBatchAsync(processedPayments);

    return processedPayments.ToList();
  }

  public async Task ProcessPaymentsAsync()
  {
    if (!await _processingMutex.WaitAsync(0))
      return;

    try
    {
      while (_queue.Count > 0)
      {
        var batchSize = Math.Min(_batchSize, _queue.Count);
        var batch = _queue.DequeueMultiple(batchSize);

        if (batch.Count == 0) break;

        await ProcessPaymentBatchAsync(batch);
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

    // Start processing if not already running
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
