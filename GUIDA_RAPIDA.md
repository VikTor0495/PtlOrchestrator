# 🚀 GUIDA RAPIDA - Worker Service

## Cosa è cambiato rispetto alla Console App?

### ✅ Vantaggi del Worker Service

| Feature | Adesso | Prima |
|---------|--------|-------|
| **Dependency Injection** | ✅ Automatica (come ASP.NET) | ❌ Manuale |
| **Logging** | ✅ `ILogger<T>` strutturato | ❌ `Console.WriteLine()` |
| **Configurazione** | ✅ Options Pattern | ❌ Lettura manuale |
| **Windows Service** | ✅ Può girare come servizio | ❌ Solo console |
| **Testabilità** | ✅ Mock facili | ❌ Difficile |
| **Professionalità** | ✅ Enterprise-ready | ❌ Basic |

## 🎯 Architettura

```
Program.cs (Host Builder)
    ↓ registra servizi nel DI Container
Worker.cs (BackgroundService)
    ↓ riceve servizi via DI
CartManager + TcpController + BarcodeInput
    ↓ tutti con ILogger e IOptions
Carrelli TCP
```

## 🏃 Quick Start

### 1. Build
```bash
dotnet build
```

### 2. Run
```bash
dotnet run
```

### 3. Usa come prima!
```
Barcode > 8001234567890
✓ NUOVO prodotto → Carrello 1

Barcode > status
[mostra stato]

Barcode > exit
[chiude applicazione]
```

## 📊 Dependency Injection Spiegata Semplice

### Registrazione Servizi (Program.cs)
```csharp
// Dici al sistema: "Quando qualcuno chiede ICartManager, dai CartManager"
builder.Services.AddSingleton<ICartManager, CartManager>();
```

### Injection Automatica (Worker.cs)
```csharp
// Il sistema inietta automaticamente le dipendenze!
public Worker(ILogger<Worker> logger, ICartManager cartManager)
{
    _logger = logger;           // ← Iniettato automaticamente
    _cartManager = cartManager; // ← Iniettato automaticamente
}
```

### Vantaggi
✅ **Nessun `new`**: Il framework crea tutto  
✅ **Testabile**: Puoi sostituire con mock nei test  
✅ **Centralizzato**: Tutta la configurazione in un posto  

## 🔧 Configurazione

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",      ← Log normali
      "BarcodeCartManager": "Debug"  ← Tutti i tuoi log in dettaglio
    }
  },
  "CartsConfiguration": {
    "NumberOfCarts": 6,
    "CartIpAddresses": [...],
    "TcpPort": 5000
  }
}
```

### Options Pattern
Il sistema legge automaticamente la configurazione e la inietta:
```csharp
// In CartManager
public CartManager(IOptions<CartsConfiguration> config)
{
    _config = config.Value; // ← Binding automatico da appsettings.json!
}
```

## 📝 Logging Strutturato

### Prima (Console App)
```csharp
Console.WriteLine($"Prodotto {barcode} assegnato al carrello {cart}");
```

### Adesso (Worker Service)
```csharp
_logger.LogInformation("Prodotto {Barcode} assegnato al Carrello {CartNumber}", 
    barcode, cartNumber);
```

### Vantaggi
✅ **Parametri strutturati**: Puoi filtrare/cercare nei log  
✅ **Livelli**: Debug, Info, Warning, Error, Critical  
✅ **Sink multipli**: Console, File, Database, Azure  
✅ **Professionale**: Standard industry  

## 🪟 Installare come Windows Service

### 1. Pubblica
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### 2. Installa
```powershell
sc create BarcodeCartManager binPath="C:\Path\To\BarcodeCartManager.exe"
sc start BarcodeCartManager
```

### 3. Sempre attivo!
Il servizio:
- ✅ Si avvia automaticamente al boot
- ✅ Gira in background
- ✅ Riavvia automaticamente se crasha
- ✅ Non serve console aperta

## 🧪 Testing Facile

### Prima (Console App)
```csharp
// Difficile testare perché CartManager crea TcpController internamente
var manager = new CartManager(...); // Come faccio a mockare TCP?
```

### Adesso (Worker Service)
```csharp
// Facile! Inietto un mock
var mockTcp = new Mock<ITcpLightController>();
var manager = new CartManager(mockLogger, mockConfig, mockTcp.Object);

