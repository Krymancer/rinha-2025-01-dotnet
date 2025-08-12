using System.Text.Json;
using Backend.Configuration;
using Backend.Models;

namespace Backend.Services;

public class DatabaseClient
{
  private readonly string _socketPath;
  private readonly QueueService<BatchItem> _batchQueue;
  private readonly Timer? _batchTimer;
  private readonly int _batchSize;
  private readonly int _batchTimeout;
  private readonly HttpClient _httpClient;
  private readonly object _flushLock = new();
  private volatile bool _isFlushingBatch = false;

  public DatabaseClient(AppConfig config)
  {
    _socketPath = config.DatabaseSocketPath;
    _batchSize = config.DatabaseClient.BatchSize;
    _batchTimeout = config.DatabaseClient.BatchTimeoutMs;
    _batchQueue = new QueueService<BatchItem>();

    _httpClient = new HttpClient(new SocketsHttpHandler
    {
      ConnectCallback = async (context, cancellationToken) =>
      {
        var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(_socketPath), cancellationToken);
        return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
      }
    });

    _batchTimer = new Timer(async _ => await FlushBatchAsync(), null, Timeout.Infinite, Timeout.Infinite);
  }

  private async Task<PaymentSummary?> SendHttpRequestForSummaryAsync(string path)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost{path}");

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var responseContent = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize(responseContent, AppJsonContext.Default.PaymentSummary);
  }

  private async Task SendHttpRequestAsync(string path, HttpMethod method, ProcessedPayment[]? body = null)
  {
    var request = new HttpRequestMessage(method, $"http://localhost{path}");

    if (body != null)
    {
      var json = JsonSerializer.Serialize(body, AppJsonContext.Default.ProcessedPaymentArray);
      request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }

  private async Task SendDatabasePaymentsAsync(string path, HttpMethod method, DatabaseProcessedPayment[]? body = null)
  {
    var request = new HttpRequestMessage(method, $"http://localhost{path}");

    if (body != null)
    {
      var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DatabaseProcessedPaymentArray);
      request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }

  private async Task FlushBatchAsync()
  {
    if (_isFlushingBatch || _batchQueue.IsEmpty) return;

    lock (_flushLock)
    {
      if (_isFlushingBatch) return;
      _isFlushingBatch = true;
    }

    try
    {
      var currentBatch = new List<BatchItem>();

      while (!_batchQueue.IsEmpty)
      {
        if (_batchQueue.TryDequeue(out var item) && item != null)
        {
          currentBatch.Add(item);
        }
      }

      if (currentBatch.Count > 0)
      {
        var allPayments = currentBatch.SelectMany(item => item.Payments).ToArray();

        if (allPayments.Length > 0)
        {
          // Convert backend ProcessedPayment (with enum) to database format (with string)
          var dbPayments = allPayments.Select(p => new DatabaseProcessedPayment(
            p.CorrelationId,
            p.Amount,
            p.Processor.ToString().ToLowerInvariant(), // Convert enum to lowercase string
            p.RequestedAt
          )).ToArray();

          Console.WriteLine($"Sending {dbPayments.Length} payments to database");
          Console.WriteLine($"Database payload: {JsonSerializer.Serialize(dbPayments, AppJsonContext.Default.DatabaseProcessedPaymentArray)}");

          try
          {
            await SendDatabasePaymentsAsync("/payments/batch", HttpMethod.Post, dbPayments);
            Console.WriteLine("Database request successful");
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Database request failed: {ex.Message}");
            throw;
          }
        }

        foreach (var item in currentBatch)
        {
          item.TaskCompletionSource.SetResult();
        }
      }
    }
    catch (Exception error)
    {
      // Handle batch errors by completing all tasks with exception
      while (_batchQueue.TryDequeue(out var item) && item != null)
      {
        item.TaskCompletionSource.SetException(error);
      }
    }
    finally
    {
      _isFlushingBatch = false;
    }
  }

  private void ScheduleBatchFlush()
  {
    _batchTimer?.Change(_batchTimeout, Timeout.Infinite);
  }

  public async Task PersistPaymentsBatchAsync(IReadOnlyList<ProcessedPayment> payments)
  {
    if (payments.Count == 0) return;

    var taskCompletionSource = new TaskCompletionSource();
    var batchItem = new BatchItem(payments, taskCompletionSource);

    _batchQueue.Enqueue(batchItem);

    if (_batchQueue.Count >= _batchSize)
    {
      await FlushBatchAsync();
    }
    else
    {
      ScheduleBatchFlush();
    }

    await taskCompletionSource.Task;
  }

  public async Task<PaymentSummary> GetDatabaseSummaryAsync(string? from = null, string? to = null)
  {
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(from)) queryParams.Add($"from={Uri.EscapeDataString(from)}");
    if (!string.IsNullOrEmpty(to)) queryParams.Add($"to={Uri.EscapeDataString(to)}");

    var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
    return await SendHttpRequestForSummaryAsync($"/payments-summary{query}") ?? new PaymentSummary(new PaymentSummaryData(0, 0), new PaymentSummaryData(0, 0));
  }

  public async Task PurgeDatabaseAsync()
  {
    await SendHttpRequestAsync("/admin/purge", HttpMethod.Delete);
  }

  public void Dispose()
  {
    _batchTimer?.Dispose();
    _httpClient.Dispose();
  }
}

public record BatchItem(
    IReadOnlyList<ProcessedPayment> Payments,
    TaskCompletionSource TaskCompletionSource
);
