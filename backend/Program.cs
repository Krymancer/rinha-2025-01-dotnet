using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton<NpgsqlConnection>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("default");
    return new NpgsqlConnection(connectionString);
});

builder.Services.AddHttpClient("default", client =>
{
  client.BaseAddress = new Uri(builder.Configuration.GetConnectionString("default"));
});

builder.Services.AddHttpClient("fallback", client =>
{
  client.BaseAddress = new Uri(builder.Configuration.GetConnectionString("fallback"));
});

var app = builder.Build();

app.MapGet('/healthcheck', async () => 
{
  return Results.Ok("healthy");
});

app.MapGet('/payments-summary', async() => 
{
  return Results.Ok("TODO");
});

app.MapPost('/payments', async () => 
{
  return Results.Ok("TODO");
});

await app.RunAsync();

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(DateTime))]
public partial class JsonContext : JsonSerializerContext { }