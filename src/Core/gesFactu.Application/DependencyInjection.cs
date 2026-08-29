using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace gesFactu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // MediatR para CQRS
        services.AddMediatR(config => config.RegisterServicesFromAssemblies(typeof(DependencyInjection).Assembly));

        // FluentValidation - registrar todos los validadores del assembly
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}

