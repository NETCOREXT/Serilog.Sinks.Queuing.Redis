# Serilog.Sinks.Queuing.Redis

[![Nuget](https://img.shields.io/nuget/v/Serilog.Sinks.Queuing.Redis)](https://www.nuget.org/packages/Serilog.Sinks.Queuing.Redis)

Write Serilog events to Redis Stream and store them in Elasticsearch

---

## Example for ASP.NET 6
### Step 1
Add package reference
```
dotnet add package Serilog.Sinks.Queuing.Redis.ElasticHook
```
### Step 2
Configure Logging in Program.cs
```
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddElasticRedisStreamHook((provider, options) =>
                                           {
                                               options.ElasticsearchHost = "http://localhost:9200";
                                               options.ApiKey = "foo bar";
                                               options.Index = "log-";
                                               options.IndexFormatPattern = "yyyyMMdd";
                                           });
builder.Services.AddRedisQueuingSink((provider, options) =>
                                     {
                                         options.RedisConnectionString = "127.0.0.1:6379";
                                         options.StreamKey = "logging";
                                         options.NotificationChannel = "logging";
                                     });
builder.ConfigureLogging((context, builder) =>
                         {
                             builder.ClearProviders();
                             builder.AddSerilog();
                         })
       .UseSerilog((ctx, provider, lc) =>
                   {
                       var redisQueuingSinkOptions = provider.GetRequiredService<RedisQueuingSinkOptions>();
                       
                       lc.ReadFrom.Configuration(ctx.Configuration)
                         .Enrich.FromLogContext()
                         .WriteTo.RedisQueuingSink(redisQueuingSinkOptions);
                   });
```