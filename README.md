# 🛒 Barcode Cart Manager - Worker Service

Sistema professionale di gestione carrelli con controllo lampadine via TCP basato su lettura barcode.

**Architettura**: .NET 8 Worker Service con Dependency Injection, ILogger strutturato e Options Pattern.

## 🎯 Caratteristiche Architetturali

### ✅ Dependency Injection
- **Tutti i servizi iniettati**: Nessun `new` nel codice
- **Interfacce per tutto**: Testabilità massima
- **Lifetime gestito**: Singleton per servizi stateful

### ✅ Logging Professionale
- **ILogger<T> strutturato**: Log con context e correlation ID
- **Livelli configurabili**: Debug, Info, Warning, Error, Critical
- **Multipli sink supportati**: Console, File, Application Insights

### ✅ Configuration Management
- **Options Pattern**: `IOptions<CartsConfiguration>`
- **Hot-reload supportato**: Modifica config senza restart
- **User Secrets**: Per dati sensibili in Development

### ✅ Lifecycle Management
- **BackgroundService**: Gestione automatica start/stop
- **Graceful Shutdown**: CancellationToken in tutto il codice
- **Windows Service Ready**: Può girare come servizio Windows

## 📦 Struttura Progetto

```
BarcodeCartManager/
├── Program.cs                           # Host Builder + DI setup
├── Worker.cs                            # BackgroundService principale
├── CartsConfiguration.cs                # Modello configurazione
├── Services/
│   ├── ICartManager.cs                  # Interface gestore carrelli
│   ├── CartManager.cs                   # Implementazione con ILogger
│   ├── ITcpLightController.cs           # Interface controller TCP
│   ├── TcpLightController.cs            # Implementazione TCP
│   ├── IBarcodeInputService.cs          # Interface input barcode
│   └── ConsoleBarcodeInputService.cs    # Implementazione console
├── appsettings.json                     # Configurazione principale
├── appsettings.Development.json         # Config per sviluppo
└── BarcodeCartManager.csproj            # File progetto
```

## 🚀 Quick Start

### 1. Installa .NET 8 SDK
```bash
dotnet --version  # Verifica che sia >= 8.0
```

### 2. Configura IP Carrelli
Modifica `appsettings.json`:
```json
{
  "CartsConfiguration": {
    "NumberOfCarts": 6,
    "CartIpAddresses": [
      "192.168.1.101",  ← Modifica questi IP
      "192.168.1.102",
      "192.168.1.103",
      "192.168.1.104",
      "192.168.1.105",
      "192.168.1.106"
    ],
    "TcpPort": 5000,
    "LightOnCommand": "LIGHT_ON",
    "TcpTimeoutMs": 3000
  }
}
```

### 3. Build e Run
```bash
# Restore dipendenze
dotnet restore

# Build
dotnet build

# Run in modalità console
dotnet run

# Run con hot-reload (Development)
dotnet watch run
```

## 🎨 Dependency Injection in Action

### Registration (Program.cs)
```csharp
// Servizi applicativi registrati nel DI container
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();
builder.Services.AddSingleton<IBarcodeInputService, ConsoleBarcodeInputService>();
```

### Injection (Worker.cs)
```csharp
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ICartManager _cartManager;
    private readonly IBarcodeInputService _barcodeInput;

    // Constructor Injection - tutto automatico!
    public Worker(
        ILogger<Worker> logger,
        ICartManager cartManager,
        IBarcodeInputService barcodeInput)
    {
        _logger = logger;
        _cartManager = cartManager;
        _barcodeInput = barcodeInput;
    }
}
```

## 📊 Logging Strutturato

### Esempi di Log
```csharp
// Log con parametri strutturati
_logger.LogInformation("Nuovo barcode registrato: {Barcode} → Carrello {CartNumber}", 
    barcode, cartIndex + 1);

// Log errori con exception
_logger.LogError(ex, "Errore TCP verso Carrello {CartNumber}", cartIndex);

// Log con livelli diversi
_logger.LogDebug("Connessione a {IpAddress}:{Port}...", ipAddress, port);
_logger.LogWarning("Timeout connessione a {IpAddress}", ipAddress);
_logger.LogCritical("Configurazione non valida!");
```

### Configurazione Livelli (appsettings.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BarcodeCartManager": "Debug",           ← Tutti i tuoi log
      "Microsoft.Hosting.Lifetime": "Warning"  ← Log framework
    }
  }
}
```

## ⚙️ Configuration con Options Pattern

### Binding Automatico
```csharp
// In Program.cs - registrazione
builder.Services.Configure<CartsConfiguration>(
    builder.Configuration.GetSection("CartsConfiguration"));

// In CartManager.cs - injection
public CartManager(IOptions<CartsConfiguration> config)
{
    _config = config.Value;  // Binding automatico!
}
```

### Hot-Reload Supportato
```json
// Modifica appsettings.json mentre l'app gira
"TcpTimeoutMs": 5000  ← Cambia questo

