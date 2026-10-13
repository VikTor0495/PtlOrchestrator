using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public class OperatorSessionContextTests
{
    private readonly OperatorSessionContext _context = new();

    private static OperatorSession NewSession()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Mario", DateTimeOffset.UtcNow);

    [Fact]
    public void NewContext_HasNoActiveSession()
    {
        Assert.False(_context.HasActiveSession);
        Assert.Null(_context.Current);
    }

    [Fact]
    public void Begin_MakesTheSessionCurrent()
    {
        var session = NewSession();

        _context.Begin(session);

        Assert.True(_context.HasActiveSession);
        Assert.Same(session, _context.Current);
    }

    [Fact]
    public void End_ClearsTheSession()
    {
        _context.Begin(NewSession());

        _context.End();

        Assert.False(_context.HasActiveSession);
        Assert.Null(_context.Current);
    }

    [Fact]
    public void Begin_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _context.Begin(null!));
    }
}
