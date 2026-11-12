using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Tests.Support;
using PtlOrchestrator.Station.Views;

namespace PtlOrchestrator.Station.Tests.Views;

/// <summary>
/// Test della schermata: le viste WPF vere, con un ViewModel e dei doppi al posto del dominio.
/// Girano su un thread STA e senza aprire finestre, quindi funzionano anche senza interfaccia grafica attiva.
/// </summary>
public sealed class GuiTests
{
    private static readonly object ResourceLock = new();

    // ---------------------------------------------------------------- infrastruttura

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // gli URI /Assets/... delle viste si risolvono sull'assembly della Station, non su quello del test host
                lock (ResourceLock)
                {
                    try { Application.ResourceAssembly = typeof(MainWindow).Assembly; }
                    catch (InvalidOperationException) { /* già impostato da un altro test */ }
                }

                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class FakeScanActivity : IScanActivity
    {
        public event EventHandler<ScanProcessedEventArgs>? ScanProcessed;

        public void Raise() => ScanProcessed?.Invoke(this, new ScanProcessedEventArgs("x", ScanOutcome.Ignored()));
    }

    private sealed class FakeLogin : IOperatorLoginService
    {
        public Task<LoginResult> LoginWithBadgeAsync(string badgeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoginResult> LoginWithPinAsync(Guid operatorId, string pin, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken) => Task.FromResult(LogoutResult.Done);
    }

    private sealed class FakeDispatcher : IScanDispatcher
    {
        public Task<ScanOutcome> DispatchAsync(string scannedCode, CancellationToken cancellationToken) => Task.FromResult(ScanOutcome.Ignored());
    }

    private sealed class FakeCarts : ICartManager
    {
        public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool IsKnownCart(string code) => false;
        public Task<CartCheckInOutcome> CheckInAsync(string cartCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CartReleaseOutcome> ReleaseCartAsync(string cartCode, CancellationToken cancellationToken) => Task.FromResult(new CartReleaseOutcome(true, "ok"));
        public IReadOnlyList<string> ActiveCartIds() => [];
        public bool HasItems() => false;
        public Task CloseWorkAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAll(CancellationToken cancellationToken) => Task.CompletedTask;
        public void ShowStatus() { }
        public void WriteCsvReport() { }
    }

    private sealed class Harness
    {
        public FakeScanActivity Scans { get; } = new();
        public OperatorSessionContext Session { get; } = new();
        public CartContainer Container { get; }
        public MainViewModel ViewModel { get; }

        public Harness(int cartCount = 0, bool firstCartHasProduct = false)
        {
            var carts = Enumerable.Range(1, cartCount)
                .Select(n => new Cart(n.ToString(), [new Basket($"{n}01", 0), new Basket($"{n}02", 0)]));
            Container = new CartContainer(carts);

            if (firstCartHasProduct)
                Container.AssignItem("8000123", 3);

            ViewModel = new MainViewModel(
                Scans, Session, new FakeLogin(), new FakeDispatcher(), new FakeCarts(), new FakeOutbox(), new FakeLightstep(),
                Container, Options.Create(new StationOptions { StationId = "ST-GUI" }),
                new ImmediateDispatcher(), TimeProvider.System, NullLogger<MainViewModel>.Instance);
        }

        public void Login()
        {
            Session.Begin(new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Mario Rossi", DateTimeOffset.UtcNow));
            Scans.Raise();
        }
    }

    private static MainWindow CreateWindow(Harness harness)
    {
        var window = new MainWindow(harness.ViewModel, new ServiceCollection().BuildServiceProvider());
        Realize(window);
        return window;
    }

    /// <summary>Un passaggio di layout senza mostrare la finestra: attiva i collegamenti di dati e le visibilità.</summary>
    private static void Realize(Window window)
    {
        window.Measure(new Size(1000, 700));
        window.Arrange(new Rect(0, 0, 1000, 700));
        window.UpdateLayout();
        PumpDispatcher();
    }

    /// <summary>
    /// WPF attiva i collegamenti di dati in modo differito, nella coda del dispatcher: senza un ciclo di messaggi
    /// restano "Unattached". Un'invocazione a priorità bassa fa eseguire prima tutto ciò che è in coda.
    /// </summary>
    private static void PumpDispatcher()
        => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));

    private static T FindLogical<T>(MainWindow window) where T : FrameworkElement
        => ((Grid)window.Content).Children.OfType<T>().Single();

