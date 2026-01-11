namespace PtlOrchestrator.Domain;

public sealed class Basket
{
    private readonly string _basketId;
    private int _maxQuantity;

    private string? _barcode;
    private int _currentQuantity;

    public Basket(string basketId, int maxQuantity)
    {
        if (string.IsNullOrWhiteSpace(basketId))
            throw new ArgumentException("BasketId non valido", nameof(basketId));

        if (maxQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxQuantity), "MaxQuantity non può essere negativa");

        _basketId = basketId;
        _maxQuantity = maxQuantity;
    }

    public string BasketId => _basketId;
    public string? Barcode => _barcode;
    public int CurrentQuantity => _currentQuantity;
    public int MaxQuantity => _maxQuantity;

    public bool IsEmpty => _currentQuantity == 0;
    public bool IsFull => _currentQuantity >= _maxQuantity;

    public void AddItem(string barcode)
    {
        if (!CanAccept(barcode))
            throw new InvalidOperationException(
                $"Basket {BasketId} non può accettare il barcode {barcode}");

        _barcode ??= barcode;
        _currentQuantity++;
    }

    public void UpdateMaxQuantity(int newMaxQuantity)
    {
        if (newMaxQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(newMaxQuantity), "MaxQuantity non può essere negativa");

        if (newMaxQuantity < _currentQuantity)
            throw new InvalidOperationException(
                "Il nuovo MaxQuantity è inferiore alla quantità attuale");

        _maxQuantity = newMaxQuantity;
    }

    public void RemoveItem(string barcode)
    {
        if (Barcode != barcode || _currentQuantity == 0)
            return;

        _currentQuantity--;

        if (_currentQuantity == 0)
        {
            _barcode = null;
            _maxQuantity = 0;
        }
    }

    public void Reset()
    {
        _barcode = null;
        _maxQuantity = 0;
        _currentQuantity = 0;
    }

    private bool CanAccept(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return false;

        if (IsFull)
            return false;

        return _barcode == null || _barcode == barcode;
    }

}
