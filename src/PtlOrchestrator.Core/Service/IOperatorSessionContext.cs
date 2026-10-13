using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service;

public interface IOperatorSessionContext
{
    OperatorSession? Current { get; }

    bool HasActiveSession { get; }

    void Begin(OperatorSession session);

    void End();
}
