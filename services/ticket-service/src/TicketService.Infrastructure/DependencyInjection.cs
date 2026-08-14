using TicketService.Application.Interfaces;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using TicketService.Infrastructure.Repositories;
using TicketService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TicketService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TicketDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        var uploadsDir = configuration["FileStorage:UploadsDirectory"] ?? "uploads";
        services.AddSingleton<IFileStorageService>(new LocalFileStorageService(uploadsDir));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
        services.AddSingleton<IEventPublisher, RabbitMQPublisher>();
        services.AddScoped<ITicketService, TicketBusinessService>();
        services.AddScoped<IKbArticleService, KbArticleService>();

        services.AddHttpContextAccessor();
        var identityBaseUrl = configuration["IdentityService:BaseUrl"] ?? "http://identity-service:8080";
        services.AddHttpClient<IUserLookupService, IdentityUserLookupService>(client =>
        {
            client.BaseAddress = new Uri(identityBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<OutboxService>();

        return services;
    }
}
