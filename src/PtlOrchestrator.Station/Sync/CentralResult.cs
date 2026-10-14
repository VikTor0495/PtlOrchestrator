namespace PtlOrchestrator.Station.Sync;

public enum CentralFailure
{
    None,

    /// <summary>Rete, timeout, server non disponibile o credenziali da sistemare: si può ritentare più tardi.</summary>
    Transient,

    /// <summary>Il server ha rifiutato il contenuto in modo definitivo: ritentare non servirebbe.</summary>
    Rejected
}

public sealed record CentralResult<T>(T? Value, CentralFailure Failure, string? Message)
{
    public bool IsSuccess => Failure == CentralFailure.None;

    public static CentralResult<T> Ok(T value) => new(value, CentralFailure.None, null);

    public static CentralResult<T> Transient(string message) => new(default, CentralFailure.Transient, message);

    public static CentralResult<T> Rejected(string message) => new(default, CentralFailure.Rejected, message);
}
