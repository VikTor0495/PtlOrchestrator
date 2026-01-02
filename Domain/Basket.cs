namespace PtlController.Domain;

public sealed class Basket(int basketId, int maxQuantity)
{
    public int BasketId { get; } = basketId;
    public int MaxQuantity { get; } = maxQuantity;

    private string? _barcode;
    private int _currentQuantity;

    public bool IsEmpty => _barcode == null;
    public bool IsFull => _currentQuantity >= MaxQuantity;

    public bool CanAccept(string barcode)
        => (IsEmpty || _barcode == barcode) && !IsFull;

    public void AddItem(string barcode)
    {
        if (!CanAccept(barcode))
            throw new InvalidOperationException("Basket non compatibile");

        _barcode ??= barcode;
        _currentQuantity++;
    }

    public void Reset()
    {
        _barcode = null;
        _currentQuantity = 0;
    }

    public string? Barcode => _barcode;
    public int CurrentQuantity => _currentQuantity;
}
