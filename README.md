# Serilog.Sinks.Queuing.Redis
Write Serilog events to Redis Stream and store them in Elasticsearch

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
                                               options.Connection = "http://localhost:9200"
                                               options.Index = "log-";
                                               options.IndexFormatPattern = "yyyyMMdd";
                                           });
builder.Services.AddRedisQueuingSink((provider, options) =>
                                     {
                                         var hook = provider.GetRequiredService<IRedisStreamHook>();
                                         var httpAccessor = provider.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
                                         
                                         // Use EcsTextFormatter
                                         var formatterConfig = new EcsTextFormatterConfiguration();
                                         formatterConfig.MapHttpContext(httpAccessor);

                                         options.LogFormatter = new EcsTextFormatter(formatterConfig);
                                         options.RedisConnectionString = "127.0.0.1:6379,writeBuffer=102400,syncTimeout=30000";
                                         options.StreamKey = "logging";
                                         options.NotficationChannel = options.StreamKey;
                                         options.RedisStreamHook = hook;
                                         options.OlderLogsKeepDays = 7;
                                     });
builder.ConfigureLogging((context, builder) =>
                         {
                             builder.AddSerilog();
                         })
       .UseSerilog((ctx, provider, lc) =>
                   {
                       Serilog.Debugging.SelfLog.Enable(Console.Error.WriteLine)
                       lc.ReadFrom.Configuration(ctx.Configuration)
                         .Enrich.FromLogContext()
                         .WriteTo
                         .Async(cfg =>
                                {
                                    cfg.RedisQueuingSink(provider);
                                });
                   });
```