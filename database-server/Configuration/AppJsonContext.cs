using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;

namespace Backend.Configuration;

[JsonSerializable(typeof(ProcessedPayment))]
[JsonSerializable(typeof(PaymentSummary))]
[JsonSerializable(typeof(PaymentSummaryData))]
[JsonSerializable(typeof(PurgeResponse))]
[JsonSerializable(typeof(ProcessedPayment[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
public partial class AppJsonContext : JsonSerializerContext
{
}
