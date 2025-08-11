using Backend.Models;
using Backend.Services;

namespace Backend.Services;

public class PaymentQuery
{
  private readonly DatabaseClient _databaseClient;

  public PaymentQuery(DatabaseClient databaseClient)
  {
    _databaseClient = databaseClient;
  }

  public async Task<PaymentSummary> GetPaymentsSummaryAsync(string? from = null, string? to = null)
  {
    var result = await _databaseClient.GetDatabaseSummaryAsync(from, to);

    return new PaymentSummary(
        Default: new PaymentSummaryData(
            TotalRequests: RoundToCommercialAmount(result.Default.TotalRequests),
            TotalAmount: RoundToCommercialAmount(result.Default.TotalAmount)
        ),
        Fallback: new PaymentSummaryData(
            TotalRequests: RoundToCommercialAmount(result.Fallback.TotalRequests),
            TotalAmount: RoundToCommercialAmount(result.Fallback.TotalAmount)
        )
    );
  }

  private static decimal RoundToCommercialAmount(decimal amount)
  {
    return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
  }
}
