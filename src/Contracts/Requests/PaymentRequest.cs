namespace Api.Contracts.Requests
{
  public record PaymentRequest(Guid CorrelationId, decimal Amount);
}