using System.Text.Json;
using Backend.Configuration;
using Backend.Models;
using Backend.Services;

var config = new AppConfig
{
  Server = new ServerConfig
  {
    Port = int.TryParse(Environment.GetEnvironmentVariable("SERVER_PORT"), out var port) ? port : 8080,
    SocketPath = Environment.GetEnvironmentVariable("SERVER_SOCKET_PATH") ?? "/tmp/api.sock"
  },
  DatabaseSocketPath = Environment.GetEnvironmentVariable("DATABASE_SOCKET_PATH") ?? "/tmp/db.sock",
  PaymentProcessors = new PaymentProcessorsConfig
  {
    Default = new PaymentProcessorConfig
    {
      Url = Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_URL_DEFAULT") ?? "http://payment-processor-default:8080",
      Type = ProcessorType.Default
    },
    Fallback = new PaymentProcessorConfig
    {
      Url = Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_URL_FALLBACK") ?? "http://payment-processor-fallback:8080",
      Type = ProcessorType.Fallback
    }
  }
};

// Clean up existing socket file
if (File.Exists(config.Server.SocketPath))
{
  File.Delete(config.Server.SocketPath);
}

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

// Configure Kestrel for Unix socket
builder.WebHost.ConfigureKestrel(serverOptions =>
{
  serverOptions.ListenUnixSocket(config.Server.SocketPath);
});

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<QueueService<PaymentRequest>>();
builder.Services.AddSingleton<PaymentProcessorRouter>();
builder.Services.AddSingleton(provider => new DatabaseClient(provider.GetRequiredService<AppConfig>()));
builder.Services.AddSingleton<PaymentCommand>();
builder.Services.AddSingleton<PaymentQuery>();

var app = builder.Build();

// Get services from DI
var paymentCommand = app.Services.GetRequiredService<PaymentCommand>();
var paymentQuery = app.Services.GetRequiredService<PaymentQuery>();

// Configure endpoints
app.MapPost("/payments", (PaymentRequest paymentInput) =>
{
  paymentCommand.Enqueue(paymentInput);
  return Results.Ok();
});

app.MapGet("/payments-summary", async (string? from, string? to) =>
{
  var summary = await paymentQuery.GetPaymentsSummaryAsync(from, to);
  Console.WriteLine($"/payments-summary {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
  return Results.Ok(summary);
});

app.MapDelete("/admin/purge", async () =>
{
  await paymentCommand.PurgeAllAsync();
  var response = new PurgeResponse(
      "Purge operation completed",
      DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
  );
  return Results.Ok(response);
});

// Set socket permissions for nginx access
app.Lifetime.ApplicationStarted.Register(() =>
{
  try
  {
    if (OperatingSystem.IsLinux() && File.Exists(config.Server.SocketPath))
    {
      File.SetUnixFileMode(config.Server.SocketPath,
          UnixFileMode.UserRead | UnixFileMode.UserWrite |
          UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
          UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
    }
  }
  catch (Exception ex)
  {
    Console.WriteLine($"Failed to set socket permissions: {ex.Message}");
  }
});

var hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
Console.WriteLine($"🚀 [{hostname}] Server running on unix socket: {config.Server.SocketPath}");

await app.RunAsync();