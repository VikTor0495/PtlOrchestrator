using Microsoft.AspNetCore.Authentication;

namespace PtlOrchestrator.Api.Security;

public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Registra autenticazione a chiave API e policy di autorizzazione. Le chiavi sono validate
    /// all'avvio: l'applicazione non parte con chiavi vuote, troppo corte o uguali tra loro.
    /// </summary>
    public static IServiceCollection AddApiKeySecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .Validate(
                o => IsLongEnough(o.StationKey),
                $"ApiKeys:StationKey deve avere almeno {ApiKeyOptions.MinimumKeyLength} caratteri")
            .Validate(
                o => IsLongEnough(o.AdminKey),
                $"ApiKeys:AdminKey deve avere almeno {ApiKeyOptions.MinimumKeyLength} caratteri")
            .Validate(
                o => o.StationKey != o.AdminKey,
                "ApiKeys:StationKey e ApiKeys:AdminKey devono essere diverse")
            .ValidateOnStart();

        services
            .AddAuthentication(ApiKeyDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, _ => { });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(ApiKeyDefaults.StationPolicy, policy => policy
                .RequireRole(ApiKeyDefaults.StationRole, ApiKeyDefaults.AdminRole))
            .AddPolicy(ApiKeyDefaults.AdminPolicy, policy => policy
                .RequireRole(ApiKeyDefaults.AdminRole));

        return services;
    }

    // il binder può assegnare null anche a una proprietà non nullable (es. "StationKey": null)
    private static bool IsLongEnough(string? key)
        => key is not null && key.Length >= ApiKeyOptions.MinimumKeyLength;
}
