namespace PtlOrchestrator.Domain.Operations;

public sealed class LoginResult
{
    private LoginResult(OperatorSession? session, LoginFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public bool Success => Session is not null;
    public OperatorSession? Session { get; }
    public LoginFailure? Failure { get; }

    public static LoginResult Succeeded(OperatorSession session)
        => new(session, null);

    public static LoginResult Failed(LoginFailure failure)
        => new(null, failure);
}
