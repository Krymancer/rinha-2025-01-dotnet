using Api.Services;
using Api.Services.Abstractions;

namespace AgoraVai.WebAPI.Services
{
  public sealed class DefaultPaymentProcessorService(HttpClient httpClient)
            : BasePaymentProcessorService(httpClient), IDefaultPaymentProcessorService
  {
    public override string ProcessorName => "default";
  }
}