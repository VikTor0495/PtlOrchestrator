using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service.Impl;

public sealed class OperatorSessionContext : IOperatorSessionContext
{
    private volatile OperatorSession? _current;

    public OperatorSession? Current => _current;

    public bool HasActiveSession => _current is not null;

    public void Begin(OperatorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _current = session;
    }

    public void End()
    {
        _current = null;
    }
}
