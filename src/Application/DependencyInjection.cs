using Application.Abstractions.Messaging;
using Application.Examples;
using Application.Examples.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateExampleCommand, Guid>, CreateExampleCommandHandler>();
        services.AddScoped<IQueryHandler<GetExampleQuery, ExampleResponse>, GetExampleQueryHandler>();
        return services;
    }
}
