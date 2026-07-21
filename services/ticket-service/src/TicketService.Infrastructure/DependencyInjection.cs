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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
        services.AddScoped<IEventPublisher, RabbitMQPublisher>();
        services.AddScoped<ITicketService, TicketBusinessService>();

        services.AddHostedService<OutboxService>();

        return services;
    }
}
