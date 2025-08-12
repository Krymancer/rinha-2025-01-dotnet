using System.Text.Json.Serialization;

namespace Backend.Models;

// Simple models for database operations only
public record ProcessedPayment(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("processor")] string Processor,
    [property: JsonPropertyName("requestedAt")] string RequestedAt
);

public record PaymentSummaryData(
    [property: JsonPropertyName("totalRequests")] int TotalRequests,
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
