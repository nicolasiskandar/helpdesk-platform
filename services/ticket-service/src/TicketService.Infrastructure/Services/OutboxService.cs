using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketService.Application.Interfaces;
using TicketService.Domain.Interfaces;

namespace TicketService.Infrastructure.Services;

public class OutboxService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxService> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _pollInterval;

    public OutboxService(IServiceProvider serviceProvider, ILogger<OutboxService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _batchSize = int.TryParse(configuration["Outbox:BatchSize"], out var b) ? b : 20;
        _pollInterval = TimeSpan.FromSeconds(
            int.TryParse(configuration["Outbox:PollIntervalSeconds"], out var i) ? i : 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox poller started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var messages = await unitOfWork.Outbox.GetUnprocessedMessagesAsync(_batchSize);
                foreach (var message in messages)
                {
                    try
                    {
                        await publisher.PublishAsync(message.EventType, message.Payload);
                        await unitOfWork.Outbox.MarkAsProcessedAsync(message.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish outbox message {Id}", message.Id);
                        await unitOfWork.Outbox.MarkAsFailedAsync(message.Id, ex.Message);
                    }
                }

                await unitOfWork.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox poller error");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
