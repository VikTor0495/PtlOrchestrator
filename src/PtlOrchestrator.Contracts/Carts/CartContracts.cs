namespace PtlOrchestrator.Contracts.Carts;

/// <summary>Richiesta di check-in: la postazione chiede al server di occupare il carrello.</summary>
public sealed record CartCheckInRequest(string StationId, Guid? SessionId = null);

/// <summary>
/// Esito positivo del check-in: il server restituisce l'endpoint del controller del carrello
/// e i cestelli censiti, così la postazione non conserva una configurazione propria dei carrelli.
/// </summary>
public sealed record CartCheckInResponse(
    string CartCode,
    string ControllerIp,
    int ControllerPort,
    IReadOnlyList<string> BasketIds);

public sealed record CartReleaseRequest(string StationId);

/// <summary>Censimento di un carrello (solo amministrazione).</summary>
public sealed record SaveCartRequest(
    string ControllerIp,
    int ControllerPort,
    IReadOnlyList<string> BasketIds,
    bool IsActive = true);

public sealed record CartDto(
    string CartCode,
    string ControllerIp,
    int ControllerPort,
    IReadOnlyList<string> BasketIds,
    bool IsActive,
    string? OccupiedByStationId);
