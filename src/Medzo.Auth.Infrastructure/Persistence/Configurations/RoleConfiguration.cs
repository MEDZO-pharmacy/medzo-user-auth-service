using Medzo.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medzo.Auth.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasMaxLength(3)
            .IsFixedLength()
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(256);

        builder.HasData(
            new Role { Id = "001", Name = "Admin", Description = "System administrator" },
            new Role { Id = "002", Name = "Pharmacist", Description = "Licensed pharmacist" },
            new Role { Id = "003", Name = "InventoryManager", Description = "Inventory manager" }
        );
    }
}
