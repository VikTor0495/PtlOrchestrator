using System.Net;
using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Persistence.Configurations;
using PtlOrchestrator.Api.Persistence.Entities;
using PtlOrchestrator.Contracts.Carts;

namespace PtlOrchestrator.Api.Services;

public interface ICartService
{
    Task<ServiceResult<CartCheckInResponse>> CheckInAsync(string cartCode, CartCheckInRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ReleaseAsync(string cartCode, CartReleaseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CartDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Codici dei carrelli attivi: ciò che serve alla postazione per riconoscerli alla scansione.</summary>
    Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken);

    Task<ServiceResult<CartDto>> SaveAsync(string cartCode, SaveCartRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Catalogo dei carrelli e occupazione. Il check-in è atomico: l'indice univoco parziale su
/// <c>cart_assignments</c> garantisce che due postazioni non possano occupare lo stesso carrello.
/// </summary>
public sealed class CartService(PtlDbContext dbContext, TimeProvider timeProvider) : ICartService
{
    private readonly PtlDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<ServiceResult<CartCheckInResponse>> CheckInAsync(string cartCode, CartCheckInRequest request, CancellationToken cancellationToken)
    {
        var stationId = request.StationId?.Trim() ?? string.Empty;
        if (stationId.Length == 0 || stationId.Length > StationConfiguration.IdMaxLength)
            return ServiceResult<CartCheckInResponse>.Fail(ServiceError.Validation, "Identificativo postazione non valido");

        var cart = await _dbContext.Carts
            .AsNoTracking()
            .Include(c => c.Baskets)
            .FirstOrDefaultAsync(c => c.Code == cartCode, cancellationToken);

        if (cart is null || !cart.IsActive)
            return ServiceResult<CartCheckInResponse>.Fail(ServiceError.NotFound, $"Carrello {cartCode} non configurato");

        var open = await FindOpenAssignmentAsync(cartCode, cancellationToken);
        if (open is not null)
            return CheckInAgainstOpen(open, stationId, cart);

        var now = _timeProvider.GetUtcNow();
        await TouchStationAsync(stationId, now, cancellationToken);

        _dbContext.CartAssignments.Add(new CartAssignmentEntity
        {
            Id = Guid.CreateVersion7(now),
            CartCode = cartCode,
            StationId = stationId,
            SessionId = request.SessionId,
            AssignedAt = now
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // un'altra postazione ha occupato il carrello tra la lettura e la scrittura: decide il DB
            _dbContext.ChangeTracker.Clear();
            var winner = await FindOpenAssignmentAsync(cartCode, cancellationToken);
            if (winner is null)
                throw;

            return CheckInAgainstOpen(winner, stationId, cart);
        }

        return ServiceResult<CartCheckInResponse>.Ok(ToResponse(cart));
    }

    public async Task<ServiceResult<bool>> ReleaseAsync(string cartCode, CartReleaseRequest request, CancellationToken cancellationToken)
    {
        var open = await FindOpenAssignmentAsync(cartCode, cancellationToken);

        // rilascio già avvenuto: operazione idempotente
        if (open is null)
            return ServiceResult<bool>.Ok(true);

        if (open.StationId != request.StationId)
            return ServiceResult<bool>.Fail(ServiceError.Conflict,
                $"Carrello {cartCode} occupato dalla postazione {open.StationId}: non può essere rilasciato da {request.StationId}");

        open.ReleasedAt = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken)
        => await _dbContext.Carts
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CartDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var carts = await _dbContext.Carts
            .AsNoTracking()
            .Include(c => c.Baskets)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        var open = await _dbContext.CartAssignments
            .AsNoTracking()
            .Where(a => a.ReleasedAt == null)
            .ToDictionaryAsync(a => a.CartCode, a => a.StationId, cancellationToken);

        return [.. carts.Select(c => ToDto(c, open.GetValueOrDefault(c.Code)))];
    }

    public async Task<ServiceResult<CartDto>> SaveAsync(string cartCode, SaveCartRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(cartCode, request);
        if (validation is not null)
            return ServiceResult<CartDto>.Fail(ServiceError.Validation, validation);

        var basketIds = request.BasketIds.Select(b => b.Trim()).ToList();

        var takenElsewhere = await _dbContext.CartBaskets
            .AnyAsync(b => basketIds.Contains(b.BasketId) && b.CartCode != cartCode, cancellationToken);
        if (takenElsewhere)
            return ServiceResult<CartDto>.Fail(ServiceError.Conflict, "Uno dei cestelli appartiene già a un altro carrello");

        var open = await FindOpenAssignmentAsync(cartCode, cancellationToken);
        var cart = await _dbContext.Carts.Include(c => c.Baskets).FirstOrDefaultAsync(c => c.Code == cartCode, cancellationToken);

        if (cart is not null && open is not null && (cart.ControllerIp != request.ControllerIp.Trim() || cart.ControllerPort != request.ControllerPort))
            return ServiceResult<CartDto>.Fail(ServiceError.Conflict, $"Carrello {cartCode} in uso: non si può cambiare il controller");

        if (cart is null)
        {
            cart = new CartEntity { Code = cartCode };
            _dbContext.Carts.Add(cart);
        }

        cart.ControllerIp = request.ControllerIp.Trim();
        cart.ControllerPort = request.ControllerPort;
        cart.IsActive = request.IsActive;

        _dbContext.CartBaskets.RemoveRange(cart.Baskets);
        cart.Baskets = [.. basketIds.Select(id => new CartBasketEntity { CartCode = cartCode, BasketId = id })];

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CartDto>.Ok(ToDto(cart, open?.StationId));
    }

    private async Task<CartAssignmentEntity?> FindOpenAssignmentAsync(string cartCode, CancellationToken cancellationToken)
        => await _dbContext.CartAssignments
            .FirstOrDefaultAsync(a => a.CartCode == cartCode && a.ReleasedAt == null, cancellationToken);

    private static ServiceResult<CartCheckInResponse> CheckInAgainstOpen(CartAssignmentEntity open, string stationId, CartEntity cart)
        => open.StationId == stationId
            ? ServiceResult<CartCheckInResponse>.Ok(ToResponse(cart))
            : ServiceResult<CartCheckInResponse>.Fail(ServiceError.Conflict,
                $"Carrello {cart.Code} già in uso dalla postazione {open.StationId}");

    private async Task TouchStationAsync(string stationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var station = await _dbContext.Stations.FindAsync([stationId], cancellationToken);
        if (station is null)
        {
            _dbContext.Stations.Add(new StationEntity { Id = stationId, FirstSeenAt = now, LastSeenAt = now });
            return;
        }

        station.LastSeenAt = now;
    }

    private static CartCheckInResponse ToResponse(CartEntity cart)
        => new(cart.Code, cart.ControllerIp, cart.ControllerPort, [.. cart.Baskets.Select(b => b.BasketId).OrderBy(b => b)]);

    private static CartDto ToDto(CartEntity cart, string? occupiedBy)
        => new(cart.Code, cart.ControllerIp, cart.ControllerPort, [.. cart.Baskets.Select(b => b.BasketId).OrderBy(b => b)], cart.IsActive, occupiedBy);

    private static string? Validate(string cartCode, SaveCartRequest request)
    {
        if (string.IsNullOrWhiteSpace(cartCode) || cartCode.Length > CartConfiguration.CodeMaxLength)
            return "Codice carrello non valido";

        if (!IPAddress.TryParse(request.ControllerIp?.Trim(), out _))
            return "Indirizzo IP del controller non valido";

        if (request.ControllerIp.Trim().Length > CartConfiguration.IpMaxLength)
            return "Indirizzo IP troppo lungo";

        if (request.ControllerPort is < 1 or > 65535)
            return "Porta del controller non valida";

        if (request.BasketIds is null || request.BasketIds.Count == 0)
            return "Il carrello deve avere almeno un cestello";

        var basketIds = request.BasketIds.Select(b => b?.Trim() ?? string.Empty).ToList();
        if (basketIds.Any(b => b.Length == 0 || b.Length > CartBasketConfiguration.BasketIdMaxLength))
            return "Identificativo cestello non valido";

        if (basketIds.Distinct().Count() != basketIds.Count)
            return "Cestelli duplicati nel carrello";

        return null;
    }
}
