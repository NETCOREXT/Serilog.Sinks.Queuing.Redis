using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Configuration;

namespace Serilog.Sinks.Queuing.Redis.Extensions;

public static class SinkExtensions
{
    public static LoggerConfiguration RedisQueuingSink(this LoggerSinkConfiguration loggerConfiguration, RedisQueuingSinkOptions options)
    {
        return loggerConfiguration.Sink(new RedisQueuingSink(options));
    }

    public static IServiceCollection AddRedisQueuingSink(this IServiceCollection services, Action<IServiceProvider, RedisQueuingSinkOptions>? configure)
    {
        var config = new RedisQueuingSinkOptions();
        
        services.TryAddSingleton(provider =>
                                 {
                                     configure?.Invoke(provider, config);

                                     return config;
                                 });

        if (!config.EnableWorker) return services;

        services.AddWorkerRunner<RedisQueuingWorker, PendingStreamRunner>();
        services.AddWorkerRunner<RedisQueuingWorker, RedisConsumerRunner>();
        services.AddHostedService<RedisQueuingWorker>();

        return services;
    }
}