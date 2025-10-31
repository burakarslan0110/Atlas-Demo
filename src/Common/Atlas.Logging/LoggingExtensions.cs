using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Sinks.OpenSearch;

namespace Atlas.Logging;

public static class LoggingExtensions
{
    public static ILogger ConfigureAtlasLogger(this LoggerConfiguration loggerConfig, IConfiguration configuration, string serviceName)
    {
        var openSearchUrl = configuration["OpenSearch:Url"] ?? "http://localhost:9200";
        var indexFormat = $"logs-atlas-{serviceName.ToLower()}-{{0:yyyy.MM.dd}}";

        return loggerConfig
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
            .Enrich.WithMachineName()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.OpenSearch(new OpenSearchSinkOptions(new Uri(openSearchUrl))
            {
                IndexFormat = indexFormat,
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.OSv1,
                MinimumLogEventLevel = Serilog.Events.LogEventLevel.Information
            })
            .MinimumLevel.Information()
            .CreateLogger();
    }
}
