using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence.Configurations;

public sealed class OperationEventConfiguration : IEntityTypeConfiguration<OperationEventEntity>
{
    public const int BarcodeMaxLength = 128;
    public const int LocationMaxLength = 64;
    public const int DetailsMaxLength = 1000;

    public void Configure(EntityTypeBuilder<OperationEventEntity> builder)
    {
        builder.ToTable("operation_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.StationId).HasMaxLength(StationConfiguration.IdMaxLength).IsRequired();

        // testo e non numero: aggiungere o riordinare valori dell'enum non corrompe i dati storici
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40).IsRequired();

        builder.Property(e => e.Barcode).HasMaxLength(BarcodeMaxLength);
        builder.Property(e => e.CartId).HasMaxLength(LocationMaxLength);
        builder.Property(e => e.BasketId).HasMaxLength(LocationMaxLength);
        builder.Property(e => e.Details).HasMaxLength(DetailsMaxLength);

        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => e.SessionId);
        builder.HasIndex(e => new { e.OperatorId, e.OccurredAt });
    }
}
