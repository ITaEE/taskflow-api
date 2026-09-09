using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");
        builder.HasKey(task => task.Id);

        builder.Property(task => task.Title)
            .HasMaxLength(DomainRules.TaskTitleMaxLength)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(DomainRules.DescriptionMaxLength);

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(task => task.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(task => task.ProjectId);
    }
}
