using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station.Tests.Presentation;

public class ScanMessageFormatterTests
{
    private static CartAssignmentResult Assignment(int quantity = 1, int max = 3)
    {
        var basket = new Basket("0007", max);
        for (var i = 0; i < quantity; i++)
            basket.AddItem("8000123");

        return CartAssignmentResult.NewItem(new Cart("2", [basket]), basket);
    }

    [Fact]
    public void Ignored_HasNoMessage()
        => Assert.Null(ScanMessageFormatter.Format("x", ScanOutcome.Ignored()));

    [Fact]
    public void OperatorLoggedIn_GreetsTheOperator()
    {
        var session = new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Mario Rossi", DateTimeOffset.UtcNow);

        var message = ScanMessageFormatter.Format("BADGE", ScanOutcome.OperatorLoggedIn(session))!;

        Assert.Equal(MessageSeverity.Success, message.Severity);
        Assert.Contains("Mario Rossi", message.Text);
    }

    [Theory]
    [InlineData(LoginFailure.UnknownOperator, "non riconosciuto")]
    [InlineData(LoginFailure.OperatorInactive, "non attivo")]
    [InlineData(LoginFailure.InvalidPin, "PIN errato")]
    [InlineData(LoginFailure.SessionAlreadyActive, "turno attivo")]
    public void LoginRejected_ExplainsTheReason(LoginFailure failure, string expected)
    {
        var message = ScanMessageFormatter.Format("BADGE", ScanOutcome.LoginRejected(failure))!;

        Assert.Equal(MessageSeverity.Error, message.Severity);
        Assert.Contains(expected, message.Text);
    }

    [Fact]
    public void SystemBusy_IsAWarning()
    {
        var message = ScanMessageFormatter.Format("8000123", ScanOutcome.SystemBusy())!;

        Assert.Equal(MessageSeverity.Warning, message.Severity);
    }

    [Fact]
    public void ProductAccepted_ShowsBarcodeCartBasketAndFill()
    {
        var message = ScanMessageFormatter.Format("8000123", ScanOutcome.ProductAccepted(Assignment(quantity: 2, max: 5)))!;

        Assert.Equal(MessageSeverity.Success, message.Severity);
        Assert.Contains("8000123", message.Text);
        Assert.Contains("carrello 2", message.Text);
        Assert.Contains("cesta 0007", message.Text);
        Assert.Contains("2/5", message.Text);
    }

    [Fact]
    public void ProductRejected_ShowsTheReason()
    {
        var outcome = ScanOutcome.ProductRejected(CartAssignmentResult.Full("Carrello 1 saturo"));

        var message = ScanMessageFormatter.Format("8000123", outcome)!;

        Assert.Equal(MessageSeverity.Error, message.Severity);
        Assert.Equal("Carrello 1 saturo", message.Text);
    }

    [Fact]
    public void ProcessingFailed_ShowsTheError()
    {
        var message = ScanMessageFormatter.Format("8000123", ScanOutcome.ProcessingFailed("controller PTL non risponde"))!;

        Assert.Equal(MessageSeverity.Error, message.Severity);
        Assert.Contains("controller PTL non risponde", message.Text);
    }

    [Theory]
    [InlineData(LogoutResult.Done)]
    [InlineData(LogoutResult.NoActiveSession)]
    [InlineData(LogoutResult.SystemBusy)]
    public void EveryLogoutResult_HasAText(LogoutResult result)
        => Assert.False(string.IsNullOrWhiteSpace(ScanMessageFormatter.FormatLogoutResult(result)));
}
