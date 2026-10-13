using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Tests;

public class PersistenceModelTests
{
    private static PtlDbContext CreatePostgresContext()
        => new(new DbContextOptionsBuilder<PtlDbContext>()
            .UseNpgsql("Host=non-usato;Database=non-usato")
            .Options);

    [Fact]
    public void Migrations_CoverTheWholeModel()
    {
        using var context = CreatePostgresContext();

        // se fallisce, una modifica al modello non ha la sua migrazione: dotnet ef migrations add <Nome>
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(typeof(OperatorEntity), "operators")]
    [InlineData(typeof(StationEntity), "stations")]
    [InlineData(typeof(WorkSessionEntity), "work_sessions")]
    [InlineData(typeof(OperationEventEntity), "operation_events")]
    public void Tables_UseSnakeCaseNames(Type entityType, string expectedTable)
    {
        using var context = CreatePostgresContext();

        Assert.Equal(expectedTable, context.Model.FindEntityType(entityType)!.GetTableName());
    }

    [Fact]
    public void Columns_UseSnakeCaseNames()
    {
        using var context = CreatePostgresContext();
        var entity = context.Model.FindEntityType(typeof(OperationEventEntity))!;
        var table = StoreObjectIdentifier.Table("operation_events");

        Assert.Equal("station_id", entity.FindProperty(nameof(OperationEventEntity.StationId))!.GetColumnName(table));
        Assert.Equal("occurred_at", entity.FindProperty(nameof(OperationEventEntity.OccurredAt))!.GetColumnName(table));
        Assert.Equal("id", entity.FindProperty(nameof(OperationEventEntity.Id))!.GetColumnName(table));
    }

    [Theory]
    [InlineData("Id", "id")]
    [InlineData("StationId", "station_id")]
    [InlineData("OccurredAt", "occurred_at")]
    [InlineData("IsActive", "is_active")]
    [InlineData("PinHash", "pin_hash")]
    [InlineData("ix_already_snake", "ix_already_snake")]
    [InlineData("PK_Operators", "pk_operators")]
    [InlineData("FK_WorkSessions_Operators_OperatorId", "fk_work_sessions_operators_operator_id")]
    public void ToSnakeCase_ConvertsPascalCaseNames(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }
}
