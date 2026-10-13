namespace PtlOrchestrator.Domain.Operations;

public enum LoginFailure
{
    UnknownOperator,
    OperatorInactive,
    InvalidPin,
    SessionAlreadyActive
}
