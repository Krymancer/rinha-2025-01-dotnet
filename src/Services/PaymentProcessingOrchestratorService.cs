using Api.Entities;
using Api.Services.Abstractions;
using Api.Utils;

namespace Api.Services
{
  public sealed class PaymentProcessingOrchestratorService : IPaymentProcessingOrchestratorService
  {
    private readonly IDefaultPaymentProcessorService _defaultProcessor;
    private readonly IFallbackPaymentProcessorService _fallbackProcessor;

    public PaymentProcessingOrchestratorService(
        IDefaultPaymentProcessorService defaultProcessor,
        IFallbackPaymentProcessorService fallbackProcessor)
    {
      _defaultProcessor = defaultProcessor;
      _fallbackProcessor = fallbackProcessor;
    }

    public async ValueTask<Result<Payment>> ProcessAsync(
        Payment payment, CancellationToken cancellationToken = default)
    {
      var success = await _defaultProcessor.ProcessAsync(payment, cancellationToken)
          .ConfigureAwait(false);
      if (success)
      {
        return Result<Payment>.Success(
            payment.WithProcessor(_defaultProcessor.ProcessorName));
      }

      success = await _fallbackProcessor.ProcessAsync(payment, cancellationToken)
          .ConfigureAwait(false);
      if (success)
      {
        return Result<Payment>.Success(
            payment.WithProcessor(_fallbackProcessor.ProcessorName));
      }

      return Result<Payment>.Failure();
    }
  }
}