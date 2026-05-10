using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class ForecastIntervalConfiguration : IEntityTypeConfiguration<ForecastInterval>
{
    public void Configure(EntityTypeBuilder<ForecastInterval> builder)
    {
        _ = builder.ToTable("ForecastIntervals");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.StartTime).HasColumnType("time");
        _ = builder.Property(x => x.EndTime).HasColumnType("time");
        _ = builder.Property(x => x.PredictedKw).HasColumnType("float");
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.Forecast)
            .WithMany(x => x.Intervals)
            .HasForeignKey(x => x.ForecastId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
