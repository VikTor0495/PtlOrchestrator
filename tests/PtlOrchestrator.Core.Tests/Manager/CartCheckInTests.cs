using AioiSystems.Lightstep;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Carts;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Manager.Impl;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Manager;

public class CartCheckInTests
{
    private sealed class FakeAssignmentClient : ICartAssignmentClient
    {
        public CartClaimResult? NextClaim { get; set; }
        public Exception? ClaimFailure { get; set; }
        public Exception? ReleaseFailure { get; set; }
        public int ClaimCalls { get; private set; }
        public List<string> Released { get; } = [];

        public bool IsKnownCart(string cartCode) => true;

        public Task RefreshCatalogAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CartClaimResult> ClaimAsync(string cartCode, string stationId, Guid? sessionId, CancellationToken cancellationToken)
        {
            ClaimCalls++;
            if (ClaimFailure is not null)
                throw ClaimFailure;

            return Task.FromResult(NextClaim!);
        }

        public Task ReleaseAsync(string cartCode, string stationId, CancellationToken cancellationToken)
        {
            if (ReleaseFailure is not null)
                throw ReleaseFailure;

            Released.Add(cartCode);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConnections : ILightstepConnectionService
    {
        public bool Reachable { get; set; } = true;
        public List<PtlEndpoint> Tried { get; } = [];

        public bool IsConnected => Reachable;
        public bool IsConnectedTo(PtlEndpoint endpoint) => Reachable;
        public Task EnsureConnectedAsync(CancellationToken ct) => Task.CompletedTask;
        public Task EnsureConnectedAsync(PtlEndpoint endpoint, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> TryConnectAsync(PtlEndpoint endpoint, CancellationToken ct)
        {
            Tried.Add(endpoint);
            return Task.FromResult(Reachable);
        }

        public EthernetController GetController(PtlEndpoint? endpoint = null) => throw new NotSupportedException();
        public void Disconnect() { }
        public void Disconnect(PtlEndpoint endpoint) { }
    }

    private sealed class FakeStore : IActiveCartStore
    {
        public List<string> Saved { get; private set; } = [];
        public Task SaveAsync(IReadOnlyCollection<string> cartCodes, CancellationToken cancellationToken)
        {
            Saved = [.. cartCodes];
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(Saved);
    }

    private static readonly PtlEndpoint Controller = new("192.168.1.20", 15010);

    private readonly CartContainer _container = new([], activeCartIds: []);
    private readonly FakePtlCommandService _ptl = new();
    private readonly FakeAssignmentClient _client = new();
    private readonly FakeConnections _connections = new();
    private readonly FakeStore _store = new();
    private readonly CartManager _manager;

    public CartCheckInTests()
    {
        var session = new OperatorSessionContext();
        session.Begin(new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Test", DateTimeOffset.UtcNow));

        _manager = new CartManager(
            _container,
            _ptl,
            new FakeCartReportWriter(),
            new StubBasketLimitService(new Dictionary<string, int>()),
            new FakeOperationRecorder(),
            Options.Create(new TimeoutOptions { Minutes = 5 }),
            _client,
            _connections,
            session,
            Options.Create(new StationOptions { StationId = "ST-01" }),
            _store,
            NullLogger<CartManager>.Instance);
    }

    private void GrantCart(string code, params string[] baskets)
        => _client.NextClaim = new CartClaimResult(
            new CartCheckInResponse(code, Controller.Ip, Controller.Port, baskets), null);

    [Fact]
    public async Task CheckIn_Success_ActivatesTheCartOnTheControllerRegisteredOnTheServer()
    {
        GrantCart("1", "0001", "0002");

        var outcome = await _manager.CheckInAsync("1", CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.True(_container.IsActive("1"));
        var cart = _container.GetActive("1")!;
        Assert.Equal(Controller, cart.Endpoint);
        Assert.Equal(["0001", "0002"], cart.GetBaskets.Select(b => b.BasketId));
    }

    [Fact]
    public async Task CheckIn_OfACartAlreadyActive_DoesNotAskTheServerAgain()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);

        var outcome = await _manager.CheckInAsync("1", CancellationToken.None);

        Assert.Equal(CartCheckInResult.AlreadyActive, outcome.Result);
        Assert.Equal(1, _client.ClaimCalls);
    }

    [Fact]
    public async Task CheckIn_OccupiedByAnotherStation_IsRejectedWithTheServerReason()
    {
        _client.NextClaim = new CartClaimResult(null, "Carrello 1 già in uso dalla postazione ST-02");

        var outcome = await _manager.CheckInAsync("1", CancellationToken.None);

        Assert.Equal(CartCheckInResult.Rejected, outcome.Result);
        Assert.Contains("ST-02", outcome.Message);
        Assert.False(_container.IsActive("1"));
    }

    [Fact]
    public async Task CheckIn_WhenTheServerIsUnreachable_IsRejectedAndNothingIsActivated()
    {
        _client.ClaimFailure = new HttpRequestException("connessione rifiutata");

        var outcome = await _manager.CheckInAsync("1", CancellationToken.None);

        Assert.Equal(CartCheckInResult.Rejected, outcome.Result);
        Assert.Contains("Server non raggiungibile", outcome.Message);
        Assert.False(_container.IsActive("1"));
    }

    [Fact]
    public async Task CheckIn_WhenTheControllerDoesNotAnswer_ReleasesTheCartOnTheServer()
    {
        GrantCart("1", "0001");
        _connections.Reachable = false;

        var outcome = await _manager.CheckInAsync("1", CancellationToken.None);

        Assert.Equal(CartCheckInResult.Rejected, outcome.Result);
        Assert.Contains("192.168.1.20:15010", outcome.Message);
        Assert.Equal(["1"], _client.Released);
        Assert.False(_container.IsActive("1"));
    }

    [Fact]
    public async Task ReleaseCart_Empty_FreesItOnTheServerAndTurnsItsLampsOff()
    {
        GrantCart("1", "0001", "0002");
        await _manager.CheckInAsync("1", CancellationToken.None);
        _ptl.SentCommands.Clear();
        _ptl.SentTo.Clear();

        var outcome = await _manager.ReleaseCartAsync("1", CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(["1"], _client.Released);
        Assert.False(_container.IsActive("1"));
        Assert.Equal([Controller], _ptl.SentTo);
    }

    [Fact]
    public async Task ReleaseCart_WithProducts_IsRefusedAndTheServerIsNotCalled()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);
        _container.AssignItem("8000123", maxQuantity: 2);

        var outcome = await _manager.ReleaseCartAsync("1", CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Empty(_client.Released);
        Assert.True(_container.IsActive("1"));
    }

    [Fact]
    public async Task ReleaseCart_WhenTheServerRefuses_KeepsTheCartActive()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);
        _client.ReleaseFailure = new InvalidOperationException("occupato da un'altra postazione");

        var outcome = await _manager.ReleaseCartAsync("1", CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.True(_container.IsActive("1"));
    }

    [Fact]
    public async Task ResetAll_SendsEachControllerOnlyItsOwnBaskets()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);
        _ptl.SentCommands.Clear();
        _ptl.SentTo.Clear();

        await _manager.ResetAll(CancellationToken.None);

        Assert.NotEmpty(_ptl.SentTo);
        Assert.All(_ptl.SentTo, endpoint => Assert.Equal(Controller, endpoint));
    }

