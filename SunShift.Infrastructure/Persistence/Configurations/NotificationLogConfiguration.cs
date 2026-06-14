using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        _ = builder.ToTable("NotificationLogs");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        _ = builder.Property(x => x.SentDate).HasColumnType("datetime2");
        _ = builder.HasIndex(x => new { x.ScheduleId, x.Kind }).IsUnique();
    }
}
