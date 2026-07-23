using System.Text.Json;
using TicketService.Application.DTOs;
using TicketService.Application.Events;
using TicketService.Application.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;

namespace TicketService.Infrastructure.Services;

public class TicketBusinessService : ITicketService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceNumberGenerator _referenceNumberGenerator;

    public TicketBusinessService(IUnitOfWork unitOfWork, IReferenceNumberGenerator referenceNumberGenerator)
    {
        _unitOfWork = unitOfWork;
        _referenceNumberGenerator = referenceNumberGenerator;
    }

    public async Task<TicketResponse> CreateTicketAsync(CreateTicketRequest request, Guid createdByUserId)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId)
            ?? throw new InvalidOperationException("Invalid category.");

        var priority = await _unitOfWork.Priorities.GetByIdAsync(request.PriorityId)
            ?? throw new InvalidOperationException("Invalid priority.");

        var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
            ?? throw new InvalidOperationException("Open status not found.");

        var referenceNumber = await _referenceNumberGenerator.GenerateAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = referenceNumber,
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            PriorityId = request.PriorityId,
            StatusId = openStatus.Id,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Tickets.AddAsync(ticket);

        var auditLog = new TicketAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = createdByUserId,
            ChangedByType = "User",
            FieldChanged = "Created",
            OldValue = null,
            NewValue = "Ticket created",
            ChangedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.created",
            Payload = JsonSerializer.Serialize(new TicketCreatedEvent(
                ticket.Id,
                ticket.ReferenceNumber,
                ticket.Title,
                ticket.Description,
                category.Name,
                priority.Name,
                createdByUserId,
                ticket.CreatedAt
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(ticket, category, priority, openStatus);
    }

    public async Task<TicketResponse> GetTicketByIdAsync(Guid id)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found.");

        return MapToResponse(ticket, ticket.Category, ticket.Priority, ticket.Status);
    }

    public async Task<TicketResponse> GetTicketByReferenceNumberAsync(string referenceNumber)
    {
        var ticket = await _unitOfWork.Tickets.GetByReferenceNumberAsync(referenceNumber)
            ?? throw new KeyNotFoundException("Ticket not found.");

        return MapToResponse(ticket, ticket.Category, ticket.Priority, ticket.Status);
    }

    public async Task<TicketListResponse> GetTicketsAsync(int page, int pageSize)
    {
        var tickets = await _unitOfWork.Tickets.GetAllAsync(page, pageSize);
        var totalCount = await _unitOfWork.Tickets.GetCountAsync();

        var responses = tickets.Select(t => MapToResponse(t, t.Category, t.Priority, t.Status)).ToList();
        return new TicketListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<TicketListResponse> GetMyTicketsAsync(Guid userId, int page, int pageSize)
    {
        var tickets = await _unitOfWork.Tickets.GetByCreatedByUserIdAsync(userId, page, pageSize);
        var totalCount = await _unitOfWork.Tickets.GetCountByCreatedByUserIdAsync(userId);

        var responses = tickets.Select(t => MapToResponse(t, t.Category, t.Priority, t.Status)).ToList();
        return new TicketListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<TicketResponse> UpdateTicketAsync(Guid id, UpdateTicketRequest request, Guid changedByUserId, string requestedByRole)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
            ?? throw new InvalidOperationException("Open status not found.");

        if (ticket.StatusId != openStatus.Id)
            throw new InvalidOperationException("Only open tickets can be edited.");

        if (requestedByRole != "Admin" && ticket.CreatedByUserId != changedByUserId)
            throw new UnauthorizedAccessException("Only the ticket creator or an admin can edit this ticket.");

        var changes = new List<TicketAuditLogEntry>();

        if (request.Title is not null && request.Title != ticket.Title)
        {
            changes.Add(CreateAuditEntry(ticket.Id, changedByUserId, "Title", ticket.Title, request.Title));
            ticket.Title = request.Title;
        }

        if (request.Description is not null && request.Description != ticket.Description)
        {
            changes.Add(CreateAuditEntry(ticket.Id, changedByUserId, "Description", ticket.Description, request.Description));
            ticket.Description = request.Description;
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != ticket.CategoryId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId.Value)
                ?? throw new InvalidOperationException("Invalid category.");
            changes.Add(CreateAuditEntry(ticket.Id, changedByUserId, "Category", ticket.CategoryId.ToString(), request.CategoryId.Value.ToString()));
            ticket.CategoryId = request.CategoryId.Value;
        }

        if (request.PriorityId.HasValue && request.PriorityId.Value != ticket.PriorityId)
        {
            var priority = await _unitOfWork.Priorities.GetByIdAsync(request.PriorityId.Value)
                ?? throw new InvalidOperationException("Invalid priority.");
            changes.Add(CreateAuditEntry(ticket.Id, changedByUserId, "Priority", ticket.PriorityId.ToString(), request.PriorityId.Value.ToString()));
            ticket.PriorityId = request.PriorityId.Value;
        }

        if (changes.Count == 0)
        {
            return MapToResponse(ticket, ticket.Category, ticket.Priority, ticket.Status);
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Tickets.UpdateAsync(ticket);

        foreach (var change in changes)
        {
            await _unitOfWork.TicketAuditLogs.AddAsync(change);
        }

        await _unitOfWork.SaveChangesAsync();

        var updatedTicket = await _unitOfWork.Tickets.GetByIdAsync(id);
        return MapToResponse(updatedTicket!, updatedTicket!.Category, updatedTicket.Priority, updatedTicket.Status);
    }

    public async Task DeleteTicketAsync(Guid id, Guid requestedByUserId, string requestedByRole)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
            ?? throw new InvalidOperationException("Open status not found.");

        if (ticket.StatusId != openStatus.Id)
            throw new InvalidOperationException("Only open tickets can be deleted.");

        if (requestedByRole != "Admin" && ticket.CreatedByUserId != requestedByUserId)
            throw new UnauthorizedAccessException("Only the ticket creator or an admin can delete this ticket.");

        var comments = await _unitOfWork.TicketComments.GetByTicketIdAsync(id, true);
        foreach (var comment in comments)
            await _unitOfWork.TicketComments.DeleteAsync(comment);

        var attachments = await _unitOfWork.TicketAttachments.GetByTicketIdAsync(id);
        foreach (var attachment in attachments)
            await _unitOfWork.TicketAttachments.DeleteAsync(attachment);

        var auditLogs = await _unitOfWork.TicketAuditLogs.GetByTicketIdAsync(id, 1, 1000);
        foreach (var auditLog in auditLogs)
            await _unitOfWork.TicketAuditLogs.DeleteAsync(auditLog);

        var assignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(id);
        foreach (var assignment in assignments)
            await _unitOfWork.TicketAssignments.DeleteAsync(assignment);

        await _unitOfWork.Tickets.DeleteAsync(ticket);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TicketResponse> ChangeStatusAsync(Guid id, ChangeStatusRequest request, Guid changedByUserId, string changedByType = "User")
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var newStatus = await _unitOfWork.Statuses.GetByIdAsync(request.StatusId)
            ?? throw new InvalidOperationException("Invalid status.");

        var oldStatusName = ticket.Status.Name;

        ticket.StatusId = request.StatusId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Tickets.UpdateAsync(ticket);

        var auditLog = CreateAuditEntry(ticket.Id, changedByUserId, "Status", oldStatusName, newStatus.Name);
        auditLog.ChangedByType = changedByType;
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.status_changed",
            Payload = JsonSerializer.Serialize(new TicketStatusChangedEvent(
                ticket.Id,
                ticket.ReferenceNumber,
                oldStatusName,
                newStatus.Name,
                changedByUserId,
                changedByType,
                DateTime.UtcNow
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(ticket, ticket.Category, ticket.Priority, newStatus);
    }

    public async Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(Guid ticketId)
    {
        var assignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(ticketId);
        return assignments.Select(MapAssignmentToResponse).ToList();
    }

    public async Task<AssignmentResponse> AssignAgentAsync(Guid ticketId, AssignAgentRequest request, Guid assignedByUserId)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var existing = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, request.AgentUserId);
        if (existing != null)
        {
            throw new InvalidOperationException("Agent is already assigned to this ticket.");
        }

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AgentUserId = request.AgentUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketAssignments.AddAsync(assignment);

        var auditLog = CreateAuditEntry(ticketId, assignedByUserId, "Assignment", null, $"Assigned agent {request.AgentUserId}");
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.assigned",
            Payload = JsonSerializer.Serialize(new TicketAssignedEvent(
                ticketId,
                ticket.ReferenceNumber,
                request.AgentUserId,
                assignedByUserId,
                assignment.AssignedAt
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();

        return MapAssignmentToResponse(assignment);
    }

    public async Task UnassignAgentAsync(Guid ticketId, UnassignAgentRequest request, Guid changedByUserId)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var assignment = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, request.AgentUserId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        assignment.UnassignedAt = DateTime.UtcNow;
        await _unitOfWork.TicketAssignments.UpdateAsync(assignment);

        var auditLog = CreateAuditEntry(ticketId, changedByUserId, "Assignment", $"Assigned agent {request.AgentUserId}", "Unassigned");
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(Guid ticketId, bool includeInternal)
    {
        var comments = await _unitOfWork.TicketComments.GetByTicketIdAsync(ticketId, includeInternal);
        return comments.Select(MapCommentToResponse).ToList();
    }

    public async Task<CommentResponse> AddCommentAsync(Guid ticketId, AddCommentRequest request, Guid authorUserId)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Content = request.Content,
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketComments.AddAsync(comment);

        var auditLog = CreateAuditEntry(ticketId, authorUserId, "Comment", null, request.IsInternal ? "Internal comment added" : "Comment added");
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        await _unitOfWork.SaveChangesAsync();

        return MapCommentToResponse(comment);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> GetAttachmentsAsync(Guid ticketId)
    {
        var attachments = await _unitOfWork.TicketAttachments.GetByTicketIdAsync(ticketId);
        return attachments.Select(MapAttachmentToResponse).ToList();
    }

    public async Task<AttachmentResponse> AddAttachmentAsync(Guid ticketId, string fileName, string fileUrl, Guid uploadedByUserId)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            FileName = fileName,
            FileUrl = fileUrl,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketAttachments.AddAsync(attachment);
        await _unitOfWork.SaveChangesAsync();

        return MapAttachmentToResponse(attachment);
    }

    public async Task<AuditLogListResponse> GetAuditLogAsync(Guid ticketId, int page, int pageSize)
    {
        var entries = await _unitOfWork.TicketAuditLogs.GetByTicketIdAsync(ticketId, page, pageSize);
        var totalCount = await _unitOfWork.TicketAuditLogs.GetCountByTicketIdAsync(ticketId);

        var responses = entries.Select(MapAuditLogToResponse).ToList();
        return new AuditLogListResponse(responses, totalCount);
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        return categories.Select(c => new CategoryResponse(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<PriorityResponse>> GetPrioritiesAsync()
    {
        var priorities = await _unitOfWork.Priorities.GetAllAsync();
        return priorities.Select(p => new PriorityResponse(p.Id, p.Name, p.Level)).ToList();
    }

    public async Task<IReadOnlyList<StatusResponse>> GetStatusesAsync()
    {
        var statuses = await _unitOfWork.Statuses.GetAllAsync();
        return statuses.Select(s => new StatusResponse(s.Id, s.Name)).ToList();
    }

    private static TicketResponse MapToResponse(Ticket ticket, Category category, Priority priority, Status status)
    {
        return new TicketResponse(
            ticket.Id,
            ticket.ReferenceNumber,
            ticket.Title,
            ticket.Description,
            category.Name,
            priority.Name,
            status.Name,
            ticket.CreatedByUserId,
            ticket.CreatedAt,
            ticket.UpdatedAt
        );
    }

    private static AssignmentResponse MapAssignmentToResponse(TicketAssignment assignment)
    {
        return new AssignmentResponse(
            assignment.Id,
            assignment.AgentUserId,
            assignment.AssignedByUserId,
            assignment.AssignedAt,
            assignment.UnassignedAt
        );
    }

    private static CommentResponse MapCommentToResponse(TicketComment comment)
    {
        return new CommentResponse(
            comment.Id,
            comment.AuthorUserId,
            comment.Content,
            comment.IsInternal,
            comment.CreatedAt
        );
    }

    private static AttachmentResponse MapAttachmentToResponse(TicketAttachment attachment)
    {
        return new AttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.FileUrl,
            attachment.UploadedByUserId,
            attachment.UploadedAt
        );
    }

    private static AuditLogEntryResponse MapAuditLogToResponse(TicketAuditLogEntry entry)
    {
        return new AuditLogEntryResponse(
            entry.Id,
            entry.ChangedByUserId,
            entry.ChangedByType,
            entry.FieldChanged,
            entry.OldValue,
            entry.NewValue,
            entry.ChangedAt
        );
    }

    private static TicketAuditLogEntry CreateAuditEntry(Guid ticketId, Guid changedByUserId, string fieldChanged, string? oldValue, string? newValue)
    {
        return new TicketAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedByUserId = changedByUserId,
            ChangedByType = "User",
            FieldChanged = fieldChanged,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTime.UtcNow
        };
    }
}