// Riavvia automaticamente (se configurato)
```

## 🔧 Testabilità con DI

### Unit Test Esempio
```csharp
[Fact]
public async Task ProcessBarcode_NewBarcode_AssignsCorrectly()
{
    // Arrange - Mock delle dipendenze
    var mockLogger = new Mock<ILogger<CartManager>>();
    var mockTcp = new Mock<ITcpLightController>();
    var mockOptions = Options.Create(new CartsConfiguration 
    { 
        NumberOfCarts = 3,
        CartIpAddresses = new[] { "192.168.1.1", "192.168.1.2", "192.168.1.3" }
    });

    var cartManager = new CartManager(mockLogger.Object, mockOptions, mockTcp.Object);

    // Act
    await cartManager.ProcessBarcodeAsync("12345");

    // Assert
    var cartNumber = cartManager.GetCartNumberForBarcode("12345");
    Assert.Equal(1, cartNumber);
    mockTcp.Verify(x => x.SendLightOnCommandAsync(0, It.IsAny<CancellationToken>()), Times.Once);
}
```

## 🪟 Installazione come Windows Service

### 1. Pubblica come Self-Contained
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

### 2. Installa il Servizio
```powershell
# Usa sc.exe o PowerShell
New-Service -Name "BarcodeCartManager" `
    -BinaryPathName "C:\Path\To\BarcodeCartManager.exe" `
    -DisplayName "Barcode Cart Manager" `
    -StartupType Automatic
```

### 3. Configura Program.cs per Windows Service
```csharp
var builder = Host.CreateApplicationBuilder(args);

// Aggiungi supporto Windows Services
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BarcodeCartManager";
});

// ... resto della configurazione
```

## 📈 Advanced Features

### 1. File Logging
Aggiungi package:
```bash
dotnet add package Serilog.Extensions.Hosting
dotnet add package Serilog.Sinks.File
```

Configura in Program.cs:
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/barcode-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddSerilog(dispose: true));
```

### 2. Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<CartHealthCheck>("carts");

// Endpoint HTTP per health check
app.MapHealthChecks("/health");
```

### 3. Application Insights (Azure)
```bash
dotnet add package Microsoft.ApplicationInsights.WorkerService
```

```csharp
builder.Services.AddApplicationInsightsTelemetryWorkerService();
```

### 4. Database Persistenza
```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

```csharp
builder.Services.AddDbContext<BarcodeDbContext>(options =>
    options.UseSqlServer(connectionString));
```

## 🔍 Troubleshooting

### Problema: "Unable to resolve service"
**Causa**: Servizio non registrato nel DI container  
**Soluzione**: Verifica registrazione in Program.cs:
```csharp
builder.Services.AddSingleton<IYourService, YourService>();
```

### Problema: "Configuration section 'CartsConfiguration' not found"
**Causa**: appsettings.json non copiato o formato errato  
**Soluzione**: Verifica `.csproj`:
```xml
<None Update="appsettings.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### Problema: Logger non scrive nulla
**Causa**: Livello di log troppo alto  
**Soluzione**: Imposta "Debug" in appsettings.json

## 🆚 Console App vs Worker Service

| Aspetto | Console App | Worker Service |
|---------|-------------|----------------|
| **DI** | Manuale | ✅ Built-in |
| **Logging** | Console.WriteLine | ✅ ILogger<T> |
| **Config** | Manuale | ✅ Options Pattern |
| **Testabilità** | Media | ✅ Ottima |
| **Windows Service** | ❌ No | ✅ Sì |
| **Professionalità** | Basic | ✅ Enterprise |

## 📚 Best Practices Implementate

✅ **SOLID Principles**: Interface segregation, Dependency inversion  
✅ **Async/Await**: Tutto asincrono dove possibile  
✅ **CancellationToken**: Graceful shutdown ovunque  
✅ **Structured Logging**: Log ricchi di context  
✅ **Configuration Management**: Options Pattern  
✅ **Error Handling**: Try-catch con logging appropriato  
✅ **Thread Safety**: Lock dove necessario  
✅ **Resource Disposal**: Using statements per IDisposable  

## 📖 Documentazione Ufficiale

- [.NET Worker Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Options Pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Logging in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)

## 🎓 Prossimi Step Suggeriti

1. **Aggiungi Unit Tests**: Con xUnit + Moq
2. **Implementa Health Checks**: Per monitoring
3. **Aggiungi Metrics**: Con OpenTelemetry
4. **Database Persistenza**: Per storico operazioni
5. **Web Dashboard**: Con Blazor o Angular

---

**Versione**: 2.0 (Worker Service)  
**Framework**: .NET 8.0  
**Architettura**: Clean Architecture con DI
