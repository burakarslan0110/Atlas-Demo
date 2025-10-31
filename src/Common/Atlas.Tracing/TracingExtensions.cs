using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Atlas.Tracing;




public static class TracingExtensions
{







    public static IServiceCollection AddAtlasTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var jaegerHost = configuration["Jaeger:Host"] ?? "jaeger";
        var jaegerPort = int.Parse(configuration["Jaeger:Port"] ?? "6831");

        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(serviceName)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
                            ["service.version"] = "1.0.0"
                        }))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {

                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = jaegerHost;
                        options.AgentPort = jaegerPort;
                    });
            });


        ActivitySourceProvider.RegisterSource(serviceName);

        return services;
    }









    public static IServiceCollection AddAtlasTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<ResourceBuilder> configureResource)
    {
        var jaegerHost = configuration["Jaeger:Host"] ?? "jaeger";
        var jaegerPort = int.Parse(configuration["Jaeger:Port"] ?? "6831");

        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                var resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(serviceName)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
                        ["service.version"] = "1.0.0"
                    });


                configureResource?.Invoke(resourceBuilder);

                tracerProviderBuilder
                    .AddSource(serviceName)
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = jaegerHost;
                        options.AgentPort = jaegerPort;
                    });
            });

        ActivitySourceProvider.RegisterSource(serviceName);

        return services;
    }
}
