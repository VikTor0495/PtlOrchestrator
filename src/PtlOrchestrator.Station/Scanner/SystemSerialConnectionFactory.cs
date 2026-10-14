using System.IO.Ports;
using PtlOrchestrator.Station.Configuration;

namespace PtlOrchestrator.Station.Scanner;

/// <summary>Apre le vere porte COM di Windows tramite <see cref="SerialPort"/>.</summary>
public sealed class SystemSerialConnectionFactory : ISerialConnectionFactory
{
    public ISerialConnection Open(SerialScannerOptions options)
    {
        var port = new SerialPort(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits);

        try
        {
            port.Open();
        }
        catch
        {
            port.Dispose();
            throw;
        }

        return new SystemSerialConnection(port);
    }

    private sealed class SystemSerialConnection(SerialPort port) : ISerialConnection
    {
        private readonly SerialPort _port = port;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            // la lettura asincrona di SerialPort non onora l'annullamento: si chiude la porta per sbloccarla
            using var registration = cancellationToken.Register(() => _port.Dispose());

            try
            {
                return await _port.BaseStream.ReadAsync(buffer, cancellationToken);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        public void Dispose() => _port.Dispose();
    }
}
