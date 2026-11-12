using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeCartManager : ICartManager
{
    public List<string> ProcessedBarcodes { get; } = [];

    public Func<string, Task<CartAssignmentResult>>? OnProcessBarcode { get; set; }

    /// <summary>Codici che il fake riconosce come carrelli del catalogo.</summary>
    public HashSet<string> KnownCarts { get; } = [];

    public List<string> CheckedInCarts { get; } = [];

    public CartCheckInResult NextCheckInResult { get; set; } = CartCheckInResult.Done;

    public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {
        ProcessedBarcodes.Add(barcode);

        return OnProcessBarcode?.Invoke(barcode)
            ?? Task.FromResult(CartAssignmentResult.Rejected("non configurato"));
    }

    public bool IsKnownCart(string code) => KnownCarts.Contains(code);

    public Task<CartCheckInOutcome> CheckInAsync(string cartId, CancellationToken cancellationToken)
    {
        CheckedInCarts.Add(cartId);

        var message = NextCheckInResult switch
        {
            CartCheckInResult.Done => $"Carrello {cartId} attivato",
            CartCheckInResult.AlreadyActive => $"Il carrello {cartId} è già attivo",
            _ => $"Carrello {cartId} non disponibile"
        };

        return Task.FromResult(new CartCheckInOutcome(NextCheckInResult, message));
    }

    public Task<CartReleaseOutcome> ReleaseCartAsync(string cartId, CancellationToken cancellationToken)
        => Task.FromResult(new CartReleaseOutcome(true, $"Carrello {cartId} rimosso"));

    public IReadOnlyList<string> ActiveCartIds() => [];

    public bool HasItems() => false;

    public Task CloseWorkAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ResetAll(CancellationToken cancellationToken) => Task.CompletedTask;

    public void ShowStatus()
    {
    }

    public void WriteCsvReport()
    {
    }
}
