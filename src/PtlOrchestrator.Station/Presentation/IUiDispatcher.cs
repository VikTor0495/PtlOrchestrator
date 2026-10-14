using System.Windows;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>Porta un'azione sul thread della UI. Astratto per poter provare i ViewModel senza WPF in esecuzione.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
