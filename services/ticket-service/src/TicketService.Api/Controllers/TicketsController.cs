using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketService.Application.DTOs;
using TicketService.Application.Interfaces;

namespace TicketService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    /// <summary>
    /// Creates a new ticket.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var userId = GetUserIdFromClaims();
        var ticket = await _ticketService.CreateTicketAsync(request, userId);
        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
    }

    /// <summary>
    /// Gets all tickets (paginated).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _ticketService.GetTicketsAsync(page, pageSize);
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
        return Ok(ticket);
    }

    /// <summary>
    /// Gets tickets created by the current user.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserIdFromClaims();
        var result = await _ticketService.GetMyTicketsAsync(userId, page, pageSize);
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
        var ticket = await _ticketService.UpdateTicketAsync(id, request, userId);
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
        var ticket = await _ticketService.ChangeStatusAsync(id, request, userId);
        return Ok(ticket);
    }

    /// <summary>
    /// Gets assignments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/assignments")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(Guid ticketId)
    {
        var assignments = await _ticketService.GetAssignmentsAsync(ticketId);
        return Ok(assignments);
    }

    /// <summary>
    /// Assigns an agent to a ticket.
    /// </summary>
    [HttpPost("{ticketId:guid}/assignments")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignAgent(Guid ticketId, [FromBody] AssignAgentRequest request)
    {
        var userId = GetUserIdFromClaims();
        var assignment = await _ticketService.AssignAgentAsync(ticketId, request, userId);
        return CreatedAtAction(nameof(GetAssignments), new { ticketId }, assignment);
    }

    /// <summary>
    /// Unassigns an agent from a ticket.
    /// </summary>
    [HttpDelete("{ticketId:guid}/assignments/{agentUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignAgent(Guid ticketId, Guid agentUserId)
    {
        var userId = GetUserIdFromClaims();
        await _ticketService.UnassignAgentAsync(ticketId, new UnassignAgentRequest(agentUserId), userId);
        return NoContent();
    }

    /// <summary>
    /// Gets comments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid ticketId, [FromQuery] bool includeInternal = false)
    {
        var comments = await _ticketService.GetCommentsAsync(ticketId, includeInternal);
        return Ok(comments);
    }

    /// <summary>
    /// Adds a comment to a ticket.
    /// </summary>
    [HttpPost("{ticketId:guid}/comments")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddComment(Guid ticketId, [FromBody] AddCommentRequest request)
    {
        var userId = GetUserIdFromClaims();
        var comment = await _ticketService.AddCommentAsync(ticketId, request, userId);
        return CreatedAtAction(nameof(GetComments), new { ticketId }, comment);
    }

    /// <summary>
    /// Gets attachments for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid ticketId)
    {
        var attachments = await _ticketService.GetAttachmentsAsync(ticketId);
        return Ok(attachments);
    }

    /// <summary>
    /// Gets audit log for a ticket.
    /// </summary>
    [HttpGet("{ticketId:guid}/audit")]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLog(Guid ticketId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
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

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(userIdClaim);
    }
}
