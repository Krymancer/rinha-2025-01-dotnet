using Backend.Configuration;
using Backend.Models;

namespace Backend.Database;

public class DatabaseServer
{
  private readonly DatabaseService _databaseService;
  private readonly string _socketPath;

  public DatabaseServer(string socketPath = "/tmp/db.sock")
  {
    _databaseService = new DatabaseService();
    _socketPath = socketPath;
  }

  public async Task StartAsync()
  {
    if (File.Exists(_socketPath))
    {
      File.Delete(_socketPath);
    }

    var builder = WebApplication.CreateSlimBuilder();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
    });

    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
      serverOptions.ListenUnixSocket(_socketPath);
    });

    var app = builder.Build();

    app.MapPost("/payments/batch", (ProcessedPayment[] payments) =>
    {
      _databaseService.PersistPayments(payments);
      return Results.Ok();
    });

    app.MapGet("/payments-summary", (string? from, string? to) =>
    {
      var summary = _databaseService.GetDatabaseSummary(from, to);
      return Results.Ok(summary);
    });

    app.MapDelete("/admin/purge", async () =>
    {
      await _databaseService.PurgeDatabaseAsync();
      var response = new PurgeResponse(
              "Data purged successfully",
              DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
          );
      return Results.Ok(response);
    });

    Console.WriteLine($"🗄️  MemoryDB Server running on unix socket: {_socketPath}");

    await app.RunAsync();
  }
}
