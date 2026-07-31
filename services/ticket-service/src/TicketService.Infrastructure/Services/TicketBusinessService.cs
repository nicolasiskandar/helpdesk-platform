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
    private readonly IFileStorageService _fileStorage;

    public TicketBusinessService(IUnitOfWork unitOfWork, IReferenceNumberGenerator referenceNumberGenerator, IFileStorageService fileStorage)
    {
        _unitOfWork = unitOfWork;
        _referenceNumberGenerator = referenceNumberGenerator;
        _fileStorage = fileStorage;
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

    public async Task<TicketListResponse> GetTicketsAsync(int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null, Guid? agentUserId = null)
    {
        IReadOnlyList<Ticket> tickets;
        int totalCount;

        if (agentUserId.HasValue)
        {
            tickets = await _unitOfWork.Tickets.GetByAgentUserIdAsync(agentUserId.Value, page, pageSize, createdFrom, createdTo);
            totalCount = await _unitOfWork.Tickets.GetCountByAgentUserIdAsync(agentUserId.Value, createdFrom, createdTo);
        }
        else
        {
            tickets = await _unitOfWork.Tickets.GetAllAsync(page, pageSize, createdFrom, createdTo);
            totalCount = await _unitOfWork.Tickets.GetCountAsync(createdFrom, createdTo);
        }

        var responses = tickets.Select(t => MapToResponse(t, t.Category, t.Priority, t.Status)).ToList();
        return new TicketListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<TicketListResponse> GetMyTicketsAsync(Guid userId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var tickets = await _unitOfWork.Tickets.GetByCreatedByUserIdAsync(userId, page, pageSize, createdFrom, createdTo);
        var totalCount = await _unitOfWork.Tickets.GetCountByCreatedByUserIdAsync(userId, createdFrom, createdTo);

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

        var comments = await _unitOfWork.TicketComments.GetByTicketIdAsync(id, requestedByUserId, "Admin", ticket.CreatedByUserId, new HashSet<Guid>());
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

        var allowedTransitions = new Dictionary<int, HashSet<int>>
        {
            [1] = new() { 2 },                          // Open → In Progress
            [2] = new() { 1, 3 },                       // In Progress → Open, Pending Confirmation
            [3] = new() { 2, 4 },                       // Pending Confirmation → In Progress, Closed
        };

        if (allowedTransitions.TryGetValue(ticket.StatusId, out var allowed) && !allowed.Contains(request.StatusId))
        {
            throw new InvalidOperationException($"Cannot transition from '{ticket.Status.Name}' to '{newStatus.Name}'.");
        }

        if (ticket.StatusId == 4 || ticket.StatusId == 5)
        {
            throw new InvalidOperationException($"Cannot change status from '{ticket.Status.Name}'.");
        }

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

    public async Task<AssignmentResponse> AssignAgentAsync(Guid ticketId, AssignAgentRequest request, Guid assignedByUserId, string assignedByName)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var existing = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, request.AgentUserId);
        if (existing != null)
        {
            throw new InvalidOperationException("Agent is already assigned to this ticket.");
        }

        if (ticket.Status.Name == "Open")
        {
            var inProgressStatus = await _unitOfWork.Statuses.GetByNameAsync("In Progress")
                ?? throw new InvalidOperationException("In Progress status not found.");

            var oldStatusName = ticket.Status.Name;
            ticket.StatusId = inProgressStatus.Id;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Tickets.UpdateAsync(ticket);

            var statusAudit = CreateAuditEntry(ticketId, assignedByUserId, "Status", oldStatusName, "In Progress");
            statusAudit.ChangedByType = "User";
            await _unitOfWork.TicketAuditLogs.AddAsync(statusAudit);

            var statusOutbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = "ticket.status_changed",
                Payload = JsonSerializer.Serialize(new TicketStatusChangedEvent(
                    ticketId, ticket.ReferenceNumber, oldStatusName, "In Progress",
                    assignedByUserId, "User", DateTime.UtcNow)),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Outbox.AddAsync(statusOutbox);
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

        var auditLog = CreateAuditEntry(ticketId, assignedByUserId, "Assignment", null, $"Assigned to {assignedByName}");
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

    public async Task UnassignAgentAsync(Guid ticketId, UnassignAgentRequest request, Guid changedByUserId, string changedByName)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var assignment = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, request.AgentUserId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        assignment.UnassignedAt = DateTime.UtcNow;
        await _unitOfWork.TicketAssignments.UpdateAsync(assignment);

        var allAssignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(ticketId);
        var hasRemaining = allAssignments.Any(a => a.Id != assignment.Id && a.UnassignedAt == null);

        if (!hasRemaining && ticket.Status.Name == "In Progress")
        {
            var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
                ?? throw new InvalidOperationException("Open status not found.");

            var oldStatusName = ticket.Status.Name;
            ticket.StatusId = openStatus.Id;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Tickets.UpdateAsync(ticket);

            var statusAudit = CreateAuditEntry(ticketId, changedByUserId, "Status", oldStatusName, "Open");
            statusAudit.ChangedByType = "User";
            await _unitOfWork.TicketAuditLogs.AddAsync(statusAudit);

            var statusOutbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = "ticket.status_changed",
                Payload = JsonSerializer.Serialize(new TicketStatusChangedEvent(
                    ticketId, ticket.ReferenceNumber, oldStatusName, "Open",
                    changedByUserId, "User", DateTime.UtcNow)),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Outbox.AddAsync(statusOutbox);
        }

        var auditLog = CreateAuditEntry(ticketId, changedByUserId, "Assignment", $"Assigned to {changedByName}", "Unassigned");
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.unassigned",
            Payload = JsonSerializer.Serialize(new TicketAssignedEvent(
                ticketId,
                ticket.ReferenceNumber,
                request.AgentUserId,
                changedByUserId,
                assignment.UnassignedAt.Value
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(outboxMessage);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TicketResponse> EscalateTicketAsync(Guid ticketId, Guid userId, string userName, string? reason)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var inProgressStatus = await _unitOfWork.Statuses.GetByNameAsync("In Progress")
            ?? throw new InvalidOperationException("In Progress status not found.");

        if (ticket.StatusId != inProgressStatus.Id)
            throw new InvalidOperationException("Only tickets in progress can be escalated.");

        var assignment = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, userId)
            ?? throw new InvalidOperationException("You are not assigned to this ticket.");

        assignment.UnassignedAt = DateTime.UtcNow;
        await _unitOfWork.TicketAssignments.UpdateAsync(assignment);

        var allAssignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(ticketId);
        var hasRemaining = allAssignments.Any(a => a.Id != assignment.Id && a.UnassignedAt == null);
        var mappedStatus = ticket.Status;

        if (!hasRemaining)
        {
            var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
                ?? throw new InvalidOperationException("Open status not found.");

            var oldStatusName = ticket.Status.Name;
            ticket.StatusId = openStatus.Id;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Tickets.UpdateAsync(ticket);
            mappedStatus = openStatus;

            var statusAudit = CreateAuditEntry(ticketId, userId, "Status", oldStatusName, "Open");
            await _unitOfWork.TicketAuditLogs.AddAsync(statusAudit);

            var statusOutbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = "ticket.status_changed",
                Payload = JsonSerializer.Serialize(new TicketStatusChangedEvent(
                    ticketId, ticket.ReferenceNumber, oldStatusName, "Open",
                    userId, "User", DateTime.UtcNow)),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Outbox.AddAsync(statusOutbox);
        }

        var escalationNote = string.IsNullOrWhiteSpace(reason) ? "Escalated" : $"Escalated: {reason}";
        var auditLog = CreateAuditEntry(ticketId, userId, "Assignment", $"Assigned to {userName}", escalationNote);
        await _unitOfWork.TicketAuditLogs.AddAsync(auditLog);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.unassigned",
            Payload = JsonSerializer.Serialize(new TicketAssignedEvent(
                ticketId,
                ticket.ReferenceNumber,
                userId,
                userId,
                assignment.UnassignedAt.Value
            )),
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Outbox.AddAsync(outboxMessage);

        await _unitOfWork.SaveChangesAsync();

        var updatedTicket = await _unitOfWork.Tickets.GetByIdAsync(ticketId);
        return MapToResponse(updatedTicket!, updatedTicket!.Category, updatedTicket.Priority, mappedStatus);
    }

    public async Task<AssignmentResponse> ClaimTicketAsync(Guid ticketId, Guid userId, string userName)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var openStatus = await _unitOfWork.Statuses.GetByNameAsync("Open")
            ?? throw new InvalidOperationException("Open status not found.");

        if (ticket.StatusId != openStatus.Id)
            throw new InvalidOperationException("Only open tickets can be claimed.");

        var existing = await _unitOfWork.TicketAssignments.GetActiveAssignmentAsync(ticketId, userId);
        if (existing != null)
            throw new InvalidOperationException("Ticket is already assigned.");

        var inProgressStatus = await _unitOfWork.Statuses.GetByNameAsync("In Progress")
            ?? throw new InvalidOperationException("In Progress status not found.");

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AgentUserId = userId,
            AssignedByUserId = userId,
            AssignedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketAssignments.AddAsync(assignment);

        var oldStatusName = ticket.Status.Name;
        ticket.StatusId = inProgressStatus.Id;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Tickets.UpdateAsync(ticket);

        var assignmentAudit = CreateAuditEntry(ticketId, userId, "Assignment", null, $"Assigned to {userName}");
        await _unitOfWork.TicketAuditLogs.AddAsync(assignmentAudit);

        var statusAudit = CreateAuditEntry(ticketId, userId, "Status", oldStatusName, "In Progress");
        await _unitOfWork.TicketAuditLogs.AddAsync(statusAudit);

        var assignedOutbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.assigned",
            Payload = JsonSerializer.Serialize(new TicketAssignedEvent(
                ticketId,
                ticket.ReferenceNumber,
                userId,
                userId,
                assignment.AssignedAt
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(assignedOutbox);

        var statusOutbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.status_changed",
            Payload = JsonSerializer.Serialize(new TicketStatusChangedEvent(
                ticketId,
                ticket.ReferenceNumber,
                oldStatusName,
                "In Progress",
                userId,
                "User",
                DateTime.UtcNow
            )),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Outbox.AddAsync(statusOutbox);
        await _unitOfWork.SaveChangesAsync();

        return MapAssignmentToResponse(assignment);
    }

    public async Task<TicketListResponse> GetOpenUnassignedTicketsAsync(int page, int pageSize)
    {
        var tickets = await _unitOfWork.Tickets.GetOpenUnassignedTicketsAsync(page, pageSize);
        var totalCount = await _unitOfWork.Tickets.GetOpenUnassignedTicketsCountAsync();

        var responses = tickets.Select(t => MapToResponse(t, t.Category, t.Priority, t.Status)).ToList();
        return new TicketListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<AgentWorkloadResponse>> GetAgentWorkloadAsync()
    {
        var workload = await _unitOfWork.TicketAssignments.GetAgentWorkloadAsync();
        return workload
            .Select(w => new AgentWorkloadResponse(
                w.AgentUserId,
                w.OpenCount,
                w.ResolvedCount,
                w.OpenTickets.Select(MapWorkloadTicketToResponse).ToList(),
                w.ResolvedTickets.Select(MapWorkloadTicketToResponse).ToList()
            ))
            .ToList();
    }

    public async Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(Guid ticketId, Guid viewerUserId, string viewerRole)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var assignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(ticketId);
        var assignedAgentIds = assignments
            .Where(a => a.UnassignedAt == null)
            .Select(a => a.AgentUserId)
            .ToHashSet();

        var comments = await _unitOfWork.TicketComments.GetByTicketIdAsync(
            ticketId, viewerUserId, viewerRole, ticket.CreatedByUserId, assignedAgentIds);
        return comments.Select(MapCommentToResponse).ToList();
    }

    public async Task<CommentResponse> AddCommentAsync(Guid ticketId, AddCommentRequest request, Guid authorUserId, string authorRole)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (request.IsPrivate)
        {
            var assignments = await _unitOfWork.TicketAssignments.GetByTicketIdAsync(ticketId);
            var assignedAgentIds = assignments
                .Where(a => a.UnassignedAt == null)
                .Select(a => a.AgentUserId)
                .ToHashSet();

            var isPermitted = authorRole == "Admin"
                || ticket.CreatedByUserId == authorUserId
                || assignedAgentIds.Contains(authorUserId);

            if (!isPermitted)
                throw new InvalidOperationException("You do not have permission to create private comments.");
        }

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Content = request.Content,
            IsPrivate = request.IsPrivate,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.TicketComments.AddAsync(comment);

        var auditLog = CreateAuditEntry(ticketId, authorUserId, "Comment", null, request.IsPrivate ? "Private comment added" : "Comment added");
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

    public async Task<AttachmentResponse> UploadAttachmentAsync(Guid ticketId, Stream fileStream, string fileName, Guid uploadedByUserId)
    {
        var ticket = await _unitOfWork.Tickets.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found.");

        var fileUrl = await _fileStorage.SaveFileAsync(fileStream, fileName, ticketId.ToString());

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
        var activeAssignee = ticket.Assignments?
            .Where(a => a.UnassignedAt == null)
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

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
            ticket.UpdatedAt,
            activeAssignee?.AgentUserId
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
            comment.IsPrivate,
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

    private static AgentWorkloadTicketResponse MapWorkloadTicketToResponse(AgentWorkloadTicketEntry ticket)
    {
        return new AgentWorkloadTicketResponse(
            ticket.TicketId,
            ticket.ReferenceNumber,
            ticket.Title,
            ticket.CategoryName,
            ticket.PriorityName,
            ticket.StatusName,
            ticket.CreatedAt,
            ticket.UpdatedAt
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
