using System.Reflection;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Skill.Suite.Application.Behaviors;

namespace Skill.Suite.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Mediator (source-generated). The generator emits AddMediator extension
        // — invoked here so handlers from this assembly are registered.
        services.AddMediator(opts => { opts.ServiceLifetime = ServiceLifetime.Scoped; });

        // FluentValidation — auto-register every validator in this assembly
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Validation pipeline behavior runs validators before handlers
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
