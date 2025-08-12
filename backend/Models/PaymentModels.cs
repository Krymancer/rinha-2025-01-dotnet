using System.Text.Json.Serialization;

namespace Backend.Models;

public record PaymentRequest(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("amount")] decimal Amount
);

public record PaymentProcessorRequest(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("requestedAt")] string RequestedAt
);

public record ProcessedPayment(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("processor")] ProcessorType Processor,
    [property: JsonPropertyName("requestedAt")] string RequestedAt
);

public record PaymentSummaryData(
    [property: JsonPropertyName("totalRequests")] decimal TotalRequests,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount
);

public record PaymentSummary(
    [property: JsonPropertyName("default")] PaymentSummaryData Default,
    [property: JsonPropertyName("fallback")] PaymentSummaryData Fallback
);

public record PurgeResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("timestamp")] string Timestamp
);

public record ProcessorHealthResponse(
    [property: JsonPropertyName("failing")] bool Failing,
    [property: JsonPropertyName("minResponseTime")] int MinResponseTime
);

[JsonConverter(typeof(JsonStringEnumConverter<ProcessorType>))]
public enum ProcessorType
{
    [JsonPropertyName("default")]
    Default,
    [JsonPropertyName("fallback")]
    Fallback
}

public record PaymentProcessor(
    string Url,
    ProcessorType Type
);
