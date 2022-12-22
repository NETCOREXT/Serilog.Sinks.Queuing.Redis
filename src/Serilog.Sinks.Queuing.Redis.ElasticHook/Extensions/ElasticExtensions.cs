using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Serilog.Sinks.Queuing.Redis.ElasticHook.Extensions;

public static class ElasticExtensions
{
    public static IServiceCollection AddElasticRedisStreamHook(this IServiceCollection services, Action<IServiceProvider, ElasticHookOptions>? configure)
    {
        services.TryAddSingleton(provider =>
                                 {
                                     var options = new ElasticHookOptions();

                                     configure?.Invoke(provider, options);

                                     return options;
                                 });

        services.TryAddSingleton<IRedisStreamHook, ElasticRedisStreamHook>();

        return services;
    }
}