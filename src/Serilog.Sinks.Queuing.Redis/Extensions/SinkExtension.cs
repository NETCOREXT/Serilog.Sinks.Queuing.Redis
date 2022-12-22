using FreeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Netcorext.Extensions.Redis.Utilities;
using Netcorext.Serialization;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Sinks.Queuing.Redis.Internals;

namespace Serilog.Sinks.Queuing.Redis.Extensions;

public static class SinkExtensions
{
    public static LoggerConfiguration RedisQueuingSink(this LoggerSinkConfiguration loggerConfiguration, RedisQueuingSinkOptions options)
    {
        return loggerConfiguration.Sink(new RedisQueuingSink(options));
    }

    public static IServiceCollection AddRedisQueuingSink(this IServiceCollection services, Action<IServiceProvider, RedisQueuingSinkOptions>? configure)
    {
        services.TryAddSingleton(provider =>
                                 {
                                     var config = new RedisQueuingSinkOptions();

                                     configure?.Invoke(provider, config);

                                     return config;
                                 });
        
        services.AddHostedService<RedisQueuingSinkWorker>();

        return services;
    }
}