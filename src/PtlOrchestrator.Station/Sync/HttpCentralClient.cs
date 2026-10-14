using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Station.Sync;

/// <summary>
/// Client HTTP del server centrale. L'<see cref="HttpClient"/> arriva già configurato
/// (indirizzo base, intestazione <c>X-Api-Key</c>, timeout).
/// </summary>
public sealed class HttpCentralClient(HttpClient httpClient) : ICentralClient
{
    private readonly HttpClient _httpClient = httpClient;

    public Task<CentralResult<IngestEventsResponse>> SendEventsAsync(
        IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
        => SendAsync<IngestEventsResponse>(
            () => _httpClient.PostAsJsonAsync("api/events", new IngestEventsRequest(events), cancellationToken),
            cancellationToken);

    public Task<CentralResult<OperatorsSnapshotResponse>> GetOperatorsSnapshotAsync(
        DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var url = since is null
            ? "api/operators/snapshot"
            : "api/operators/snapshot?since=" + Uri.EscapeDataString(since.Value.ToString("O", CultureInfo.InvariantCulture));

        return SendAsync<OperatorsSnapshotResponse>(() => _httpClient.GetAsync(url, cancellationToken), cancellationToken);
    }

    private static async Task<CentralResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();

            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return value is null
                    ? CentralResult<T>.Transient("Risposta del server vuota")
                    : CentralResult<T>.Ok(value);
            }

            var message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

            // solo un contenuto non valido è un rifiuto definitivo: credenziali sbagliate o server
            // in difficoltà si risolvono lato configurazione/server, e gli eventi vanno conservati
            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                ? CentralResult<T>.Rejected(message)
                : CentralResult<T>.Transient(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return CentralResult<T>.Transient("Timeout della richiesta al server");
        }
        catch (HttpRequestException ex)
        {
            return CentralResult<T>.Transient(ex.Message);
        }
        catch (JsonException ex)
        {
            return CentralResult<T>.Transient("Risposta del server non valida: " + ex.Message);
        }
    }
}
