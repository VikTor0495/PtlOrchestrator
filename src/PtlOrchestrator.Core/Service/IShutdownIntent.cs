namespace PtlOrchestrator.Service;

/// <summary>
/// Dice allo spegnimento che tipo di chiusura è in corso. Una chiusura normale scollega i carrelli (dopo il report);
/// un riavvio per applicare le impostazioni li tiene collegati e la postazione li riprende alla riapertura.
/// </summary>
public interface IShutdownIntent
{
    bool KeepCartsAttached { get; set; }
}

public sealed class ShutdownIntent : IShutdownIntent
{
    public bool KeepCartsAttached { get; set; }
}
