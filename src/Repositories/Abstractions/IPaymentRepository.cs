using Api.Entities;

namespace Api.Repositories.Abstractions
{
  public record SummariesReadModel(
      SummaryReadModel Default,
      SummaryReadModel Fallback);

  public sealed record SummaryReadModel(
      long TotalRequests, decimal TotalAmount);

  public sealed class SummaryRowReadModel
  {
    public string ProcessedBy { get; init; } = string.Empty;
    public long TotalRequests { get; init; }
    public decimal TotalAmount { get; init; }
  }

  public interface IPaymentRepository
  {
    ValueTask InserBatchAsync(IEnumerable<Payment> payments);
    ValueTask<IEnumerable<SummaryRowReadModel>> GetProcessorsSummaryAsync(
        DateTimeOffset? from, DateTimeOffset? to);
    ValueTask PurgeAsync();
  }
}