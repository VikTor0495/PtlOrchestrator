using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>Un componente collegato alla postazione (server centrale o carrello) con il suo stato.</summary>
public sealed class LinkStatus
{
    public LinkStatus(string name, string detail, bool isConnected, Func<Task> reconnect, Func<Task>? remove = null)
    {
        Name = name;
        Detail = detail;
        IsConnected = isConnected;
        ReconnectCommand = new AsyncRelayCommand(reconnect);
        RemoveCommand = new AsyncRelayCommand(remove ?? (() => Task.CompletedTask));
        CanRemove = remove is not null;
    }

    public string Name { get; }

    public string Detail { get; }

    public bool IsConnected { get; }

    public string StatusText => IsConnected ? "Connesso" : "Non connesso";

    /// <summary>Riprova il collegamento: la GUI lo mostra solo quando il componente non è connesso.</summary>
    public IAsyncRelayCommand ReconnectCommand { get; }

    /// <summary>Solo per i carrelli: lo toglie dal turno e lo libera sul server (possibile se è vuoto).</summary>
    public IAsyncRelayCommand RemoveCommand { get; }

    public bool CanRemove { get; }
}

/// <summary>
/// Elenco dei collegamenti della postazione: server centrale e un riquadro per ogni carrello attivo.
/// Per ciascuno si vede se è connesso e, se non lo è, si può riprovare il collegamento.
/// </summary>
public sealed partial class DiagnosticsViewModel(
    ICartAssignmentClient serverClient,
    ICartManager cartManager,
    ILightstepConnectionService lightstep,
    CartContainer cartContainer,
    IUiDispatcher dispatcher,
    ILogger<DiagnosticsViewModel> logger) : ObservableObject
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly ICartAssignmentClient _serverClient = serverClient;
    private readonly ICartManager _cartManager = cartManager;
    private readonly ILightstepConnectionService _lightstep = lightstep;
    private readonly CartContainer _cartContainer = cartContainer;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly ILogger<DiagnosticsViewModel> _logger = logger;

    public ObservableCollection<LinkStatus> Links { get; } = [];

    [ObservableProperty]
    private string _message = string.Empty;

    private int _refreshing;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // con il server spento il controllo dura qualche secondo: gli aggiornamenti del timer non devono sovrapporsi
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;

        try
        {
            await RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var serverReachable = await ProbeServerAsync(cancellationToken);

        var links = new List<LinkStatus>
        {
            new("Server centrale", serverReachable ? "raggiungibile" : "non raggiungibile", serverReachable, ReconnectServerAsync)
        };

        foreach (var cart in _cartContainer.GetCarts())
        {
            var cartId = cart.CartId;
            var endpoint = cart.Endpoint;
            var connected = endpoint is null ? _lightstep.IsConnected : _lightstep.IsConnectedTo(endpoint);

            links.Add(new(
                $"Carrello {cartId}",
                endpoint?.ToString() ?? "controller predefinito",
                connected,
                () => ReconnectCartAsync(cartId, endpoint),
                () => RemoveCartAsync(cartId)));
        }

        _dispatcher.Post(() =>
        {
            Links.Clear();
            foreach (var link in links)
                Links.Add(link);
        });
    }

    private async Task<bool> ProbeServerAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            await _serverClient.RefreshCatalogAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ReconnectServerAsync()
    {
        var reachable = await ProbeServerAsync(CancellationToken.None);

        SetMessage(reachable ? "Server centrale raggiungibile" : "Il server centrale non risponde: controlla la rete e l'indirizzo nelle Impostazioni");
        await RefreshAsync(CancellationToken.None);
    }

    private async Task ReconnectCartAsync(string cartId, PtlEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            SetMessage($"Il carrello {cartId} usa il controller predefinito: riavvia la postazione per riconnetterlo");
            return;
        }

        try
        {
            // la DLL del PTL blocca il thread che la chiama: si lavora fuori dal thread della finestra
            var connected = await Task.Run(async () =>
            {
                _lightstep.Disconnect(endpoint);
                return await _lightstep.TryConnectAsync(endpoint, CancellationToken.None);
            });

            SetMessage(connected
                ? $"Carrello {cartId} riconnesso"
                : $"Il carrello {cartId} non risponde su {endpoint}: controlla che sia acceso e collegato alla rete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Riconnessione del carrello {CartId} non riuscita", cartId);
            SetMessage($"Riconnessione del carrello {cartId} non riuscita: {ex.Message}");
        }

        await RefreshAsync(CancellationToken.None);
    }

    /// <summary>Scollega il carrello dal turno (come la ✕ nella schermata di lavoro): possibile solo se è vuoto.</summary>
    private async Task RemoveCartAsync(string cartId)
    {
        // la DLL del PTL blocca il thread che la chiama: si lavora fuori dal thread della finestra
        var outcome = await Task.Run(() => _cartManager.ReleaseCartAsync(cartId, CancellationToken.None));

        SetMessage(outcome.Message);
        await RefreshAsync(CancellationToken.None);
    }

    private void SetMessage(string text) => _dispatcher.Post(() => Message = text);
}
