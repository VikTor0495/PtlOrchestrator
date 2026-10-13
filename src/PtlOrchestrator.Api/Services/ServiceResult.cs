namespace PtlOrchestrator.Api.Services;

public enum ServiceError
{
    None,
    Validation,
    NotFound,
    Conflict
}

/// <summary>
/// Esito di un'operazione di servizio: tiene la logica di dominio indipendente da HTTP,
/// sarà l'endpoint a tradurre l'errore in uno status code.
/// </summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, ServiceError error, string? message)
    {
        Value = value;
        Error = error;
        Message = message;
    }

    public bool Success => Error == ServiceError.None;
    public T? Value { get; }
    public ServiceError Error { get; }
    public string? Message { get; }

    public static ServiceResult<T> Ok(T value)
        => new(value, ServiceError.None, null);

    public static ServiceResult<T> Fail(ServiceError error, string message)
        => new(default, error, message);
}
