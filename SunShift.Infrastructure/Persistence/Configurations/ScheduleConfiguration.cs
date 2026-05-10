using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        _ = builder.ToTable("Schedules");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.ScheduledDate).HasColumnType("date");
        _ = builder.Property(x => x.StartTime).HasColumnType("time");
        _ = builder.Property(x => x.EndTime).HasColumnType("time");
        _ = builder.Property(x => x.IsManual).IsRequired();
        _ = builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.Workload)
            .WithMany()
            .HasForeignKey(x => x.WorkloadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
