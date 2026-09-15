using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public static readonly Guid StudentRoleId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid TrainerRoleId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid AdminRoleId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid");

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(255);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasData(
            new Role
            {
                Id = StudentRoleId,
                Code = "STUDENT",
                Name = "Student",
                Description = "Ethos student"
            },
            new Role
            {
                Id = TrainerRoleId,
                Code = "TRAINER",
                Name = "Trainer",
                Description = "Ethos trainer"
            },
            new Role
            {
                Id = AdminRoleId,
                Code = "ADMIN",
                Name = "Administrator",
                Description = "Ethos administrator"
            });
    }
}
