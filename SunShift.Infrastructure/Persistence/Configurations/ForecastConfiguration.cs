using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class ForecastConfiguration : IEntityTypeConfiguration<Forecast>
{
    public void Configure(EntityTypeBuilder<Forecast> builder)
    {
        _ = builder.ToTable("Forecasts");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.TargetDate).HasColumnType("date");
        _ = builder.Property(x => x.TotalPredictedKwh).HasColumnType("float");
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.User)
            .WithMany(x => x.Forecasts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasMany(x => x.Intervals)
            .WithOne(x => x.Forecast)
            .HasForeignKey(x => x.ForecastId)
            .OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasIndex(x => new { x.UserId, x.TargetDate }).IsUnique();
    }
}
