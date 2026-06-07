using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class SolarProfileConfiguration : IEntityTypeConfiguration<SolarProfile>
{
    public void Configure(EntityTypeBuilder<SolarProfile> builder)
    {
        _ = builder.ToTable("SolarProfiles");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.PanelCapacityKw).HasColumnType("float");
        _ = builder.Property(x => x.InverterCapacityKw).HasColumnType("float");
        _ = builder.Property(x => x.TiltAngle).HasColumnType("float");
        _ = builder.Property(x => x.Azimuth).IsRequired();   // degrees (0–359)
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
