using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence.Configurations;

public sealed class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSessionEntity>
{
    public void Configure(EntityTypeBuilder<WorkSessionEntity> builder)
    {
        builder.ToTable("work_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.StationId).HasMaxLength(StationConfiguration.IdMaxLength).IsRequired();

        builder.HasOne(s => s.Operator)
            .WithMany()
            .HasForeignKey(s => s.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StationEntity>()
            .WithMany()
            .HasForeignKey(s => s.StationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.StartedAt);
        builder.HasIndex(s => new { s.OperatorId, s.StartedAt });
    }
}
