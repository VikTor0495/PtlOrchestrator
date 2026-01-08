using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Report;

public interface ICartReportWriter
{
    void Write(IEnumerable<Cart> carts);
}