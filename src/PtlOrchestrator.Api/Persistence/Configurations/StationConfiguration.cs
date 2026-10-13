using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence.Configurations;

public sealed class StationConfiguration : IEntityTypeConfiguration<StationEntity>
{
    public const int IdMaxLength = 64;

    public void Configure(EntityTypeBuilder<StationEntity> builder)
    {
        builder.ToTable("stations");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasMaxLength(IdMaxLength).ValueGeneratedNever();
    }
}
