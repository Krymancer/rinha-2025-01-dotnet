namespace Api.Models
{
  public class PaymentProcessors
  {
    public PaymentProcessor? Default { get; set; }
    public PaymentProcessor? Fallback { get; set; }
  }

  public record PaymentProcessor(string BaseUrl);

};