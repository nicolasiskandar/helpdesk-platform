using NotificationService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public EmailController(IEmailSender emailSender, IConfiguration configuration)
    {
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request)
    {
        var serviceKey = _configuration["NOTIFICATION_SERVICE_KEY"];
        var isTrustedCaller = !string.IsNullOrEmpty(serviceKey)
            && Request.Headers["X-Notification-Service-Key"].ToString() == serviceKey;

        if (!isTrustedCaller)
        {
            return Unauthorized();
        }

        await _emailSender.SendAsync(request.ToEmail, request.Subject, request.HtmlBody);
        return NoContent();
    }

    public record SendEmailRequest(string ToEmail, string Subject, string HtmlBody);
}
