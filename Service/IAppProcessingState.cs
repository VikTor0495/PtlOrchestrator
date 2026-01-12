namespace PtlOrchestrator.Service;

public interface IAppProcessingState
{
    bool IsBusy { get; }
    bool TryEnter();
    void Exit();
}