    [Fact]
    public async Task CheckIn_BeyondTheMaximumNumberOfCarts_IsRefusedWithoutAskingTheServer()
    {
        for (var n = 1; n <= CartContainer.MaxActiveCarts; n++)
        {
            GrantCart(n.ToString(), $"000{n}");
            Assert.True((await _manager.CheckInAsync(n.ToString(), CancellationToken.None)).Success);
        }

        var outcome = await _manager.CheckInAsync("5", CancellationToken.None);

        Assert.Equal(CartCheckInResult.LimitReached, outcome.Result);
        Assert.Contains("Massimo 4", outcome.Message);
        Assert.Equal(CartContainer.MaxActiveCarts, _client.ClaimCalls);
    }

    [Fact]
    public async Task CheckInAndRelease_KeepTheStoredListOfActiveCartsUpToDate()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);
        GrantCart("2", "0002");
        await _manager.CheckInAsync("2", CancellationToken.None);
        Assert.Equal(["1", "2"], _store.Saved);

        await _manager.ReleaseCartAsync("1", CancellationToken.None);

        Assert.Equal(["2"], _store.Saved);
    }

    [Fact]
    public async Task ResetAll_DoesNotDetachAnyCart()
    {
        GrantCart("1", "0001");
        await _manager.CheckInAsync("1", CancellationToken.None);

        await _manager.CloseWorkAsync(CancellationToken.None);

        // il reset azzera i prodotti ma i carrelli restano attivi e collegati al server
        Assert.True(_container.IsActive("1"));
        Assert.Empty(_client.Released);
        Assert.Equal(["1"], _store.Saved);
    }

    [Fact]
    public async Task CheckIn_AfterReleasingACart_IsAllowedAgain()
    {
        for (var n = 1; n <= CartContainer.MaxActiveCarts; n++)
        {
            GrantCart(n.ToString(), $"000{n}");
            await _manager.CheckInAsync(n.ToString(), CancellationToken.None);
        }

        await _manager.ReleaseCartAsync("1", CancellationToken.None);
        GrantCart("5", "0005");

        var outcome = await _manager.CheckInAsync("5", CancellationToken.None);

        Assert.True(outcome.Success);
    }
}
