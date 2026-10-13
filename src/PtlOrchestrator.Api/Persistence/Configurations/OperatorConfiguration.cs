using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence.Configurations;

public sealed class OperatorConfiguration : IEntityTypeConfiguration<OperatorEntity>
{
    public const int NameMaxLength = 100;
    public const int BadgeCodeMaxLength = 64;

    public void Configure(EntityTypeBuilder<OperatorEntity> builder)
    {
        builder.ToTable("operators");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Name).HasMaxLength(NameMaxLength).IsRequired();
        builder.Property(o => o.BadgeCode).HasMaxLength(BadgeCodeMaxLength);
        builder.Property(o => o.PinHash).HasMaxLength(200);

        // il badge identifica l'operatore alla pistola: deve essere univoco (i NULL sono ammessi più volte)
        builder.HasIndex(o => o.BadgeCode).IsUnique();
        builder.HasIndex(o => o.UpdatedAt);
    }
}
