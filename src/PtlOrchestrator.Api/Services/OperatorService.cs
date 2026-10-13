using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Persistence.Configurations;
using PtlOrchestrator.Api.Persistence.Entities;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Contracts.Security;

namespace PtlOrchestrator.Api.Services;

public sealed partial class OperatorService(
    PtlDbContext dbContext,
    TimeProvider timeProvider) : IOperatorService
{
    private readonly PtlDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyList<OperatorDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var operators = await _dbContext.Operators
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        return [.. operators.Select(ToDto)];
    }

    public async Task<OperatorsSnapshotResponse> GetSnapshotAsync(DateTimeOffset? since, CancellationToken cancellationToken)
    {
        // l'ora del server si legge prima della query: chi usa questo valore come "since"
        // successivo non perde le modifiche avvenute mentre la query era in corso
        var serverTime = _timeProvider.GetUtcNow();

        var query = _dbContext.Operators.AsNoTracking();

        if (since is not null)
        {
            var threshold = since.Value.ToUniversalTime();
            query = query.Where(o => o.UpdatedAt > threshold);
        }

        var operators = await query.OrderBy(o => o.Name).ToListAsync(cancellationToken);

        return new OperatorsSnapshotResponse([.. operators.Select(ToDto)], serverTime);
    }

    public async Task<ServiceResult<OperatorDto>> CreateAsync(SaveOperatorRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request, existingPinHash: null);
        if (validationError is not null)
            return ServiceResult<OperatorDto>.Fail(ServiceError.Validation, validationError);

        var badgeCode = NormalizeBadge(request.BadgeCode);

        if (await BadgeIsTakenAsync(badgeCode, exceptOperatorId: null, cancellationToken))
            return BadgeConflict();

        var now = _timeProvider.GetUtcNow();
        var entity = new OperatorEntity
        {
            Id = Guid.CreateVersion7(now),
            Name = request.Name.Trim(),
            BadgeCode = badgeCode,
            PinHash = request.Pin is null ? null : PinHasher.Hash(request.Pin),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Operators.Add(entity);

        return await SaveAsync(entity, cancellationToken);
    }

    public async Task<ServiceResult<OperatorDto>> UpdateAsync(Guid operatorId, SaveOperatorRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, cancellationToken);
        if (entity is null)
            return ServiceResult<OperatorDto>.Fail(ServiceError.NotFound, "Operatore non trovato");

        var validationError = Validate(request, entity.PinHash);
        if (validationError is not null)
            return ServiceResult<OperatorDto>.Fail(ServiceError.Validation, validationError);

        var badgeCode = NormalizeBadge(request.BadgeCode);

        if (await BadgeIsTakenAsync(badgeCode, exceptOperatorId: operatorId, cancellationToken))
            return BadgeConflict();

        entity.Name = request.Name.Trim();
        entity.BadgeCode = badgeCode;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = _timeProvider.GetUtcNow();

        if (request.Pin is not null)
            entity.PinHash = PinHasher.Hash(request.Pin);

        return await SaveAsync(entity, cancellationToken);
    }

    private async Task<ServiceResult<OperatorDto>> SaveAsync(OperatorEntity entity, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (entity.BadgeCode is not null)
        {
            // due richieste concorrenti con lo stesso badge: il controllo preventivo non basta, vince l'indice univoco
            return BadgeConflict();
        }

        return ServiceResult<OperatorDto>.Ok(ToDto(entity));
    }

    private Task<bool> BadgeIsTakenAsync(string? badgeCode, Guid? exceptOperatorId, CancellationToken cancellationToken)
    {
        if (badgeCode is null)
            return Task.FromResult(false);

        var excluded = exceptOperatorId ?? Guid.Empty;

        return _dbContext.Operators.AnyAsync(
            o => o.BadgeCode == badgeCode && o.Id != excluded,
            cancellationToken);
    }

    private static string? Validate(SaveOperatorRequest request, string? existingPinHash)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Il nome è obbligatorio";

        if (request.Name.Trim().Length > OperatorConfiguration.NameMaxLength)
            return $"Il nome non può superare {OperatorConfiguration.NameMaxLength} caratteri";

        var badgeCode = NormalizeBadge(request.BadgeCode);

        if (badgeCode is { Length: > OperatorConfiguration.BadgeCodeMaxLength })
            return $"Il badge non può superare {OperatorConfiguration.BadgeCodeMaxLength} caratteri";

        if (request.Pin is not null && !PinPattern().IsMatch(request.Pin))
            return "Il PIN deve contenere da 4 a 8 cifre";

        var hasCredential = badgeCode is not null || request.Pin is not null || existingPinHash is not null;
        if (!hasCredential)
            return "Serve almeno un badge o un PIN, altrimenti l'operatore non potrebbe accedere";

        return null;
    }

    private static string? NormalizeBadge(string? badgeCode)
        => string.IsNullOrWhiteSpace(badgeCode) ? null : badgeCode.Trim();

    private static ServiceResult<OperatorDto> BadgeConflict()
        => ServiceResult<OperatorDto>.Fail(ServiceError.Conflict, "Il badge è già assegnato a un altro operatore");

    private static OperatorDto ToDto(OperatorEntity entity)
        => new(entity.Id, entity.Name, entity.BadgeCode, entity.PinHash, entity.IsActive, entity.UpdatedAt);

    [GeneratedRegex(@"^\d{4,8}$")]
    private static partial Regex PinPattern();
}
