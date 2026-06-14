using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class WorkloadConfiguration : IEntityTypeConfiguration<Workload>
{
    public void Configure(EntityTypeBuilder<Workload> builder)
    {
        _ = builder.ToTable("Workloads");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        _ = builder.Property(x => x.Wattage).HasColumnType("float");
        _ = builder.Property(x => x.Duration).IsRequired();   // minutes
        // Stored as its underlying int (High=1, Normal=2, Low=3); exposed as a string by the API.
        _ = builder.Property(x => x.Priority).HasConversion<int>().IsRequired();
        _ = builder.Property(x => x.IsIoTEnabled).IsRequired();
        _ = builder.Property(x => x.IoTEntityId).HasMaxLength(200);
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.User)
            .WithMany(x => x.Workloads)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
