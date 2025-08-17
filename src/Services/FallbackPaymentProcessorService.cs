using Api.Services;
using Api.Services.Abstractions;

namespace AgoraVai.WebAPI.Services
{
  public sealed class FallbackPaymentProcessorService(
      HttpClient httpClient)
            : BasePaymentProcessorService(httpClient), IFallbackPaymentProcessorService
  {
    public override string ProcessorName => "fallback";
  }
}