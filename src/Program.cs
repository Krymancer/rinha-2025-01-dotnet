using System.Runtime;
using System.Text.Json.Serialization;
using AgoraVai.WebAPI.Jobs;
using Api.Channels;
using Api.Contracts.Requests;
using Api.Models;
using Api.Repositories;
using Api.Repositories.Abstractions;
using Api.Services;
using Api.Services.Abstractions;
using Api.Utils;
using Microsoft.AspNetCore.Mvc;

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

builder.Services.AddSingleton<ProcessingChannel>();
builder.Services.AddSingleton<PersistenceChannel>();
builder.Services.AddHostedService<PaymentProcessingJob>();
builder.Services.AddHostedService<PaymentPersistingJob>();

var processingBatchSize = builder.Configuration.GetRequiredSection("JobsConfig:ProcessingBatchSize").Get<int>();
var processingParalellism = builder.Configuration.GetRequiredSection("JobsConfig:ProcessingParalellism").Get<int>();
var processingWait = builder.Configuration.GetRequiredSection("JobsConfig:ProcessingWait").Get<int>();
var persistenceBatchSize = builder.Configuration.GetRequiredSection("JobsConfig:PersistenceBatchSize").Get<int>();
var persistenceWait = builder.Configuration.GetRequiredSection("JobsConfig:PersistenceWait").Get<int>();
builder.Services.AddSingleton(new JobConfig
{
    ProcessingBatchSize = processingBatchSize,
    ProcessingParalellism = processingParalellism,
    ProcessingWait = processingWait,
    PersistenceBatchSize = persistenceBatchSize,
    PersistenceWait = persistenceWait
});

var connectionString = builder.Configuration.GetConnectionString("Postgres")!;
builder.Services.AddScoped<IPaymentRepository>(_ => new PaymentRepository(connectionString));

builder.Services.AddHttpClients(builder.Configuration);
builder.Services.AddScoped<IPaymentProcessingOrchestratorService, PaymentProcessingOrchestratorService>();

var app = builder.Build();

app.MapPost("/payments", static async (
[FromServices] ProcessingChannel processingChannel,
[FromBody] PaymentRequest request) =>
{
    await processingChannel.WriteAsync(request)
        .ConfigureAwait(false);
    return Results.Accepted();
});

app.MapGet("/payments-summary", async (
[FromServices] IPaymentRepository paymentRepository,
[FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to) =>
{
    var payments = await paymentRepository.GetProcessorsSummaryAsync(from, to)
        .ConfigureAwait(false);

    var defaultPayment = payments
        .FirstOrDefault(p => p.ProcessedBy == "default")
            ?? new SummaryRowReadModel();
    var fabllbackPayment = payments
        .FirstOrDefault(p => p.ProcessedBy == "fallback")
            ?? new SummaryRowReadModel();

    return Results.Ok(new SummariesReadModel(
        new SummaryReadModel(defaultPayment.TotalRequests, defaultPayment.TotalAmount),
        new SummaryReadModel(fabllbackPayment.TotalRequests, fabllbackPayment.TotalAmount)));
});

app.MapPost(
"/purge-payments",
async ([FromServices] IPaymentRepository paymentRepository) =>
{
    await paymentRepository.PurgeAsync()
        .ConfigureAwait(false);
    return Results.Ok();
});

app.MapHealthChecks("/health");

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(JobConfig))]
[JsonSerializable(typeof(PaymentRequest))]
[JsonSerializable(typeof(SummariesReadModel))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
