using PtlOrchestrator.Domain;

namespace PtlOrchestrator.File;

public interface ICartReportWriter
{
    void Write(IEnumerable<Cart> carts);
}