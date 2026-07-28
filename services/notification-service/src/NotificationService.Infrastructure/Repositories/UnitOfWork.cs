using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace NotificationService.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _context;

    public UnitOfWork(NotificationDbContext context,
        INotificationRepository notifications,
        INotificationPreferenceRepository notificationPreferences,
        IProcessedMessageRepository processedMessages)
    {
        _context = context;
        Notifications = notifications;
        NotificationPreferences = notificationPreferences;
        ProcessedMessages = processedMessages;
    }

    public INotificationRepository Notifications { get; }
    public INotificationPreferenceRepository NotificationPreferences { get; }
    public IProcessedMessageRepository ProcessedMessages { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public async Task<IReadOnlyList<Guid>> GetActiveAgentUserIdsAsync()
    {
        var identityUrl = Environment.GetEnvironmentVariable("IDENTITY_SERVICE_URL") ?? "http://identity-service:8080";
        using var http = new HttpClient { BaseAddress = new Uri(identityUrl) };
        try
        {
            var users = await http.GetFromJsonAsync<List<ExternalUser>>("/api/users?roleId=2&isActive=true&pageSize=100");
            return users?.Select(u => u.Id).ToList() ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }

    public async Task<IReadOnlyList<Guid>> GetTicketRecipientIdsAsync(Guid ticketId)
    {
        var ticketUrl = Environment.GetEnvironmentVariable("TICKET_SERVICE_URL") ?? "http://ticket-service:8080";
        using var http = new HttpClient { BaseAddress = new Uri(ticketUrl) };
        try
        {
            var ticket = await http.GetFromJsonAsync<TicketInfo>($"/api/tickets/{ticketId}");
            if (ticket == null) return new List<Guid>();

            var recipients = new List<Guid> { ticket.CreatedByUserId };
            if (ticket.AssigneeUserId.HasValue && ticket.AssigneeUserId.Value != ticket.CreatedByUserId)
                recipients.Add(ticket.AssigneeUserId.Value);

            return recipients.Distinct().ToList();
        }
        catch
        {
            return new List<Guid>();
        }
    }

    private class ExternalUser
    {
        public Guid Id { get; set; }
    }

    private class TicketInfo
    {
        public Guid CreatedByUserId { get; set; }
        public Guid? AssigneeUserId { get; set; }
    }
}
