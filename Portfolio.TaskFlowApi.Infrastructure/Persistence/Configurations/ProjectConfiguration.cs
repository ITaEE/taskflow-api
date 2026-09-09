using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);

        builder.Property(project => project.OwnerUserId)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasMaxLength(DomainRules.ProjectNameMaxLength)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasMaxLength(DomainRules.DescriptionMaxLength);

        builder.Property(project => project.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(project => project.Tasks)
            .WithOne(task => task.Project)
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(project => project.Owner)
            .WithMany(user => user.Projects)
            .HasForeignKey(project => project.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(project => project.Tasks)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
