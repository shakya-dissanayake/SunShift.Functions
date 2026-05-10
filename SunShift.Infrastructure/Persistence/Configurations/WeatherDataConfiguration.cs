using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        _ = builder.ToTable("WeatherData");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.Timestamp).HasColumnType("datetime2");
        _ = builder.Property(x => x.ShortwaveRadiation).HasColumnType("float");
        _ = builder.Property(x => x.DirectRadiation).HasColumnType("float");
        _ = builder.Property(x => x.DiffuseRadiation).HasColumnType("float");
        _ = builder.Property(x => x.DirectNormalIrradiance).HasColumnType("float");
        _ = builder.Property(x => x.CloudCover).HasColumnType("float");
        _ = builder.Property(x => x.CloudCoverLow).HasColumnType("float");
        _ = builder.Property(x => x.CloudCoverMid).HasColumnType("float");
        _ = builder.Property(x => x.CloudCoverHigh).HasColumnType("float");
        _ = builder.Property(x => x.Temperature).HasColumnType("float");
        _ = builder.Property(x => x.RelativeHumidity).HasColumnType("float");
        _ = builder.Property(x => x.DewPoint).HasColumnType("float");
        _ = builder.Property(x => x.Precipitation).HasColumnType("float");
        _ = builder.Property(x => x.Rain).HasColumnType("float");
        _ = builder.Property(x => x.WindSpeed).HasColumnType("float");
        _ = builder.Property(x => x.WindDirection).HasColumnType("float");
        _ = builder.Property(x => x.SurfacePressure).HasColumnType("float");
        _ = builder.Property(x => x.SunshineDuration).HasColumnType("float");
        _ = builder.Property(x => x.IsDay).HasColumnType("bit");
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.Location)
            .WithMany(x => x.WeatherData)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        _ = builder.HasIndex(x => new { x.LocationId, x.Timestamp }).IsUnique();
    }
}
