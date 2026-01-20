using System.Threading;
using System.Threading.Tasks;
using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service;

public interface IPtlCommandService
{
    Task SendRawAsync(string moduleAddress, string command, CancellationToken cancellationToken);

    Task<string> WaitForButtonAsync(string expectedModule, CancellationToken cancellationToken);
}
