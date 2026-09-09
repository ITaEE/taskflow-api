using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(DomainRules.EmailMaxLength)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(DomainRules.EmailMaxLength)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(1_024)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        builder.Navigation(user => user.Projects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
