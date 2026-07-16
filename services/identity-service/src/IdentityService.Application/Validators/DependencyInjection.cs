using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Application.Validators;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        return services;
    }
}
