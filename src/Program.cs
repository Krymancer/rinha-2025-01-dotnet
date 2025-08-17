using System.Runtime;
using System.Text.Json.Serialization;
using Api.Models;
using Api.Utils;

GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddHealthChecks();

builder.Services.AddHttpClients(builder.Configuration);

var app = builder.Build();

app.MapPost("/payments", () =>
{
    throw new NotImplementedException();
});

app.MapGet("/payments-summary", () =>
{
    throw new NotImplementedException();
});

app.MapPost("/purge-payments", () =>
{
    throw new NotImplementedException();
});


app.MapHealthChecks("/health");

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
