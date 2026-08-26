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
            .ValueGeneratedOnAdd();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(256);

        // Seed default roles
        builder.HasData(
            new Role { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Admin", Description = "System administrator" },
            new Role { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Pharmacist", Description = "Licensed pharmacist" },
            new Role { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "InventoryManager", Description = "Inventory manager" },
            new Role { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "User", Description = "Regular user" }
        );
    }
}
