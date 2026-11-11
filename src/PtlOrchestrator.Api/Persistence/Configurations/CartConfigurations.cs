using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<CartEntity>
{
    public const int CodeMaxLength = 16;
    public const int IpMaxLength = 45;

    public void Configure(EntityTypeBuilder<CartEntity> builder)
    {
        builder.ToTable("carts");

        builder.HasKey(c => c.Code);
        builder.Property(c => c.Code).HasMaxLength(CodeMaxLength).ValueGeneratedNever();
        builder.Property(c => c.ControllerIp).HasMaxLength(IpMaxLength).IsRequired();
        builder.Property(c => c.ControllerPort).IsRequired();

        builder.HasMany(c => c.Baskets)
            .WithOne(b => b.Cart)
            .HasForeignKey(b => b.CartCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CartBasketConfiguration : IEntityTypeConfiguration<CartBasketEntity>
{
    public const int BasketIdMaxLength = 64;

    public void Configure(EntityTypeBuilder<CartBasketEntity> builder)
    {
        builder.ToTable("cart_baskets");

        builder.HasKey(b => new { b.CartCode, b.BasketId });
        builder.Property(b => b.CartCode).HasMaxLength(CartConfiguration.CodeMaxLength);
        builder.Property(b => b.BasketId).HasMaxLength(BasketIdMaxLength).IsRequired();

        // un cestello è una lampada fisica: non può appartenere a due carrelli
        builder.HasIndex(b => b.BasketId).IsUnique();
    }
}

public sealed class CartAssignmentConfiguration : IEntityTypeConfiguration<CartAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<CartAssignmentEntity> builder)
    {
        builder.ToTable("cart_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.CartCode).HasMaxLength(CartConfiguration.CodeMaxLength).IsRequired();
        builder.Property(a => a.StationId).HasMaxLength(StationConfiguration.IdMaxLength).IsRequired();

        builder.HasOne<CartEntity>()
            .WithMany()
            .HasForeignKey(a => a.CartCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StationEntity>()
            .WithMany()
            .HasForeignKey(a => a.StationId)
            .OnDelete(DeleteBehavior.Restrict);

        // al più un'assegnazione aperta per carrello: è il controllo di occupazione fatto dal DB
        builder.HasIndex(a => a.CartCode)
            .IsUnique()
            .HasFilter("released_at IS NULL")
            .HasDatabaseName("ux_cart_assignments_open");
    }
}
