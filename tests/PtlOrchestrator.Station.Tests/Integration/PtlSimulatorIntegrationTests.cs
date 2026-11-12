using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Builder;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Station.Tests.Integration;

/// <summary>
/// Test di integrazione: la libreria Lightstep reale (AioiSystems.Lightstep) parla con il simulatore
/// del controller PTL (tools/ptl-simulator), avviato come processo Python. Il simulatore ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¨ costruito
/// dal protocollo documentato dal fornitore, non dal codice di Reflexer.
///
/// Richiede Python 3 (variabile PTL_SIMULATOR_PYTHON, di default "python") e il simulatore nel repo.
/// </summary>
[Collection("PtlSimulator")]
public sealed class PtlSimulatorIntegrationTests
{
    // comando conforme al documento "PP505 command creating process": m1 rosso acceso, m2 verde, m3 rosso lampeggiante
    private static string ConformantCommand(string address, string display = "00009")
        => "PP5050000m1" + (char)50 + (char)17 + (char)33
         + "m2" + (char)49 + (char)33 + (char)33
         + "m3" + (char)51 + (char)17 + (char)33
         + address + display;

    [Fact(Skip = "Non affidabile: la DLL Lightstep a volte non completa Connect se chiamata da un thread del pool (non raggiunge nemmeno il simulatore). Da indagare sul campo. Vedi Roadmap 5.11")]
    public async Task ConformantCommand_TurnsTheModuleRedOnTheController()
    {
        using var sim = SimulatorProcess.Start();
        var service = CreateService();

        await Task.Run(() => service.SendRawAsync(ConformantCommand("0001"), sim.Endpoint, CancellationToken.None));

        var lamp = (await sim.StateAsync()).GetProperty("lamps").GetProperty("0001");
        Assert.Equal("rosso", lamp.GetProperty("color").GetString());
        Assert.Equal("00009", lamp.GetProperty("display").GetString());
    }

    [Fact(Skip = "Non affidabile: la DLL Lightstep a volte non completa Connect se chiamata da un thread del pool (non raggiunge nemmeno il simulatore). Da indagare sul campo. Vedi Roadmap 5.11")]
    public async Task BuilderCommand_IsRejectedByTheController()
    {
        // Pp505Builder non rispetta il formato a blocchi da 3 byte dei manuali: il controller risponde NAK
        using var sim = SimulatorProcess.Start(strict: true);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.Run(() => service.SendRawAsync(Pp505Builder.BuildGreenCommand(["0001"]), sim.Endpoint, CancellationToken.None)));

        Assert.Contains("NAK", ex.Message);
    }

    [Fact(Skip = "Non affidabile: la DLL Lightstep a volte non completa Connect se chiamata da un thread del pool (non raggiunge nemmeno il simulatore). Da indagare sul campo. Vedi Roadmap 5.11")]
    public async Task ConfirmPressed_OnTheController_IsReturnedToTheStation()
    {
        using var sim = SimulatorProcess.Start();
        var service = CreateService();
        await Task.Run(() => service.SendRawAsync(ConformantCommand("0002"), sim.Endpoint, CancellationToken.None));

        await sim.ControlAsync(new { op = "press", address = "0002" });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var module = await Task.Run(() => service.WaitForButtonAsync("0002", sim.Endpoint, timeout.Token));
        Assert.Equal("0002", module);
    }

    [Fact(Skip = "Non affidabile: la DLL Lightstep a volte non completa Connect se chiamata da un thread del pool (non raggiunge nemmeno il simulatore). Da indagare sul campo. Vedi Roadmap 5.11")]
    public async Task TwoCarts_OnTwoControllers_EachModuleLightsOnlyOnItsOwnController()
    {
        using var first = SimulatorProcess.Start();
        using var second = SimulatorProcess.Start();
        var service = CreateService();

        await Task.Run(() => service.SendRawAsync(ConformantCommand("0001"), first.Endpoint, CancellationToken.None));
        await Task.Run(() => service.SendRawAsync(ConformantCommand("0003"), second.Endpoint, CancellationToken.None));

        var firstLamps = (await first.StateAsync()).GetProperty("lamps");
        var secondLamps = (await second.StateAsync()).GetProperty("lamps");
        Assert.True(firstLamps.TryGetProperty("0001", out _));
        Assert.False(firstLamps.TryGetProperty("0003", out _));
        Assert.True(secondLamps.TryGetProperty("0003", out _));
        Assert.False(secondLamps.TryGetProperty("0001", out _));
    }

    private static LightstepPtlCommandService CreateService()
    {
        var options = Options.Create(new LightstepOptions { LicenzeKey = string.Empty, ConnectionTimeout = 3000 });
        var connections = new LightstepConnectionService(options, NullLogger<LightstepConnectionService>.Instance);
        return new LightstepPtlCommandService(connections, NullLogger<LightstepPtlCommandService>.Instance);
    }

    /// <summary>Simulatore Python avviato in un processo separato, su porte libere.</summary>
    private sealed class SimulatorProcess : IDisposable
    {
        private readonly Process _process;
        private readonly int _controlPort;

        private SimulatorProcess(Process process, int port, int controlPort)
        {
            _process = process;
            Endpoint = new PtlEndpoint("127.0.0.1", port);
            _controlPort = controlPort;
        }

        public PtlEndpoint Endpoint { get; }

        public static SimulatorProcess Start(bool strict = false)
        {
            var port = FreePort();
            var controlPort = FreePort();
            var script = Path.Combine(FindRepositoryRoot(), "tools", "ptl-simulator", "simulator.py");
            var python = PythonExecutable();

            var info = new ProcessStartInfo
            {
                FileName = python,
                ArgumentList = { script, "--headless", "--port", port.ToString(), "--control-port", controlPort.ToString() },
                UseShellExecute = false,
                CreateNoWindow = true
                // niente redirezione dell'output: nessuno lo leggerebbe e un buffer pieno fermerebbe il simulatore
            };

            if (strict)
                info.ArgumentList.Add("--strict");

            var process = Process.Start(info) ?? throw new InvalidOperationException("Impossibile avviare il simulatore Python");

            var sim = new SimulatorProcess(process, port, controlPort);
            sim.WaitUntilListening(port);
            return sim;
        }

        public async Task<JsonElement> StateAsync()
            => await ControlAsync(new { op = "state" });

        public async Task<JsonElement> ControlAsync(object request)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _controlPort);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            var line = await reader.ReadLineAsync() ?? throw new IOException("nessuna risposta dal simulatore");
            using var document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }

        public void Dispose()
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            _process.Dispose();
        }

        private void WaitUntilListening(int port)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var probe = new TcpClient();
                    probe.Connect(IPAddress.Loopback, port);
                    return;
                }
                catch (SocketException)
                {
                    Thread.Sleep(100);
                }
            }

            throw new TimeoutException($"Il simulatore non risponde sulla porta {port}");
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        // "python" nel PATH puÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â² essere lo stub di Microsoft Store, che non esegue script: si preferisce l'installazione reale
        private static string PythonExecutable()
        {
            var configured = Environment.GetEnvironmentVariable("PTL_SIMULATOR_PYTHON");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python312-arm64", "python.exe");
            return System.IO.File.Exists(local) ? local : "python";
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "PtlOrchestrator.sln")))
                directory = directory.Parent;

            return directory?.FullName ?? throw new DirectoryNotFoundException("Radice del repository non trovata");
        }
    }
}

[CollectionDefinition("PtlSimulator", DisableParallelization = true)]
public sealed class PtlSimulatorCollection;
