namespace Api.Entities
{
  public class Payment
  {
    public Guid CorrelationId { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public string ProcessedBy { get; private set; } = string.Empty;

    public Payment WithProcessor(string processorName)
    {
      ProcessedBy = processorName;
      return this;
    }
  }
}