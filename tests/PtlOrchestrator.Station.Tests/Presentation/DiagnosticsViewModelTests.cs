using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Contracts.Carts;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Presentation;

public sealed class DiagnosticsViewModelTests
{
    private sealed class FakeServerClient : ICartAssignmentClient
    {
        public bool Reachable { get; set; } = true;
        public int Probes { get; private set; }

        public bool IsKnownCart(string cartCode) => false;

        public Task RefreshCatalogAsync(CancellationToken cancellationToken)
        {
            Probes++;
            return Reachable ? Task.CompletedTask : throw new HttpRequestException("server non raggiungibile");
        }

        public Task<CartClaimResult> ClaimAsync(string cartCode, string stationId, Guid? sessionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ReleaseAsync(string cartCode, string stationId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private static readonly PtlEndpoint FirstController = new("10.0.0.1", 15010);
    private static readonly PtlEndpoint SecondController = new("10.0.0.2", 15011);

    private readonly FakeServerClient _server = new();
    private readonly FakeLightstep _lightstep = new();

    private DiagnosticsViewModel CreateViewModel(params (string Id, PtlEndpoint Endpoint)[] carts)
    {
        var container = new CartContainer([], activeCartIds: []);
        foreach (var (id, endpoint) in carts)
            container.Activate(new Cart(id, [new Basket($"{id}01", 0)], endpoint));

        return new DiagnosticsViewModel(_server, _lightstep, container, new ImmediateDispatcher(), NullLogger<DiagnosticsViewModel>.Instance);
    }

    [Fact]
    public async Task Refresh_ListsTheCentralServerAndEveryActiveCart()
    {
        _lightstep.IsConnected = true;
        var viewModel = CreateViewModel(("1", FirstController), ("2", SecondController));

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(["Server centrale", "Carrello 1", "Carrello 2"], viewModel.Links.Select(l => l.Name));
        Assert.All(viewModel.Links, link => Assert.True(link.IsConnected));
        Assert.Equal("10.0.0.1:15010", viewModel.Links[1].Detail);
    }

    [Fact]
    public async Task Refresh_WithoutCarts_ListsOnlyTheServer()
    {
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("Server centrale", Assert.Single(viewModel.Links).Name);
    }

    [Fact]
    public async Task Refresh_WhenTheServerDoesNotAnswer_ShowsItAsNotConnected()
    {
        _server.Reachable = false;
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        var server = Assert.Single(viewModel.Links);
        Assert.False(server.IsConnected);
        Assert.Equal("Non connesso", server.StatusText);
    }

    [Fact]
    public async Task Refresh_ShowsADisconnectedCartWhileTheServerIsFine()
    {
        _lightstep.IsConnected = false;
        var viewModel = CreateViewModel(("1", FirstController));

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.Links[0].IsConnected);
        Assert.False(viewModel.Links[1].IsConnected);
    }

    [Fact]
    public async Task ReconnectingACart_DisconnectsAndConnectsItAgain()
    {
        _lightstep.IsConnected = false;
        var viewModel = CreateViewModel(("1", FirstController));
        await viewModel.RefreshAsync(CancellationToken.None);

        await viewModel.Links[1].ReconnectCommand.ExecuteAsync(null);

        Assert.Equal(["disconnect", "try-connect 10.0.0.1:15010"], _lightstep.Calls);
        Assert.True(viewModel.Links[1].IsConnected);
        Assert.Contains("riconnesso", viewModel.Message);
    }

    [Fact]
    public async Task ReconnectingTheServer_ProbesItAgainAndUpdatesTheList()
    {
        _server.Reachable = false;
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        var probesBefore = _server.Probes;

        _server.Reachable = true;
        await viewModel.Links[0].ReconnectCommand.ExecuteAsync(null);

        Assert.True(_server.Probes > probesBefore);
        Assert.True(viewModel.Links[0].IsConnected);
        Assert.Contains("raggiungibile", viewModel.Message);
    }

    [Fact]
    public async Task ReconnectingTheServer_WhenItIsStillDown_ExplainsWhatToCheck()
    {
        _server.Reachable = false;
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);

        await viewModel.Links[0].ReconnectCommand.ExecuteAsync(null);

        Assert.False(viewModel.Links[0].IsConnected);
        Assert.Contains("Impostazioni", viewModel.Message);
    }
}
