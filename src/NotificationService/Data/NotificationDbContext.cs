using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;




public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .HasColumnName("email")
                .HasMaxLength(255);

            entity.Property(e => e.PhoneNumber)
                .HasColumnName("phone_number")
                .HasMaxLength(20);

            entity.Property(e => e.NotificationType)
                .HasColumnName("notification_type")
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.TemplateName)
                .HasColumnName("template_name")
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Subject)
                .HasColumnName("subject")
                .HasMaxLength(500);

            entity.Property(e => e.Body)
                .HasColumnName("body")
                .IsRequired();

            entity.Property(e => e.TemplateData)
                .HasColumnName("template_data")
                .HasColumnType("jsonb");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.RetryCount)
                .HasColumnName("retry_count")
                .HasDefaultValue(0);

            entity.Property(e => e.MaxRetries)
                .HasColumnName("max_retries")
                .HasDefaultValue(3);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.SentAt)
                .HasColumnName("sent_at");

            entity.Property(e => e.ReferenceId)
                .HasColumnName("reference_id")
                .HasMaxLength(100);

            entity.Property(e => e.ReferenceType)
                .HasColumnName("reference_type")
                .HasMaxLength(50);


            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("ix_notifications_user_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_notifications_status");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("ix_notifications_created_at");

            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId })
                .HasDatabaseName("ix_notifications_reference");
        });
    }
}
