using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public sealed class ScanListenerTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeBarcodeSource _source = new();
    private readonly StubScanDispatcher _dispatcher = new();
    private readonly AppProcessingState _processingState = new(NullLogger<AppProcessingState>.Instance);
    private readonly Channel<ScanProcessedEventArgs> _published = Channel.CreateUnbounded<ScanProcessedEventArgs>();
    private readonly ScanListener _listener;

    public ScanListenerTests()
    {
        _listener = new ScanListener(
            _source,
            _dispatcher,
            _processingState,
            NullLogger<ScanListener>.Instance);

        _listener.ScanProcessed += (_, args) => _published.Writer.TryWrite(args);
    }

    public Task InitializeAsync() => _listener.StartAsync(CancellationToken.None);

    public async Task DisposeAsync()
    {
        _source.Complete();
        await _listener.StopAsync(CancellationToken.None);
        _listener.Dispose();
    }

    private async Task<ScanProcessedEventArgs> NextPublishedAsync()
    {
        using var cts = new CancellationTokenSource(Timeout);
        return await _published.Reader.ReadAsync(cts.Token);
    }

    [Fact]
    public async Task ScansAreDispatchedAndTheirOutcomeIsPublished()
    {
        _dispatcher.Outcome = ScanOutcome.Ignored();

        _source.Push("AAA");
        var published = await NextPublishedAsync();

        Assert.Equal("AAA", published.ScannedCode);
        Assert.Equal(ScanOutcomeKind.Ignored, published.Outcome.Kind);
        Assert.Equal(["AAA"], _dispatcher.DispatchedCodes);
    }

    [Fact]
    public async Task ConsecutiveScans_AreProcessedInOrder()
    {
        _source.Push("AAA");
        await NextPublishedAsync();
        _source.Push("BBB");
        await NextPublishedAsync();

        Assert.Equal(["AAA", "BBB"], _dispatcher.DispatchedCodes);
    }

    [Fact]
    public async Task WhenTheSystemIsBusy_TheScanIsDroppedAsSystemBusyWithoutDispatching()
    {
        _processingState.TryEnter();

        _source.Push("AAA");
        var published = await NextPublishedAsync();

        Assert.Equal(ScanOutcomeKind.SystemBusy, published.Outcome.Kind);
        Assert.Empty(_dispatcher.DispatchedCodes);
    }

    [Fact]
    public async Task ScansArrivingWhileOneIsBeingProcessed_AreNotQueued()
    {
        var gate = new TaskCompletionSource();
        _dispatcher.OnDispatch = async _ =>
        {
            _processingState.TryEnter();
            await gate.Task;
            _processingState.Exit();
        };

        _source.Push("AAA");
        await _dispatcher.WaitUntilDispatchStartedAsync();
        _source.Push("BBB");
        var dropped = await NextPublishedAsync();
        gate.SetResult();
        var processed = await NextPublishedAsync();

        Assert.Equal("BBB", dropped.ScannedCode);
        Assert.Equal(ScanOutcomeKind.SystemBusy, dropped.Outcome.Kind);
        Assert.Equal("AAA", processed.ScannedCode);
        Assert.Equal(["AAA"], _dispatcher.DispatchedCodes);
    }

    [Fact]
    public async Task AFailingObserver_DoesNotStopTheListener()
    {
        _listener.ScanProcessed += (_, _) => throw new InvalidOperationException("observer rotto");

        _source.Push("AAA");
        await NextPublishedAsync();
        _source.Push("BBB");
        await NextPublishedAsync();

        Assert.Equal(["AAA", "BBB"], _dispatcher.DispatchedCodes);
    }

    private sealed class StubScanDispatcher : IScanDispatcher
    {
        private readonly TaskCompletionSource _started = new();

        public List<string> DispatchedCodes { get; } = [];

        public ScanOutcome Outcome { get; set; } = ScanOutcome.Ignored();

        public Func<string, Task>? OnDispatch { get; set; }

        public Task WaitUntilDispatchStartedAsync() => _started.Task.WaitAsync(Timeout);

        public async Task<ScanOutcome> DispatchAsync(string scannedCode, CancellationToken cancellationToken)
        {
            DispatchedCodes.Add(scannedCode);
            _started.TrySetResult();

            if (OnDispatch is not null)
                await OnDispatch(scannedCode);

            return Outcome;
        }
    }
}
