using Api.Channels;
using Api.Entities;
using Api.Models;
using Api.Repositories;
using Api.Repositories.Abstractions;
using Api.Utils;
using System.Diagnostics;
using System.Threading.Channels;

namespace AgoraVai.WebAPI.Jobs
{
  public sealed class PaymentPersistingJob : BackgroundService
  {
    private readonly ChannelReader<Payment> _reader;
    private readonly IServiceProvider _serviceProvider;
    private readonly JobConfig _jobsConfig;
    private readonly ILogger<PaymentPersistingJob> _logger;

    public PaymentPersistingJob(
        PersistenceChannel channel,
        IServiceProvider serviceProvider,
        JobConfig jobsConfig,
        ILogger<PaymentPersistingJob> logger)
    {
      _reader = channel.GetReader();
      _serviceProvider = serviceProvider;
      _jobsConfig = jobsConfig;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      var batchSize = _jobsConfig.PersistenceBatchSize!.Value;
      var maxWait = _jobsConfig.PersistenceWait!.Value;

      using var scope = _serviceProvider.CreateScope();
      var repository = scope.ServiceProvider
          .GetRequiredService<IPaymentRepository>();

      var stopwatch = new Stopwatch();
      var buffer = new List<Payment>(batchSize);
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          buffer.Clear();
          stopwatch.Restart();

          var first = await _reader.ReadAsync(stoppingToken)
              .ConfigureAwait(false);
          buffer.Add(first);

          var batchStart = Stopwatch.GetTimestamp();
          while (buffer.Count < batchSize)
          {
            var elapsed = batchStart.ElapsedMilliseconds();
            if (elapsed >= maxWait) break;

            var readTask = _reader.WaitToReadAsync(stoppingToken).AsTask();
            var delayTask = Task.Delay(maxWait - (int)elapsed, stoppingToken);

            var winner = await Task.WhenAny(readTask, delayTask)
                .ConfigureAwait(false);
            if (winner == delayTask || !readTask.Result) break;

            while (buffer.Count < batchSize && _reader.TryRead(out var item))
              buffer.Add(item);
          }

          await repository.InserBatchAsync(buffer)
              .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Persistence error!");
        }
      }
    }
  }
}