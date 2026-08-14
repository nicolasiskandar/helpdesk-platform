using NotificationService.Application.Interfaces;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Repositories;
using NotificationService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INotificationService, NotificationBusinessService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddHttpClient<IUserEmailResolver, IdentityUserEmailResolver>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["IdentityService:BaseUrl"] ?? "http://identity-service:8080");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<RabbitMQConsumer>();

        return services;
    }
}
