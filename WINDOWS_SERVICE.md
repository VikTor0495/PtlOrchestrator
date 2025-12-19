# 🪟 INSTALLAZIONE COME WINDOWS SERVICE

## Perché Installare come Servizio?

✅ **Avvio automatico al boot**  
✅ **Gira in background** (non serve console aperta)  
✅ **Riavvio automatico** in caso di crash  
✅ **Gestito da Windows** (Services.msc)  
✅ **Log centralizzati** (Event Viewer)  

## Prerequisiti

1. .NET 8 Runtime installato
2. Permessi amministratore sul PC
3. Applicazione compilata e testata

## Step 1: Pubblica l'Applicazione

### Opzione A: Self-Contained (Consigliata)
Include il runtime .NET, non serve installare .NET sul server:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin\Release\net8.0\win-x64\publish\BarcodeCartManager.exe`

### Opzione B: Framework-Dependent
Richiede .NET 8 installato sul server:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## Step 2: Copia File sul Server

Copia la cartella `publish` in una posizione permanente:

```
C:\Program Files\BarcodeCartManager\
├── BarcodeCartManager.exe
├── appsettings.json
└── ... (altre DLL se framework-dependent)
```

**⚠️ IMPORTANTE**: Configura `appsettings.json` con gli IP reali PRIMA di installare!

## Step 3: Installa il Servizio

### Metodo A: sc.exe (Command Prompt)

```cmd
REM Apri CMD come Amministratore

REM Crea il servizio
sc create BarcodeCartManager ^
    binPath= "C:\Program Files\BarcodeCartManager\BarcodeCartManager.exe" ^
    DisplayName= "Barcode Cart Manager" ^
    start= auto

REM Descrizione servizio
sc description BarcodeCartManager "Sistema gestione carrelli con barcode e controllo luci TCP"

REM Avvia il servizio
sc start BarcodeCartManager
```

### Metodo B: PowerShell

```powershell
# Apri PowerShell come Amministratore

# Crea il servizio
New-Service -Name "BarcodeCartManager" `
    -BinaryPathName "C:\Program Files\BarcodeCartManager\BarcodeCartManager.exe" `
    -DisplayName "Barcode Cart Manager" `
    -Description "Sistema gestione carrelli con barcode e controllo luci TCP" `
    -StartupType Automatic

# Avvia il servizio
Start-Service -Name "BarcodeCartManager"
```

### Metodo C: NSSM (Non-Sucking Service Manager)

NSSM è un tool che semplifica la gestione dei servizi Windows.

1. **Scarica NSSM**: https://nssm.cc/download
2. **Estrai** in `C:\Tools\nssm\`
3. **Installa servizio**:

```cmd
C:\Tools\nssm\nssm.exe install BarcodeCartManager "C:\Program Files\BarcodeCartManager\BarcodeCartManager.exe"
```

4. **Configura GUI**: NSSM aprirà una finestra con opzioni:
   - Application tab: path exe
   - Details tab: nome, descrizione
   - Log on tab: account di esecuzione
   - I/O tab: redirezione stdout/stderr

5. **Avvia**:
```cmd
C:\Tools\nssm\nssm.exe start BarcodeCartManager
```

## Step 4: Verifica Installazione

### Tramite Services.msc

1. Premi `Win + R`
2. Digita `services.msc`
3. Cerca "Barcode Cart Manager"
4. Verifica:
   - Status: Running
   - Startup Type: Automatic

### Tramite Command Line

```cmd
REM Verifica stato
sc query BarcodeCartManager

REM Output atteso:
REM STATE: 4 RUNNING
```

### Tramite PowerShell

```powershell
Get-Service -Name "BarcodeCartManager" | Format-List

# Output atteso:
# Status : Running
# StartType : Automatic
```

## Step 5: Configura Riavvio Automatico

### In caso di crash, riavvia automaticamente:

```cmd
sc failure BarcodeCartManager reset= 86400 actions= restart/60000/restart/60000/restart/60000

REM Spiegazione:
REM reset=86400 → Reset contatore dopo 1 giorno
REM actions → Riavvia dopo 60 secondi (3 tentativi)
```

Oppure tramite GUI (services.msc):
1. Doppio click sul servizio
2. Tab "Recovery"
3. Configura azioni:
   - First failure: Restart the Service
   - Second failure: Restart the Service
   - Subsequent failures: Restart the Service
   - Restart service after: 1 minute

## Gestione Quotidiana

### Avvio
```cmd
sc start BarcodeCartManager
# oppure
net start BarcodeCartManager
```

### Stop
```cmd
sc stop BarcodeCartManager
# oppure
net stop BarcodeCartManager
```

### Riavvio
```cmd
sc stop BarcodeCartManager && sc start BarcodeCartManager
# oppure
Restart-Service -Name "BarcodeCartManager"
```

### Verifica Log
```cmd
REM Event Viewer
eventvwr.msc

