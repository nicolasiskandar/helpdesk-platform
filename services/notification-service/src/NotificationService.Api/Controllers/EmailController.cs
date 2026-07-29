using NotificationService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailSender _emailSender;

    public EmailController(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request)
    {
        await _emailSender.SendAsync(request.ToEmail, request.Subject, request.HtmlBody);
        return NoContent();
    }

    public record SendEmailRequest(string ToEmail, string Subject, string HtmlBody);
}
