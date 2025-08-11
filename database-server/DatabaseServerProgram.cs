using System.Diagnostics;
using System.Text.Json;
using Backend.Configuration;
using Backend.Database;
using Backend.Models;

namespace Backend.Database;

public class DatabaseServerProgram
{
    public static async Task Main(string[] args)
    {
        var socketPath = Environment.GetEnvironmentVariable("DATABASE_SOCKET_PATH") ?? "/tmp/db.sock";

        // Clean up existing socket file
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
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
            serverOptions.ListenUnixSocket(socketPath);
        });

        // Register the database service
        builder.Services.AddSingleton<DatabaseService>();

        var app = builder.Build();

        var databaseService = app.Services.GetRequiredService<DatabaseService>();

        app.MapPost("/payments/batch", (ProcessedPayment[] payments) =>
        {
            databaseService.PersistPayments(payments);
            return Results.Ok();
        });

        app.MapGet("/payments-summary", (string? from, string? to) =>
        {
            var summary = databaseService.GetDatabaseSummary(from, to);
            return Results.Ok(summary);
        });

        app.MapDelete("/admin/purge", async () =>
        {
            await databaseService.PurgeDatabaseAsync();
            var response = new PurgeResponse(
                "MemoryDB purged successfully",
                DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            );
            return Results.Ok(response);
        });

        Console.WriteLine($"🗄️  MemoryDB Server running on unix socket: {socketPath}");

        // Set permissions on Unix socket
        if (File.Exists(socketPath))
        {
            Process.Start("chmod", $"666 {socketPath}");
        }

        await app.RunAsync();
    }
}
