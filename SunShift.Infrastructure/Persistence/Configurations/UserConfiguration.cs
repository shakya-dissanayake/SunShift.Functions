using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SunShift.Domain.Entities;

namespace SunShift.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        _ = builder.ToTable("Users");
        _ = builder.HasKey(x => x.Id);
        _ = builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        _ = builder.Property(x => x.UserId).HasMaxLength(128).IsRequired();
        _ = builder.Property(x => x.Email).HasMaxLength(256);
        _ = builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        _ = builder.Property(x => x.Latitude).HasColumnType("float").IsRequired();
        _ = builder.Property(x => x.Longitude).HasColumnType("float").IsRequired();
        _ = builder.Property(x => x.CreatedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.CreatedBy).HasMaxLength(100);
        _ = builder.Property(x => x.ModifiedDate).HasColumnType("datetime2");
        _ = builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        _ = builder.HasQueryFilter(x => !x.IsDeleted);
        _ = builder.HasIndex(x => x.UserId).IsUnique();
        _ = builder.HasOne(x => x.SolarProfile)
            .WithOne(x => x.User)
            .HasForeignKey<SolarProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
