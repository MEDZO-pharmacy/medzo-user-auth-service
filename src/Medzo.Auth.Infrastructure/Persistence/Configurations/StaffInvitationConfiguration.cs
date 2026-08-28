using Medzo.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medzo.Auth.Infrastructure.Persistence.Configurations;

public class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("StaffInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StaffId).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.StaffId).IsUnique();
    }
}

