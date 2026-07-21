using System.Net;
using System.Text.Json;
using TicketService.Application.DTOs;

namespace TicketService.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            FluentValidation.ValidationException ex =>
                (HttpStatusCode.BadRequest, "Validation failed",
                    ex.Errors.ToDictionary(e => e.PropertyName, e => new[] { e.ErrorMessage })),

            InvalidOperationException ex =>
                (HttpStatusCode.BadRequest, ex.Message, (IDictionary<string, string[]>?)null),

            UnauthorizedAccessException ex =>
                (HttpStatusCode.Unauthorized, ex.Message, (IDictionary<string, string[]>?)null),

            KeyNotFoundException ex =>
                (HttpStatusCode.NotFound, ex.Message, (IDictionary<string, string[]>?)null),

            _ =>
                (HttpStatusCode.InternalServerError, "An unexpected error occurred.",
                    (IDictionary<string, string[]>?)null)
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse(message, errors);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
