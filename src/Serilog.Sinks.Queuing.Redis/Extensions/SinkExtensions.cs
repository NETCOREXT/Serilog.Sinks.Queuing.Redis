using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Sinks.Queuing.Redis.Internal;
using StackExchange.Redis;

namespace Serilog.Sinks.Queuing.Redis.Extensions;

public static class SinkExtensions
{
    public static LoggerConfiguration RedisQueuingSink(this LoggerSinkConfiguration loggerConfiguration, IServiceProvider provider)
    {
        var queueSink = provider.GetServices<ILogEventSink>().First(t => t.GetType() == typeof(RedisQueuingSink));

        return loggerConfiguration.Sink(queueSink);
    }

    public static IServiceCollection AddRedisQueuingSink(this IServiceCollection services, Action<IServiceProvider, RedisQueuingSinkOptions> configure)
    {
        services.TryAddSingleton(provider =>
                                 {
                                     var config = new RedisQueuingSinkOptions();

                                     configure?.Invoke(provider, config);

                                     return config;
                                 });

        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
                                                         {
                                                             var opt = provider.GetRequiredService<RedisQueuingSinkOptions>();

                                                             return new RedisConnection(opt.RedisConnectionString).Connection;
                                                         });

        services.TryAddTransient<IDatabase>(provider =>
                                            {
                                                var redis = provider.GetRequiredService<IConnectionMultiplexer>();

                                                return redis.GetDatabase();
                                            });

        services.TryAddTransient<ISubscriber>(provider =>
                                              {
                                                  var redis = provider.GetRequiredService<IConnectionMultiplexer>();

                                                  return redis.GetSubscriber();
                                              });

        services.AddSingleton<ILogEventSink, RedisQueuingSink>();
        services.AddHostedService<RedisQueuingSinkWorker>();

        return services;
    }
}