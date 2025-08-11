namespace Backend.Configuration;

public class AppConfig
{
  public ServerConfig Server { get; init; } = new();
  public string DatabaseSocketPath { get; init; } = "/tmp/db.sock";
  public PaymentProcessorsConfig PaymentProcessors { get; init; } = new();
  public PaymentWorkerConfig PaymentWorker { get; init; } = new();
  public PaymentRouterConfig PaymentRouter { get; init; } = new();
  public DatabaseClientConfig DatabaseClient { get; init; } = new();
}

public class ServerConfig
{
  public int Port { get; init; } = 8080;
  public string SocketPath { get; init; } = "/tmp/api.sock";
}

public class PaymentProcessorsConfig
{
  public PaymentProcessorConfig Default { get; init; } = new()
  {
    Url = "http://payment-processor-default:8080",
    Type = Models.ProcessorType.Default
  };

  public PaymentProcessorConfig Fallback { get; init; } = new()
  {
    Url = "http://payment-processor-fallback:8080",
    Type = Models.ProcessorType.Fallback
  };
}

public class PaymentProcessorConfig
{
  public string Url { get; init; } = "";
  public Models.ProcessorType Type { get; init; }
}

public class PaymentWorkerConfig
{
  public int BatchSize { get; init; } = 100;
}

public class PaymentRouterConfig
{
  public int RequestTimeoutMs { get; init; } = 3000;
}

public class DatabaseClientConfig
{
  public int BatchSize { get; init; } = 300;
  public int BatchTimeoutMs { get; init; } = 8;
}
