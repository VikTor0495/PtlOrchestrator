using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Tests.Service;

public class AppProcessingStateTests
{
    private readonly AppProcessingState _state = new(NullLogger<AppProcessingState>.Instance);

    [Fact]
    public void NewState_IsNotBusy()
    {
        Assert.False(_state.IsBusy);
    }

    [Fact]
    public void TryEnter_WhenIdle_SucceedsAndMarksBusy()
    {
        var entered = _state.TryEnter();

        Assert.True(entered);
        Assert.True(_state.IsBusy);
    }

    [Fact]
    public void TryEnter_WhenAlreadyBusy_Fails()
    {
        _state.TryEnter();

        Assert.False(_state.TryEnter());
        Assert.True(_state.IsBusy);
    }

    [Fact]
    public void Exit_ReleasesBusyState()
    {
        _state.TryEnter();

        _state.Exit();

        Assert.False(_state.IsBusy);
        Assert.True(_state.TryEnter());
    }

    [Fact]
    public void Exit_WhenAlreadyIdle_IsHarmless()
    {
        _state.Exit();

        Assert.False(_state.IsBusy);
    }

    [Fact]
    public void TryEnter_FromManyThreads_AdmitsExactlyOne()
    {
        var winners = 0;

        Parallel.For(0, 200, _ =>
        {
            if (_state.TryEnter())
                Interlocked.Increment(ref winners);
        });

        Assert.Equal(1, winners);
    }
}
