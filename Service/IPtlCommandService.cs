using System.Threading;
using System.Threading.Tasks;
using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service;

public interface IPtlCommandService
{
    Task SendAsync(string moduleAddress, PtlActivation activation, CancellationToken ct);

    Task WaitForButtonAsync(string expectedModule, CancellationToken ct);
}