// Test isolato senza rete reale!
await manager.ProcessBarcodeAsync("12345");
mockTcp.Verify(x => x.SendLightOnCommandAsync(0), Times.Once);
```

## 🎓 Concetti Chiave

### 1. Dependency Injection (DI)
**Problema**: Accoppiamento forte, difficile da testare  
**Soluzione**: Le dipendenze vengono iniettate dall'esterno

### 2. Interface Segregation
**Problema**: Dipendenze concrete, impossibili da mockare  
**Soluzione**: Dipendi da interfacce (`ICartManager`, non `CartManager`)

### 3. Options Pattern
**Problema**: Configurazione sparsa, difficile da gestire  
**Soluzione**: Classe POCO tipizzata + validation automatica

### 4. Structured Logging
**Problema**: Log come stringhe, impossibili da query  
**Soluzione**: Log con parametri strutturati, query/filtrabili

## 📦 File Importanti

| File | Scopo |
|------|-------|
| `Program.cs` | Setup DI container + Host |
| `Worker.cs` | Loop principale (BackgroundService) |
| `Services/` | Tutti i servizi con interfacce |
| `appsettings.json` | Configurazione (IP, log levels, ecc.) |

## 🔄 Lifecycle

```
1. Program.cs → Configura Host + DI
2. Host.Run() → Avvia l'applicazione
3. Worker.StartAsync() → Inizializza Worker
4. Worker.ExecuteAsync() → Loop principale
   ├─ Legge barcode
   ├─ Chiama CartManager
   └─ CartManager chiama TcpController
5. CTRL+C → CancellationToken triggered
6. Worker.StopAsync() → Graceful shutdown
```

## 🆘 FAQ

**Q: È più complicato della Console App?**  
R: Inizialmente sì, ma è MOLTO più professionale e manutenibile.

**Q: Devo usare sempre il Worker Service?**  
R: Per progetti seri SÌ. Per script veloci va bene la Console App.

**Q: Come debug le dipendenze iniettate?**  
R: Metti breakpoint nel constructor, vedrai cosa viene iniettato.

**Q: Posso ancora usarlo in modalità console?**  
R: SÌ! Fa `dotnet run` come prima. Può diventare Windows Service SE vuoi.

**Q: Vale la pena imparare tutto questo?**  
R: ASSOLUTAMENTE SÌ. È lo standard per applicazioni .NET professionali.

## 🎯 Differenze Pratiche

### Registrazione Servizio
**Prima**: 
```csharp
var tcp = new TcpController(config);
var manager = new CartManager(config, tcp);
```

**Adesso**:
```csharp
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();
builder.Services.AddSingleton<ICartManager, CartManager>();
// Il framework li crea e inietta automaticamente!
```

### Logging
**Prima**: 
```csharp
Console.WriteLine("Errore!");
```

**Adesso**:
```csharp
_logger.LogError("Errore in {Operation} per {Barcode}", operation, barcode);
// Log strutturato, filtrable, professionale
```

### Configurazione
**Prima**: 
```csharp
var config = new ConfigurationBuilder()...Build();
var cartConfig = config.GetSection("CartsConfiguration").Get<CartsConfiguration>();
```

**Adesso**:
```csharp
builder.Services.Configure<CartsConfiguration>(
    builder.Configuration.GetSection("CartsConfiguration"));
// Options Pattern automatico!
```

## 🚀 Prossimi Passi

1. ✅ **Usa così com'è**: Funziona come la Console App ma meglio
2. 📊 **Aggiungi Unit Tests**: Approfitta della testabilità
3. 📁 **Log su file**: Aggiungi Serilog (vedi README)
4. 🪟 **Installa come servizio**: Per produzione
5. 🎯 **Estendi**: Database, API, Dashboard

---

**Pro Tip**: Inizia a usarlo subito anche se non capisci tutto al 100%. L'architettura diventerà chiara col tempo! 💡
