using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Contracts.Stations;

namespace PtlOrchestrator.Api.Services;

public interface IStationService
{
    Task<IReadOnlyList<StationInfo>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed class StationService(PtlDbContext dbContext) : IStationService
{
    private readonly PtlDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<StationInfo>> GetAllAsync(CancellationToken cancellationToken)
    {
        var stations = await _dbContext.Stations
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return [.. stations.Select(s => new StationInfo(s.Id, s.FirstSeenAt, s.LastSeenAt))];
    }
}
