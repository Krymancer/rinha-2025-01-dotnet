using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Entities;
using Api.Services.Abstractions;

namespace Api.Services
{
  public abstract class BasePaymentProcessorService(HttpClient httpClient) : IPaymentProcessorService
  {
    private readonly HttpClient _httpClient = httpClient;

    public abstract string ProcessorName { get; }

    public async ValueTask<bool> ProcessAsync(
        Payment payment, CancellationToken cancellationToken = default)
    {
      try
      {
        using var content = new StringContent(
            content: JsonSerializer.Serialize(
                payment,
                PaymentJsonContext.Default.Payment),
            encoding: Encoding.UTF8,
            mediaType: "application/json");

        using var result = await _httpClient.PostAsync(
            "/payments", content, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccessStatusCode;
      }
      catch
      {
        return false;
      }
    }
  }

  [JsonSerializable(typeof(Payment))]
  public partial class PaymentJsonContext : JsonSerializerContext
  {
  }
}