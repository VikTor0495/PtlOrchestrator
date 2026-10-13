using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public class OperatorLoginServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 30, 0, TimeSpan.Zero);

    private readonly FakeOperatorDirectory _directory = new();
    private readonly OperatorSessionContext _sessionContext = new();
    private readonly CollectingOperationEventSink _sink = new();
    private readonly AppProcessingState _processingState = new(NullLogger<AppProcessingState>.Instance);
    private readonly OperatorLoginService _service;

    public OperatorLoginServiceTests()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var recorder = new OperationRecorder(
            _sink,
            _sessionContext,
            Options.Create(new StationOptions { StationId = "ST-01" }),
            timeProvider,
            NullLogger<OperationRecorder>.Instance);

        _service = new OperatorLoginService(
            _directory,
            _sessionContext,
            recorder,
            _processingState,
            timeProvider,
            NullLogger<OperatorLoginService>.Instance);
    }

    [Fact]
    public async Task LoginWithBadge_ForKnownActiveOperator_StartsASession()
    {
        var mario = _directory.Add(badgeCode: "BADGE-1");

        var result = await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(mario.Id, result.Session!.OperatorId);
        Assert.Equal("Mario Rossi", result.Session.OperatorName);
        Assert.Equal(Now, result.Session.StartedAt);
        Assert.Same(result.Session, _sessionContext.Current);
    }

    [Fact]
    public async Task LoginWithBadge_IgnoresSurroundingWhitespace()
    {
        _directory.Add(badgeCode: "BADGE-1");

        var result = await _service.LoginWithBadgeAsync("  BADGE-1 ", CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task LoginWithBadge_ForUnknownBadge_Fails()
    {
        _directory.Add(badgeCode: "BADGE-1");

        var result = await _service.LoginWithBadgeAsync("OTHER", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(LoginFailure.UnknownOperator, result.Failure);
        Assert.False(_sessionContext.HasActiveSession);
    }

    [Fact]
    public async Task LoginWithBadge_ForInactiveOperator_Fails()
    {
        _directory.Add(badgeCode: "BADGE-1", isActive: false);

        var result = await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        Assert.Equal(LoginFailure.OperatorInactive, result.Failure);
        Assert.False(_sessionContext.HasActiveSession);
    }

    [Fact]
    public async Task LoginWithPin_WithCorrectPin_StartsASession()
    {
        var mario = _directory.Add(pin: "4821");

        var result = await _service.LoginWithPinAsync(mario.Id, "4821", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(mario.Id, _sessionContext.Current!.OperatorId);
    }

    [Fact]
    public async Task LoginWithPin_WithWrongPin_FailsWithoutStartingASession()
    {
        var mario = _directory.Add(pin: "4821");

        var result = await _service.LoginWithPinAsync(mario.Id, "0000", CancellationToken.None);

        Assert.Equal(LoginFailure.InvalidPin, result.Failure);
        Assert.False(_sessionContext.HasActiveSession);
        Assert.Empty(_sink.Events);
    }

    [Fact]
    public async Task LoginWithPin_ForOperatorWithoutPin_Fails()
    {
        var mario = _directory.Add(pin: null);

        var result = await _service.LoginWithPinAsync(mario.Id, "4821", CancellationToken.None);

        Assert.Equal(LoginFailure.InvalidPin, result.Failure);
    }

    [Fact]
    public async Task LoginWithPin_ForUnknownOperator_Fails()
    {
        var result = await _service.LoginWithPinAsync(Guid.NewGuid(), "4821", CancellationToken.None);

        Assert.Equal(LoginFailure.UnknownOperator, result.Failure);
    }

    [Fact]
    public async Task LoginWithPin_ForInactiveOperator_FailsEvenWithCorrectPin()
    {
        var mario = _directory.Add(pin: "4821", isActive: false);

        var result = await _service.LoginWithPinAsync(mario.Id, "4821", CancellationToken.None);

        Assert.Equal(LoginFailure.OperatorInactive, result.Failure);
    }

    [Fact]
    public async Task Login_WhileAnotherSessionIsActive_IsRejected()
    {
        _directory.Add(name: "Mario", badgeCode: "BADGE-1");
        _directory.Add(name: "Luisa", badgeCode: "BADGE-2");
        await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        var result = await _service.LoginWithBadgeAsync("BADGE-2", CancellationToken.None);

        Assert.Equal(LoginFailure.SessionAlreadyActive, result.Failure);
        Assert.Equal("Mario", _sessionContext.Current!.OperatorName);
    }

    [Fact]
    public async Task Login_RecordsSessionStartedAttributedToTheNewSession()
    {
        _directory.Add(badgeCode: "BADGE-1");

        var result = await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        var started = Assert.Single(_sink.Events);
        Assert.Equal(OperationEventType.SessionStarted, started.Type);
        Assert.Equal(result.Session!.SessionId, started.SessionId);
        Assert.Equal(result.Session.OperatorId, started.OperatorId);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        _directory.Add(badgeCode: "BADGE-1");
        await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.Equal(LogoutResult.Done, result);
        Assert.False(_sessionContext.HasActiveSession);
    }

    [Fact]
    public async Task Logout_RecordsSessionEndedStillAttributedToTheClosingSession()
    {
        _directory.Add(badgeCode: "BADGE-1");
        var login = await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);

        await _service.LogoutAsync(CancellationToken.None);

        var ended = _sink.Events.Last();
        Assert.Equal(OperationEventType.SessionEnded, ended.Type);
        Assert.Equal(login.Session!.SessionId, ended.SessionId);
        Assert.Equal(login.Session.OperatorId, ended.OperatorId);
    }

    [Fact]
    public async Task Logout_WithoutSession_ReportsNoActiveSession()
    {
        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.Equal(LogoutResult.NoActiveSession, result);
        Assert.Empty(_sink.Events);
    }

    [Fact]
    public async Task Logout_WhileAnOperationIsInProgress_IsRefusedAndKeepsTheSession()
    {
        _directory.Add(badgeCode: "BADGE-1");
        await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);
        _processingState.TryEnter();

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.Equal(LogoutResult.SystemBusy, result);
        Assert.True(_sessionContext.HasActiveSession);
    }

    [Fact]
    public async Task AfterLogout_ANewOperatorCanLogIn()
    {
        _directory.Add(name: "Mario", badgeCode: "BADGE-1");
        _directory.Add(name: "Luisa", badgeCode: "BADGE-2");
        await _service.LoginWithBadgeAsync("BADGE-1", CancellationToken.None);
        await _service.LogoutAsync(CancellationToken.None);

        var result = await _service.LoginWithBadgeAsync("BADGE-2", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Luisa", _sessionContext.Current!.OperatorName);
    }
}
