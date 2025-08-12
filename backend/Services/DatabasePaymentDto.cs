using System.Text.Json.Serialization;

namespace Backend.Services;

// DTO for sending data to database server
public record DatabaseProcessedPayment(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("processor")] string Processor,
    [property: JsonPropertyName("requestedAt")] string RequestedAt
);
