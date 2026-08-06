using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketService.Application.DTOs;
using TicketService.Application.Interfaces;
using TicketService.Infrastructure.Services;

namespace TicketService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ITicketService ticketService, IFileStorageService fileStorage, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new ticket.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Employee")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var userId = GetUserIdFromClaims();
        var ticket = await _ticketService.CreateTicketAsync(request, userId);
        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
    }

    /// <summary>
    /// Gets all tickets (paginated). For agents, returns only open unassigned, assigned, and closed tickets.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var role = GetUserRoleFromClaims();
        Guid? agentUserId = null;

        if (role == "IT Support Agent" || role == "Agent")
        {
            agentUserId = GetUserIdFromClaims();
        }

        var result = await _ticketService.GetTicketsAsync(page, pageSize, createdFrom, createdTo, agentUserId);
        return Ok(result);
    }

    /// <summary>
    /// Gets a ticket by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketById(Guid id)
    {
        await EnsureTicketAccessAsync(id);
        var ticket = await _ticketService.GetTicketByIdAsync(id);
        return Ok(ticket);
    }

    /// <summary>
    /// Gets a ticket by reference number.
    /// </summary>
    [HttpGet("ref/{referenceNumber}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketByReferenceNumber(string referenceNumber)
    {
        var ticket = await _ticketService.GetTicketByReferenceNumberAsync(referenceNumber);
        await EnsureTicketAccessAsync(ticket.Id);
        return Ok(ticket);
    }

    /// <summary>
    /// Gets tickets created by the current user.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var userId = GetUserIdFromClaims();
        var result = await _ticketService.GetMyTicketsAsync(userId, page, pageSize, createdFrom, createdTo);
        return Ok(result);
    }

    /// <summary>
    /// Gets open, unassigned tickets available for pickup.
    /// </summary>
    [HttpGet("open-unassigned")]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenUnassignedTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _ticketService.GetOpenUnassignedTicketsAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Updates a ticket.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateTicketRequest request)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await EnsureTicketAccessAsync(id, userId, role);
        var ticket = await _ticketService.UpdateTicketAsync(id, request, userId, role);
        return Ok(ticket);
    }

    /// <summary>
    /// Changes the status of a ticket.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await EnsureTicketAccessAsync(id, userId, role);
        var ticket = await _ticketService.ChangeStatusAsync(id, request, userId);
        return Ok(ticket);
    }

    /// <summary>
    /// Deletes a ticket. Only the ticket creator or an admin can delete an open ticket.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await EnsureTicketAccessAsync(id, userId, role);
        await _ticketService.DeleteTicketAsync(id, userId, role);
        return NoContent();
    }

    /// <summary>
    /// Gets assignments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/assignments")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(Guid ticketId)
    {
        await EnsureTicketAccessAsync(ticketId);
        var assignments = await _ticketService.GetAssignmentsAsync(ticketId);
        return Ok(assignments);
    }

    /// <summary>
    /// Assigns an agent to a ticket.
    /// </summary>
    [HttpPost("{ticketId:guid}/assignments")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignAgent(Guid ticketId, [FromBody] AssignAgentRequest request)
    {
        var userId = GetUserIdFromClaims();
        var userName = GetUserNameFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, GetUserRoleFromClaims());
        var assignment = await _ticketService.AssignAgentAsync(ticketId, request, userId, userName);
        return CreatedAtAction(nameof(GetAssignments), new { ticketId }, assignment);
    }

    /// <summary>
    /// Unassigns an agent from a ticket.
    /// </summary>
    [HttpDelete("{ticketId:guid}/assignments/{agentUserId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignAgent(Guid ticketId, Guid agentUserId)
    {
        var userId = GetUserIdFromClaims();
        var userName = GetUserNameFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, GetUserRoleFromClaims());
        await _ticketService.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), userId, userName);
        return NoContent();
    }

    /// <summary>
    /// Claims an open ticket (self-assign and set status to In Progress).
    /// </summary>
    [HttpPost("{ticketId:guid}/claim")]
    [Authorize(Roles = "IT Support Agent")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClaimTicket(Guid ticketId)
    {
        var userId = GetUserIdFromClaims();
        var userName = GetUserNameFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, GetUserRoleFromClaims());
        var assignment = await _ticketService.ClaimTicketAsync(ticketId, userId, userName);
        return CreatedAtAction(nameof(GetAssignments), new { ticketId }, assignment);
    }

    /// <summary>
    /// Returns an in-progress ticket to the open queue when the assigned agent cannot resolve it.
    /// </summary>
    [HttpPost("{ticketId:guid}/escalate")]
    [Authorize(Roles = "IT Support Agent")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateTicket(Guid ticketId, [FromBody] EscalateTicketRequest request)
    {
        var userId = GetUserIdFromClaims();
        var userName = GetUserNameFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, GetUserRoleFromClaims());
        var ticket = await _ticketService.EscalateTicketAsync(ticketId, userId, userName, request.Reason);
        return Ok(ticket);
    }

    /// <summary>
    /// Gets comments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid ticketId)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, role);
        var comments = await _ticketService.GetCommentsAsync(ticketId, userId, role);
        return Ok(comments);
    }

    /// <summary>
    /// Adds a comment to a ticket (multipart/form-data). Optional files are attached to the comment.
    /// </summary>
    [HttpPost("{ticketId:guid}/comments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddComment(Guid ticketId, [FromForm] AddCommentForm request, [FromForm] List<IFormFile>? files)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        var userName = GetUserNameFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, role);

        IReadOnlyList<Guid>? recipientUserIds = null;
        if (!string.IsNullOrWhiteSpace(request.RecipientUserIds))
        {
            recipientUserIds = JsonSerializer.Deserialize<List<Guid>>(request.RecipientUserIds);
        }

        var dto = new AddCommentRequest(request.Content, request.IsPrivate, request.ParentCommentId, recipientUserIds);

        IReadOnlyList<CommentFileUpload>? uploads = files is { Count: > 0 }
            ? files.Select(f => new CommentFileUpload(f.FileName, f.OpenReadStream(), f.Length)).ToList()
            : null;

        var comment = await _ticketService.AddCommentAsync(ticketId, dto, userId, role, userName, uploads);
        return CreatedAtAction(nameof(GetComments), new { ticketId }, comment);
    }

    /// <summary>
    /// Downloads a comment attachment.
    /// </summary>
    [HttpGet("{ticketId:guid}/comments/{commentId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCommentAttachment(Guid ticketId, Guid commentId, Guid attachmentId)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, role);

        var comments = await _ticketService.GetCommentsAsync(ticketId, userId, role);
        var attachment = comments
            .Where(c => c.Id == commentId)
            .SelectMany(c => c.Attachments ?? Array.Empty<CommentAttachmentResponse>())
            .FirstOrDefault(a => a.Id == attachmentId);

        if (attachment is null)
            return NotFound(new ErrorResponse("Attachment not found.", null));

        var stream = _fileStorage.OpenFileAsync(attachment.FileUrl);
        var contentType = GetContentType(attachment.FileName);
        return File(stream, contentType, attachment.FileName);
    }

    /// <summary>
    /// Deletes a comment attachment. Allowed for the comment author, the ticket creator, or an admin.
    /// </summary>
    [HttpDelete("{ticketId:guid}/comments/{commentId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCommentAttachment(Guid ticketId, Guid commentId, Guid attachmentId)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await _ticketService.DeleteCommentAttachmentAsync(ticketId, commentId, attachmentId, userId, role);
        return NoContent();
    }

    /// <summary>
    /// Gets attachments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid ticketId)
    {
        await EnsureTicketAccessAsync(ticketId);
        var attachments = await _ticketService.GetAttachmentsAsync(ticketId);
        return Ok(attachments);
    }

    /// <summary>
    /// Uploads a file attachment to a ticket.
    /// </summary>
    [HttpPost("{ticketId:guid}/attachments")]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(Guid ticketId, IFormFile file)
    {
        _logger.LogInformation("UploadAttachment called for ticket {TicketId}, file: {FileName}, size: {FileSize}", ticketId, file.FileName, file.Length);

        if (file.Length == 0)
            return BadRequest(new ErrorResponse("File is empty.", null));

        var userId = GetUserIdFromClaims();
        await EnsureTicketAccessAsync(ticketId, userId, GetUserRoleFromClaims());
        var attachment = await _ticketService.UploadAttachmentAsync(ticketId, file.OpenReadStream(), file.FileName, userId);

        _logger.LogInformation("UploadAttachment completed for ticket {TicketId}, attachmentId: {AttachmentId}", ticketId, attachment.Id);

        return CreatedAtAction(nameof(GetAttachments), new { ticketId }, attachment);
    }

    /// <summary>
    /// Deletes an attachment. Only the uploader, the ticket creator, or an admin can delete.
    /// </summary>
    [HttpDelete("{ticketId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid ticketId, Guid attachmentId)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await _ticketService.DeleteAttachmentAsync(ticketId, attachmentId, userId, role);
        return NoContent();
    }

    /// <summary>
    /// Downloads an attachment file.
    /// </summary>
    [HttpGet("{ticketId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid ticketId, Guid attachmentId)
    {
        await EnsureTicketAccessAsync(ticketId);
        var attachments = await _ticketService.GetAttachmentsAsync(ticketId);
        var attachment = attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            return NotFound(new ErrorResponse("Attachment not found.", null));

        var stream = _fileStorage.OpenFileAsync(attachment.FileUrl);
        var contentType = GetContentType(attachment.FileName);
        return File(stream, contentType, attachment.FileName);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Gets audit log for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/audit")]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLog(Guid ticketId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        await EnsureTicketAccessAsync(ticketId);
        var result = await _ticketService.GetAuditLogAsync(ticketId, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Gets all categories.
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _ticketService.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Gets all priorities.
    /// </summary>
    [HttpGet("priorities")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PriorityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriorities()
    {
        var priorities = await _ticketService.GetPrioritiesAsync();
        return Ok(priorities);
    }

    /// <summary>
    /// Gets all statuses.
    /// </summary>
    [HttpGet("statuses")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<StatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatuses()
    {
        var statuses = await _ticketService.GetStatusesAsync();
        return Ok(statuses);
    }

    /// <summary>
    /// Gets agent workload (open and resolved ticket counts per agent).
    /// </summary>
    [HttpGet("agent-workload")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentWorkloadResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgentWorkload()
    {
        var workload = await _ticketService.GetAgentWorkloadAsync();
        return Ok(workload);
    }

    /// <summary>
    /// Gets statistics for the last 6 months (volume trend, resolution time, SLA compliance).
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(AnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics()
    {
        var statistics = await _ticketService.GetStatisticsAsync();
        return Ok(statistics);
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(userIdClaim);
    }

    private async Task EnsureTicketAccessAsync(Guid ticketId)
    {
        var userId = GetUserIdFromClaims();
        var role = GetUserRoleFromClaims();
        await _ticketService.EnsureTicketAccessAsync(ticketId, userId, role);
    }

    private async Task EnsureTicketAccessAsync(Guid ticketId, Guid userId, string role)
    {
        await _ticketService.EnsureTicketAccessAsync(ticketId, userId, role);
    }

    private string GetUserRoleFromClaims()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
    }

    private string GetUserNameFromClaims()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }
}

public class AddCommentForm
{
    public string Content { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string? RecipientUserIds { get; set; }
}
