using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SearchService.Api.Services;
using Serilog;
using Serilog.Events;

const string serviceName = "helpdesk-search-service";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHttpClient<MeilisearchClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Meilisearch:Url"] ?? "http://meilisearch:7700");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddHttpClient<TicketServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["TicketService:BaseUrl"] ?? "http://ticket-service:8080");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddHostedService<TicketSearchConsumer>();
    builder.Services.AddHealthChecks()
        .AddCheck<MeilisearchHealthCheck>("meilisearch", tags: ["ready"])
        .AddRabbitMQ(
            $"amqp://{builder.Configuration["RabbitMQ:HostName"] ?? "rabbitmq"}:{builder.Configuration["RabbitMQ:Port"] ?? "5672"}",
            name: "rabbitmq", tags: ["ready"]);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
                ?? throw new InvalidOperationException("Jwt:PublicKeyPath is not configured.");
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
    builder.Services.AddAuthorization();
    builder.Services.AddCors(options => options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResultStatusCodes =
        {
            [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy] = StatusCodes.Status200OK,
            [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "{ServiceName} terminated unexpectedly", serviceName);
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
