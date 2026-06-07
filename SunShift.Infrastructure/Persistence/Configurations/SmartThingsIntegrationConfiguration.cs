using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class SmartThingsIntegrationConfiguration : IEntityTypeConfiguration<SmartThingsIntegration>
{
    public void Configure(EntityTypeBuilder<SmartThingsIntegration> builder)
    {
        _ = builder.ToTable("SmartThingsIntegrations");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        _ = builder.Property(x => x.AccessTokenEncrypted).HasMaxLength(2048);
        _ = builder.Property(x => x.RefreshTokenEncrypted).HasMaxLength(2048);
        _ = builder.Property(x => x.AccessTokenExpiresAtUtc).HasColumnType("datetime2");
        _ = builder.Property(x => x.Scope).HasMaxLength(500);
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasOne(x => x.User)
            .WithOne(u => u.SmartThingsIntegration)
            .HasForeignKey<SmartThingsIntegration>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasIndex(x => x.UserId).IsUnique();
    }
}