    /// <summary>La vista di lavoro dentro un contenitore misurato: nessuna finestra, ma con il layout vero.</summary>
    private static WorkView LayOutWorkView(Harness harness)
    {
        var view = new WorkView { DataContext = harness.ViewModel };
        var host = new Grid { Width = 1000, Height = 700 };
        host.Children.Add(view);
        host.Measure(new Size(1000, 700));
        host.Arrange(new Rect(0, 0, 1000, 700));
        host.UpdateLayout();
        return view;
    }

    /// <summary>IsVisible è falso se la finestra non è mai stata mostrata: si controlla la visibilità risalendo l'albero.</summary>
    private static bool IsShown(UIElement element)
    {
        DependencyObject? current = element;

        while (current is not null)
        {
            if (current is UIElement ui && ui.Visibility != Visibility.Visible)
                return false;

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
                yield return match;

            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }

    // ---------------------------------------------------------------- schermate

    [Fact]
    public void WithoutLogin_OnlyTheLoginScreenIsVisible()
    {
        RunOnSta(() =>
        {
            var window = CreateWindow(new Harness());

            Assert.Equal(Visibility.Visible, FindLogical<LoginView>(window).Visibility);
            Assert.Equal(Visibility.Collapsed, FindLogical<WorkView>(window).Visibility);
        });
    }

    [Fact]
    public void AfterLogin_TheWorkScreenReplacesTheLoginScreen()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            var window = CreateWindow(harness);

            harness.Login();
            Realize(window);

            Assert.Equal(Visibility.Collapsed, FindLogical<LoginView>(window).Visibility);
            Assert.Equal(Visibility.Visible, FindLogical<WorkView>(window).Visibility);
        });
    }

    [Fact]
    public void TheWindowOpensFullScreen()
    {
        RunOnSta(() =>
        {
            var window = CreateWindow(new Harness());

            Assert.Equal(WindowState.Maximized, window.WindowState);
        });
    }

    [Fact]
    public void LoginScreen_OffersOnlyLoginAndTheKeyboard()
    {
        RunOnSta(() =>
        {
            var view = new LoginView { DataContext = new Harness().ViewModel };
            var host = new Grid { Width = 800, Height = 600 };
            host.Children.Add(view);
            host.Measure(new Size(800, 600));
            host.Arrange(new Rect(0, 0, 800, 600));
            host.UpdateLayout();

            var buttons = Descendants<Button>(view).Select(b => b.Content as string).ToList();

            Assert.Equal(["Entra", "⌨"], buttons);
            Assert.Single(Descendants<TextBox>(view));
        });
    }

    // ---------------------------------------------------------------- schermata di lavoro

    [Fact]
    public void WorkScreen_WithNoCarts_InvitesToCheckIn()
    {
        RunOnSta(() =>
        {
            var view = LayOutWorkView(new Harness(cartCount: 0));

            var texts = Descendants<TextBlock>(view).Where(IsShown).Select(t => t.Text).ToList();

            Assert.Contains("Nessun carrello attivo", texts);
            Assert.Empty(Descendants<UniformGrid>(view).Where(IsShown));
        });
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 2)]
    public void WorkScreen_ArrangesOneToFourCarts(int carts, int expectedColumns)
    {
        RunOnSta(() =>
        {
            var view = LayOutWorkView(new Harness(cartCount: carts));

            var grid = Descendants<UniformGrid>(view).Single();
            var removeButtons = Descendants<Button>(view).Where(b => b.Content as string == "✕").ToList();

            Assert.Equal(expectedColumns, grid.Columns);
            Assert.Equal(carts, removeButtons.Count);
            Assert.Empty(Descendants<TextBlock>(view).Where(t => IsShown(t) && t.Text == "Nessun carrello attivo"));
        });
    }

    [Fact]
    public void RemoveButton_IsEnabledOnlyOnEmptyCarts()
    {
        RunOnSta(() =>
        {
            // il primo carrello contiene un prodotto, il secondo è vuoto
            var view = LayOutWorkView(new Harness(cartCount: 2, firstCartHasProduct: true));

            var removeButtons = Descendants<Button>(view).Where(b => b.Content as string == "✕").ToList();

            Assert.Equal([false, true], removeButtons.Select(b => b.IsEnabled));
        });
    }

    [Fact]
    public void WorkScreen_ShowsTheOperatorAndTheMainCommands()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            harness.Login();
            var view = LayOutWorkView(harness);

            var buttons = Descendants<Button>(view).Select(b => b.Content as string).Where(c => c is not null).ToList();
            var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToList();

            Assert.Contains("Mario Rossi", texts);
            Assert.Contains("Check-in carrello", buttons);
            Assert.Contains("Reset", buttons);
            Assert.Contains("Esci", buttons);
            Assert.Contains("⌨", buttons);
        });
    }
}
