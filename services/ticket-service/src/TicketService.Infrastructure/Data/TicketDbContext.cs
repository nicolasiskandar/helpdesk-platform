using TicketService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Data;

public class TicketDbContext : DbContext
{
    public TicketDbContext(DbContextOptions<TicketDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Priority> Priorities => Set<Priority>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<TicketAssignment> TicketAssignments => Set<TicketAssignment>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketAuditLogEntry> TicketAuditLogs => Set<TicketAuditLogEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Priority>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Status>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferenceNumber).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.ReferenceNumber).IsUnique();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Tickets)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Priority)
                .WithMany(p => p.Tickets)
                .HasForeignKey(e => e.PriorityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Status)
                .WithMany(s => s.Tickets)
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Assignments)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketAuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChangedByType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.FieldChanged).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.TicketId, e.ChangedAt });

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.AuditLog)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ProcessedAt);
        });

        SeedLookupData(modelBuilder);
    }

    private static void SeedLookupData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Hardware" },
            new Category { Id = 2, Name = "Software" },
            new Category { Id = 3, Name = "Network" },
            new Category { Id = 4, Name = "Access" },
            new Category { Id = 5, Name = "Other" }
        );

        modelBuilder.Entity<Priority>().HasData(
            new Priority { Id = 1, Name = "Low", Level = 1 },
            new Priority { Id = 2, Name = "Medium", Level = 2 },
            new Priority { Id = 3, Name = "High", Level = 3 },
            new Priority { Id = 4, Name = "Critical", Level = 4 }
        );

        modelBuilder.Entity<Status>().HasData(
            new Status { Id = 1, Name = "Open" },
            new Status { Id = 2, Name = "In Progress" },
            new Status { Id = 3, Name = "Resolved - Pending Confirmation" },
            new Status { Id = 4, Name = "Closed" },
            new Status { Id = 5, Name = "Resolved by AI" }
        );
    }
}