REM Cerca in:
REM Windows Logs > Application
REM Source: BarcodeCartManager
```

## Disinstallazione

```cmd
REM Stop il servizio
sc stop BarcodeCartManager

REM Disinstalla
sc delete BarcodeCartManager

REM Elimina file (opzionale)
rmdir /s "C:\Program Files\BarcodeCartManager"
```

## Configurazione Logging per Servizio

### Modifica appsettings.json per logging su file:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "File": {
      "Path": "C:\\ProgramData\\BarcodeCartManager\\logs\\app.log",
      "MinLevel": "Information"
    }
  }
}
```

### Installa Serilog per file logging:

```bash
dotnet add package Serilog.Extensions.Hosting
dotnet add package Serilog.Sinks.File
```

### Configura in Program.cs:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: @"C:\ProgramData\BarcodeCartManager\logs\app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.EventLog(
        source: "BarcodeCartManager",
        manageEventSource: false) // Richiede permessi admin
    .CreateLogger();

builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddSerilog(dispose: true));
```

## Troubleshooting

### Servizio non parte

**Problema**: Errore 1053 "The service did not respond in a timely fashion"  
**Causa**: Timeout durante startup  
**Soluzione**: 
```cmd
sc config BarcodeCartManager start= delayed-auto
```

### Errore permissions

**Problema**: Access denied  
**Soluzione**: Esegui sotto account con permessi:
```cmd
sc config BarcodeCartManager obj= "DOMAIN\ServiceAccount" password= "password"
```

### Configurazione non trovata

**Problema**: appsettings.json non trovato  
**Soluzione**: 
1. Verifica che sia nella stessa cartella dell'exe
2. Verifica permessi lettura file

### Non riesce a connettersi ai carrelli

**Problema**: Firewall blocca connessioni TCP  
**Soluzione**:
```cmd
REM Aggiungi regola firewall
netsh advfirewall firewall add rule name="BarcodeCartManager" dir=out action=allow program="C:\Program Files\BarcodeCartManager\BarcodeCartManager.exe"
```

## Monitoraggio

### Script PowerShell per monitoraggio:

```powershell
# check-service.ps1
$serviceName = "BarcodeCartManager"
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($service.Status -ne "Running") {
    Write-Host "⚠️ Servizio non in esecuzione! Tentativo riavvio..."
    Start-Service -Name $serviceName
    Start-Sleep -Seconds 5
    
    $service = Get-Service -Name $serviceName
    if ($service.Status -eq "Running") {
        Write-Host "✅ Servizio riavviato con successo"
    } else {
        Write-Host "❌ ERRORE: Impossibile riavviare il servizio"
        # Invia alert/email
    }
} else {
    Write-Host "✅ Servizio in esecuzione"
}
```

Programma check ogni 5 minuti con Task Scheduler:
```cmd
schtasks /create /tn "BarcodeCartManager Monitor" /tr "powershell.exe -File C:\Scripts\check-service.ps1" /sc minute /mo 5 /ru SYSTEM
```

## Aggiornamento Versione

### Procedura sicura:

```cmd
REM 1. Stop servizio
sc stop BarcodeCartManager

REM 2. Backup versione corrente
xcopy "C:\Program Files\BarcodeCartManager" "C:\Backup\BarcodeCartManager_%date%" /E /I

REM 3. Backup configurazione
copy "C:\Program Files\BarcodeCartManager\appsettings.json" "C:\Backup\appsettings.json.backup"

REM 4. Copia nuova versione
xcopy "C:\NewVersion\*" "C:\Program Files\BarcodeCartManager" /E /Y

REM 5. Ripristina configurazione (se necessario)
copy "C:\Backup\appsettings.json.backup" "C:\Program Files\BarcodeCartManager\appsettings.json"

REM 6. Riavvia servizio
sc start BarcodeCartManager

REM 7. Verifica log
```

## Checklist Pre-Produzione

- [ ] Applicazione testata in modalità console
- [ ] appsettings.json configurato con IP reali
- [ ] Pubblicata con opzione corretta (self-contained vs framework-dependent)
- [ ] Copiata in posizione permanente
- [ ] Servizio installato
- [ ] Startup Type = Automatic
- [ ] Recovery options configurate (auto-restart)
- [ ] Firewall configurato
- [ ] Logging su file abilitato
- [ ] Test avvio/stop servizio
- [ ] Test riavvio server (verifica auto-start)
- [ ] Monitoraggio configurato
- [ ] Documentazione consegnata all'IT

## Riferimenti

- [.NET Windows Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [NSSM Documentation](https://nssm.cc/usage)
- [SC Command Reference](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create)

---

**Tip**: Testa SEMPRE l'installazione in ambiente di staging prima di produzione!
