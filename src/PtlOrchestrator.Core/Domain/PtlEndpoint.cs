namespace PtlOrchestrator.Domain;

/// <summary>Indirizzo del controller PTL a cui è collegato un carrello.</summary>
public sealed record PtlEndpoint(string Ip, int Port)
{
    public override string ToString() => $"{Ip}:{Port}";
}
