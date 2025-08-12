using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Configuration;
using Backend.Models;

namespace Backend.Services;

public class HealthCheckService : IDisposable
{
  private readonly HttpClient _httpClient;
  private readonly ConcurrentDictionary<ProcessorType, ProcessorHealthInfo> _healthCache;
  private readonly ConcurrentDictionary<ProcessorType, DateTime> _lastHealthCheck;
  private readonly TimeSpan _rateLimitInterval = TimeSpan.FromSeconds(5);
  private readonly AppConfig _config;

  public HealthCheckService(AppConfig config)
  {
    _config = config;
    _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }; // Reduced timeout
    _healthCache = new ConcurrentDictionary<ProcessorType, ProcessorHealthInfo>();
    _lastHealthCheck = new ConcurrentDictionary<ProcessorType, DateTime>();

    // Initialize with HEALTHY default state (assume working until proven otherwise)
    _healthCache[ProcessorType.Default] = new ProcessorHealthInfo(false, 50);
    _healthCache[ProcessorType.Fallback] = new ProcessorHealthInfo(false, 100);
  }

  public async Task<ProcessorHealthInfo> GetHealthAsync(ProcessorType processorType)
  {
    var now = DateTime.UtcNow;
    var lastCheck = _lastHealthCheck.GetValueOrDefault(processorType, DateTime.MinValue);

    // Respect rate limit - only check every 5 seconds
    if (now - lastCheck < _rateLimitInterval)
    {
      return _healthCache.GetValueOrDefault(processorType, new ProcessorHealthInfo(true, 1000));
    }

    try
    {
      var url = processorType == ProcessorType.Default
          ? _config.PaymentProcessors.Default.Url
          : _config.PaymentProcessors.Fallback.Url;

      var response = await _httpClient.GetAsync($"{url}/payments/service-health");

      if (response.IsSuccessStatusCode)
      {
        var content = await response.Content.ReadAsStringAsync();
        var healthData = JsonSerializer.Deserialize<ProcessorHealthResponse>(content, AppJsonContext.Default.ProcessorHealthResponse);

        if (healthData != null)
        {
          var healthInfo = new ProcessorHealthInfo(healthData.Failing, healthData.MinResponseTime);
          _healthCache[processorType] = healthInfo;
          _lastHealthCheck[processorType] = now;

          Console.WriteLine($"Health check {processorType}: failing={healthData.Failing}, minTime={healthData.MinResponseTime}ms");
          return healthInfo;
        }
      }
    }
    catch (Exception ex)
    {
      // Don't log health check failures as errors - they're expected during instability
      // Only mark as failing if we get explicit failures, not timeouts
      if (ex is TaskCanceledException or TimeoutException)
      {
        // Timeout = assume healthy but slow
        var timeoutHealth = new ProcessorHealthInfo(false, 3000);
        _healthCache[processorType] = timeoutHealth;
        _lastHealthCheck[processorType] = now;
        return timeoutHealth;
      }

      // Other exceptions might indicate actual failure
      Console.WriteLine($"Health check failed for {processorType}: {ex.Message}");
      var failedHealth = new ProcessorHealthInfo(true, 5000);
      _healthCache[processorType] = failedHealth;
      _lastHealthCheck[processorType] = now;
      return failedHealth;
    }

    // Return cached value or assume healthy
    return _healthCache.GetValueOrDefault(processorType, new ProcessorHealthInfo(false, 100));
  }

  public ProcessorType GetOptimalProcessor()
  {
    var defaultHealth = _healthCache.GetValueOrDefault(ProcessorType.Default, new ProcessorHealthInfo(false, 100));
    var fallbackHealth = _healthCache.GetValueOrDefault(ProcessorType.Fallback, new ProcessorHealthInfo(false, 200));

    // Always prefer default if it's not failing
    if (!defaultHealth.Failing)
    {
      return ProcessorType.Default;
    }

    // Use fallback if default is failing
    if (!fallbackHealth.Failing)
    {
      return ProcessorType.Fallback;
    }

    // If both are failing, still prefer default (lower fees)
    return ProcessorType.Default;
  }

  public void Dispose()
  {
    _httpClient.Dispose();
  }
}

public record ProcessorHealthInfo(bool Failing, int MinResponseTime);
