using System.Diagnostics;
using Gateway.Middleware;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

var serviceName = "helpdesk-gateway";
var serviceVersion = "1.0.0";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.WithProperty("ServiceName", serviceName)
    .Enrich.WithProperty("ServiceVersion", serviceVersion)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting {ServiceName} v{Version}", serviceName, serviceVersion);

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Yarp.ReverseProxy")
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Yarp.ReverseProxy")
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

    builder.Services.AddHealthChecks();

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms<Gateway.Middleware.TraceContextTransform>();

    var app = builder.Build();

    app.UseMiddleware<RequestLoggingMiddleware>();
    app.MapHealthChecks("/health");
    app.MapReverseProxy();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{ServiceName} terminated unexpectedly", serviceName);
}
finally
{
    Log.CloseAndFlush();
}
