using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PtlOrchestrator.Api.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IOptions<ApiKeyOptions> apiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, urlEncoder)
{
    private readonly ApiKeyOptions _apiKeyOptions = apiKeyOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var providedKey) || providedKey.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = ResolveRole(providedKey.ToString());

        if (role is null)
            return Task.FromResult(AuthenticateResult.Fail("Chiave API non valida"));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)],
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? ResolveRole(string providedKey)
    {
        if (KeysMatch(providedKey, _apiKeyOptions.AdminKey))
            return ApiKeyDefaults.AdminRole;

        if (KeysMatch(providedKey, _apiKeyOptions.StationKey))
            return ApiKeyDefaults.StationRole;

        return null;
    }

    private static bool KeysMatch(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
            return false;

        // confronto a tempo costante: non rivela quanti caratteri iniziali sono corretti
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
