using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public class ActiveCartRestorerTests
{
    private sealed class MemoryStore(params string[] saved) : IActiveCartStore
    {
        public List<string> Saved { get; private set; } = [.. saved];

        public Task SaveAsync(IReadOnlyCollection<string> cartCodes, CancellationToken cancellationToken)
        {
            Saved = [.. cartCodes];
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(Saved);
    }

    private readonly FakeCartManager _carts = new();

    private ActiveCartRestorer CreateRestorer(MemoryStore store)
        => new(_carts, store, NullLogger<ActiveCartRestorer>.Instance);

    [Fact]
    public async Task Restore_ChecksInEverySavedCart()
    {
        var restorer = CreateRestorer(new MemoryStore("1", "2"));

        var pending = await restorer.RestoreAsync(CancellationToken.None);

        Assert.Equal(0, pending);
        Assert.Equal(["1", "2"], _carts.CheckedInCarts);
    }

    [Fact]
    public async Task Restore_SkipsTheCartsThatAreAlreadyActive()
    {
        _carts.ActiveCarts.Add("1");
        var restorer = CreateRestorer(new MemoryStore("1", "2"));

        await restorer.RestoreAsync(CancellationToken.None);

        Assert.Equal(["2"], _carts.CheckedInCarts);
    }

    [Fact]
    public async Task Restore_CountsTheCartsThatCouldNotBeResumedYet()
    {
        _carts.NextCheckInResult = CartCheckInResult.Rejected;
        var restorer = CreateRestorer(new MemoryStore("1", "2"));

        var pending = await restorer.RestoreAsync(CancellationToken.None);

        Assert.Equal(2, pending);
    }

    [Fact]
    public async Task Restore_WithNothingSaved_DoesNothing()
    {
        var restorer = CreateRestorer(new MemoryStore());

        var pending = await restorer.RestoreAsync(CancellationToken.None);

        Assert.Equal(0, pending);
        Assert.Empty(_carts.CheckedInCarts);
    }

    [Fact]
    public async Task AbandonPending_KeepsOnlyTheCartsThatAreActive()
    {
        _carts.ActiveCarts.Add("1");
        var store = new MemoryStore("1", "2");

        await CreateRestorer(store).AbandonPendingAsync(CancellationToken.None);

        Assert.Equal(["1"], store.Saved);
    }
}
