namespace PtlOrchestrator.Domain.Operations;

public enum ScanOutcomeKind
{
    Ignored,
    OperatorLoggedIn,
    LoginRejected,
    SystemBusy,
    ProductAccepted,
    ProductRejected,
    ProcessingFailed
}

/// <summary>
/// Esito di una scansione, pronto per essere mostrato all'operatore. Una sola classe con
/// costruttori statici (come <see cref="CartAssignmentResult"/>) evita null-check sparsi.
/// </summary>
public sealed class ScanOutcome
{
    private ScanOutcome(
        ScanOutcomeKind kind,
        string? message = null,
        OperatorSession? session = null,
        LoginFailure? loginFailure = null,
        CartAssignmentResult? assignment = null)
    {
        Kind = kind;
        Message = message;
        Session = session;
        LoginFailure = loginFailure;
        Assignment = assignment;
    }

    public ScanOutcomeKind Kind { get; }
    public string? Message { get; }
    public OperatorSession? Session { get; }
    public LoginFailure? LoginFailure { get; }
    public CartAssignmentResult? Assignment { get; }

    public static ScanOutcome Ignored()
        => new(ScanOutcomeKind.Ignored);

    public static ScanOutcome OperatorLoggedIn(OperatorSession session)
        => new(ScanOutcomeKind.OperatorLoggedIn, session: session);

    public static ScanOutcome LoginRejected(LoginFailure failure)
        => new(ScanOutcomeKind.LoginRejected, loginFailure: failure);

    public static ScanOutcome SystemBusy()
        => new(ScanOutcomeKind.SystemBusy, "Sistema occupato");

    public static ScanOutcome ProductAccepted(CartAssignmentResult assignment)
        => new(ScanOutcomeKind.ProductAccepted, assignment: assignment);

    public static ScanOutcome ProductRejected(CartAssignmentResult assignment)
        => new(ScanOutcomeKind.ProductRejected, assignment.Reason, assignment: assignment);

    public static ScanOutcome ProcessingFailed(string message)
        => new(ScanOutcomeKind.ProcessingFailed, message);
}
