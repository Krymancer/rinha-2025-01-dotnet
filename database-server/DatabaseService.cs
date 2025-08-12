using Backend.Models;

namespace Backend.Database;

public class DatabaseService
{
  private readonly MemoryStore _memoryStore;

  public DatabaseService()
  {
    _memoryStore = new MemoryStore();
  }

  public void PersistPayments(IReadOnlyList<ProcessedPayment> payments)
  {
    if (payments.Count == 0) return;

    foreach (var payment in payments)
    {
      if (DateTimeOffset.TryParse(payment.RequestedAt, out var timestamp))
      {
        // Now correctly track the actual processor used
        _memoryStore.Add(timestamp.ToUnixTimeMilliseconds(), payment.Amount, payment.Processor);
      }
    }
  }

  public PaymentSummary GetDatabaseSummary(string? from = null, string? to = null)
  {
    var allData = _memoryStore.GetAll();

    var fromTimestamp = !string.IsNullOrEmpty(from) && DateTimeOffset.TryParse(from, out var fromDate)
        ? fromDate.ToUnixTimeMilliseconds()
        : 0;

    var toTimestamp = !string.IsNullOrEmpty(to) && DateTimeOffset.TryParse(to, out var toDate)
        ? toDate.ToUnixTimeMilliseconds()
        : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    var filteredData = allData.Where(item =>
        item.Timestamp >= fromTimestamp && item.Timestamp <= toTimestamp).ToList();

    var defaultItems = filteredData.Where(item => item.Processor == "default");
    var fallbackItems = filteredData.Where(item => item.Processor == "fallback");

    var defaultSummary = new PaymentSummaryData(
        TotalRequests: defaultItems.Count(),
        TotalAmount: defaultItems.Sum(item => item.Value)
    );

    var fallbackSummary = new PaymentSummaryData(
        TotalRequests: fallbackItems.Count(),
        TotalAmount: fallbackItems.Sum(item => item.Value)
    );

    return new PaymentSummary(
        Default: defaultSummary,
        Fallback: fallbackSummary
    );
  }

  public Task PurgeDatabaseAsync()
  {
    _memoryStore.Clear();
    Console.WriteLine("MemoryStore database purged successfully");
    return Task.CompletedTask;
  }
}
