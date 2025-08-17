using Api.Entities;
using Api.Utils;

namespace Api.Services.Abstractions
{
  public interface IPaymentProcessingOrchestratorService
  {
    ValueTask<Result<Payment>> ProcessAsync(
        Payment payment, CancellationToken cancellationToken = default);
  }
}