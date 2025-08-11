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
        _memoryStore.Add(timestamp.ToUnixTimeMilliseconds(), payment.Amount);
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

    var defaultSummary = new PaymentSummaryData(0, 0);
    var fallbackSummary = new PaymentSummaryData(0, 0);

    var defaultItems = filteredData.Where(item => item.Processor == ProcessorType.Default);
    var fallbackItems = filteredData.Where(item => item.Processor == ProcessorType.Fallback);

    if (defaultItems.Any())
    {
      defaultSummary = new PaymentSummaryData(
          TotalRequests: defaultItems.Count(),
          TotalAmount: defaultItems.Sum(item => item.Value)
      );
    }

    if (fallbackItems.Any())
    {
      fallbackSummary = new PaymentSummaryData(
          TotalRequests: fallbackItems.Count(),
          TotalAmount: fallbackItems.Sum(item => item.Value)
      );
    }

    return new PaymentSummary(defaultSummary, fallbackSummary);
  }

  public Task PurgeDatabaseAsync()
  {
    _memoryStore.Clear();
    Console.WriteLine("MemoryStore database purged successfully");
    return Task.CompletedTask;
  }
}
