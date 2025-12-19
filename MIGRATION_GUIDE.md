# 🔄 GUIDA MIGRAZIONE: Console App → Worker Service

## Perché Migrare?

| Feature | Console App | Worker Service |
|---------|-------------|----------------|
| **Dependency Injection** | ❌ Manuale | ✅ Automatica |
| **Logging Strutturato** | ❌ Console.WriteLine | ✅ ILogger<T> |
| **Configurazione** | ❌ Manuale | ✅ Options Pattern |
| **Testabilità** | ⚠️ Difficile | ✅ Facile |
| **Windows Service** | ❌ No | ✅ Sì |
| **Scalabilità** | ⚠️ Limitata | ✅ Ottima |

## Cosa Cambia

### 1. Program.cs

**Prima (Console App)**:
```csharp
// Program.cs con setup manuale
var configuration = new ConfigurationBuilder()...Build();
var tcpController = new TcpLightController(config);
var cartManager = new CartManager(config, tcpController);

while (true)
{
    var barcode = Console.ReadLine();
    await cartManager.ProcessBarcodeAsync(barcode);
}
```

**Dopo (Worker Service)**:
```csharp
// Program.cs con Host Builder
var builder = Host.CreateApplicationBuilder(args);

// Registra servizi nel DI container
builder.Services.Configure<CartsConfiguration>(
    builder.Configuration.GetSection("CartsConfiguration"));
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();

builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
```

### 2. Servizi con Interfacce

**Prima**:
```csharp
// Classi concrete senza interfacce
public class CartManager
{
    public CartManager(CartsConfiguration config, TcpLightController tcp)
    {
        // ...
    }
}
```

**Dopo**:
```csharp
// Interfacce + Implementazioni
public interface ICartManager { ... }
public class CartManager : ICartManager
{
    public CartManager(
        ILogger<CartManager> logger,
        IOptions<CartsConfiguration> config,
        ITcpLightController tcp)
    {
        // Tutto iniettato dal DI container!
    }
}
```

### 3. Logging

**Prima**:
```csharp
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"✓ Prodotto {barcode} → Carrello {cart}");
Console.ResetColor();
```

**Dopo**:
```csharp
_logger.LogInformation("✓ Prodotto {Barcode} → Carrello {CartNumber}", 
    barcode, cartNumber);
```

### 4. Loop Principale

**Prima**:
```csharp
// Loop manuale nel Main
while (true)
{
    var input = Console.ReadLine();
    await ProcessInput(input);
}
```

**Dopo**:
```csharp
// BackgroundService con CancellationToken
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var input = await _barcodeInput.ReadInputAsync(stoppingToken);
        await _cartManager.ProcessBarcodeAsync(input, stoppingToken);
    }
}
```

## Step-by-Step Migration

### Passo 1: Crea Interfacce

```csharp
// Services/ICartManager.cs
public interface ICartManager
{
    Task ProcessBarcodeAsync(string barcode, CancellationToken ct = default);
    void ShowStatus();
    void ResetAll();
}

// Services/ITcpLightController.cs
public interface ITcpLightController
{
    Task<bool> SendLightOnCommandAsync(int cartIndex, CancellationToken ct = default);
}
```

### Passo 2: Aggiungi ILogger alle Classi

```csharp
public class CartManager : ICartManager
{
    private readonly ILogger<CartManager> _logger;
    
    public CartManager(ILogger<CartManager> logger, ...)
    {
        _logger = logger;
        // ...
    }
    
    // Sostituisci tutti i Console.WriteLine con _logger.LogXxx()
}
```

### Passo 3: Usa Options Pattern

```csharp
// Prima
public CartManager(CartsConfiguration config)
{
    _config = config;
}

// Dopo
public CartManager(IOptions<CartsConfiguration> config)
{
    _config = config.Value;
}
```

### Passo 4: Crea Worker.cs

```csharp
public class Worker : BackgroundService
{
    private readonly ICartManager _cartManager;
    
    public Worker(ICartManager cartManager)
    {
        _cartManager = cartManager;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Loop logica qui
        }
    }
}
```

### Passo 5: Setup DI in Program.cs

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CartsConfiguration>(
    builder.Configuration.GetSection("CartsConfiguration"));

builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
```

### Passo 6: Aggiorna .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Compatibilità Codice Esistente

### ✅ Rimane Uguale
- Logica business (algoritmo round-robin)
- Comunicazione TCP
- Struttura dati in memoria
- Comandi console (status, reset, exit)

### 🔄 Cambia Solo l'Infrastruttura
- Come i servizi vengono creati (DI invece di `new`)
- Come si fa logging (ILogger invece di Console)
- Come si legge config (Options invece di manual binding)
- Come gira il loop (BackgroundService invece di while)

## Vantaggi Immediati

### 1. Testabilità
```csharp
// Ora puoi scrivere unit test facilmente!
var mockTcp = new Mock<ITcpLightController>();
var manager = new CartManager(mockLogger, mockConfig, mockTcp.Object);
await manager.ProcessBarcodeAsync("12345");

mockTcp.Verify(x => x.SendLightOnCommandAsync(0), Times.Once);
```

### 2. Logging Ricercabile
```bash
# Prima
Prodotto assegnato al carrello 1

# Dopo - log strutturato
{"level":"Information","message":"Prodotto assegnato","Barcode":"12345","CartNumber":1}

# Puoi cercare: "Mostrami tutti i log per Barcode=12345"
```

### 3. Configurazione Flessibile
```json
// Development
{
  "CartsConfiguration": {
    "CartIpAddresses": ["127.0.0.1", "127.0.0.1"]
  }
}

// Production
{
  "CartsConfiguration": {
    "CartIpAddresses": ["192.168.1.101", "192.168.1.102"]
  }
}
```

### 4. Windows Service (Production)
```bash
# Installa come servizio
sc create BarcodeCartManager binPath="C:\App\BarcodeCartManager.exe"

# Avvio automatico
sc config BarcodeCartManager start=auto

# Parte al boot, gira sempre in background
```

## Esempio Completo: Migrazione di un Metodo

### Prima (Console App)
```csharp
public async Task ProcessBarcodeAsync(string barcode)
{
    if (string.IsNullOrWhiteSpace(barcode))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ Barcode non valido");
        Console.ResetColor();
        throw new ArgumentException("Barcode non valido");
    }

    var cartIndex = AssignCart(barcode);
    var success = await _tcpController.SendCommand(cartIndex);

    if (success)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Prodotto {barcode} → Carrello {cartIndex + 1}");
        Console.ResetColor();
    }
}
```

### Dopo (Worker Service)
```csharp
public async Task ProcessBarcodeAsync(
    string barcode, 
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(barcode))
    {
        _logger.LogWarning("Barcode non valido ricevuto");
        throw new ArgumentException("Barcode non valido", nameof(barcode));
    }

    var cartIndex = AssignCart(barcode);
    var success = await _tcpController.SendLightOnCommandAsync(
        cartIndex, 
        cancellationToken);

    if (success)
    {
        _logger.LogInformation(
            "✓ Prodotto {Barcode} → Carrello {CartNumber} ({IpAddress})",
            barcode,
            cartIndex + 1,
            _config.CartIpAddresses[cartIndex]);
    }
}
```

## Checklist Migrazione

- [ ] Crea interfacce per tutti i servizi
- [ ] Aggiungi ILogger a tutte le classi
- [ ] Sostituisci Console.WriteLine con ILogger
- [ ] Usa IOptions per configurazione
- [ ] Crea Worker.cs (BackgroundService)
- [ ] Setup DI in Program.cs
- [ ] Aggiungi CancellationToken ai metodi async
- [ ] Aggiorna .csproj a SDK.Worker
- [ ] Testa in modalità console
- [ ] (Opzionale) Installa come Windows Service

## FAQ Migrazione

**Q: Devo riscrivere tutto?**  
R: NO! Solo l'infrastruttura. La logica business rimane identica.

**Q: Quanto tempo ci vuole?**  
R: Per un progetto piccolo come questo: 1-2 ore.

**Q: Perdo funzionalità?**  
R: NO! Guadagni solo funzionalità (testabilità, logging, DI).

**Q: Posso ancora usare la console?**  
R: SÌ! Fa `dotnet run` come prima.

**Q: È retrocompatibile?**  
R: L'utilizzo finale (scansione barcode) è identico.

## Risultato Finale

### Cosa Cambia per l'Utente: NIENTE!
```
Barcode > 8001234567890
✓ NUOVO prodotto → Carrello 1

Barcode > status
[mostra stato]
```

### Cosa Cambia per lo Sviluppatore: TUTTO!
- ✅ Codice più pulito e professionale
- ✅ Facilmente testabile con unit test
- ✅ Logging strutturato per debugging
- ✅ Può diventare Windows Service
- ✅ Scalabile per future feature

---

**Conclusione**: La migrazione richiede poco sforzo ma porta benefici enormi per manutenibilità, testabilità e professionalità del codice.
