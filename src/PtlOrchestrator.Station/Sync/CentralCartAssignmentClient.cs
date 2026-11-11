using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PtlOrchestrator.Contracts.Carts;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Station.Sync;

/// <summary>
/// Client del server carrelli. Il catalogo dei codici è tenuto in memoria e aggiornato in background;
/// check-in e rilascio chiamano sempre il server, che è l'unico a decidere chi occupa un carrello.
/// </summary>
public sealed class CentralCartAssignmentClient(IHttpClientFactory httpClientFactory) : ICartAssignmentClient
{
    public const string HttpClientName = "central-carts";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private volatile HashSet<string> _catalogue = [];

    /// <summary>Ultimo catalogo ricevuto: vuoto finché il server non risponde.</summary>
    public IReadOnlyCollection<string> Catalogue => _catalogue;

    public bool IsKnownCart(string cartCode) => _catalogue.Contains(cartCode);

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync("api/carts/catalog", cancellationToken);
        response.EnsureSuccessStatusCode();

        var codes = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken) ?? [];
        _catalogue = [.. codes];
    }

    public async Task<CartClaimResult> ClaimAsync(string cartCode, string stationId, Guid? sessionId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            $"api/carts/{Uri.EscapeDataString(cartCode)}/checkin",
            new CartCheckInRequest(stationId, sessionId),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var cart = await response.Content.ReadFromJsonAsync<CartCheckInResponse>(cancellationToken);
            return cart is null
                ? new CartClaimResult(null, "Risposta del server non valida")
                : new CartClaimResult(cart, null);
        }

        // 404 (non configurato), 409 (occupato da un'altra postazione): il server spiega il motivo
        return new CartClaimResult(null, await ReadProblemAsync(response, cartCode));
    }

    public async Task ReleaseAsync(string cartCode, string stationId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            $"api/carts/{Uri.EscapeDataString(cartCode)}/release",
            new CartReleaseRequest(stationId),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemAsync(response, cartCode));
    }

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, string cartCode)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // corpo non JSON: si ricade sul messaggio generico
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => $"Carrello {cartCode} non configurato",
            HttpStatusCode.Conflict => $"Carrello {cartCode} non disponibile",
            _ => $"Il server ha risposto {(int)response.StatusCode} per il carrello {cartCode}"
        };
    }
}
