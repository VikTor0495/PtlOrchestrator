# PTL Orchestrator – Barcode Cart Manager

> **Evoluzione in corso (branch `feature/evolutiva-gui-db`)**: il progetto ora è composto da una **postazione WPF** (`PtlOrchestrator.Station`, con database locale SQLite e funzionamento offline), un **server centrale** (`PtlOrchestrator.Api`, ASP.NET Core + PostgreSQL) e il **Core** condiviso. La console descritta sotto è la versione storica. Documentazione completa, architettura e roadmap: [`docs/wiki/Home.md`](docs/wiki/Home.md).

## 📌 Descrizione generale

**PTL Orchestrator** è un’applicazione **console/worker self-contained** che gestisce un sistema **Pick-to-Light (PTL)** per il caricamento guidato di prodotti su **carrelli e basket**, utilizzando **barcode** e **conferma fisica tramite pulsante** sul modulo PTL.

L’applicazione:
- guida l’operatore tramite **LED e display PTL**
- garantisce **coerenza tra stato logico e stato hardware**
- registra il **lavoro realmente svolto**
- genera un **report CSV finale** su richiesta (RESET / QUIT)

È pensata per **ambienti industriali**, dove:
- l’ordine dei comandi è critico
- l’hardware è seriale
- l’affidabilità è più importante del throughput

---

## ⚙️ Funzionalità principali

- ✔ Lettura barcode (scanner o input testuale)
- ✔ Assegnazione automatica a carrello e basket
- ✔ LED verde lampeggiante fino a conferma operatore
- ✔ LED rosso fisso a basket pieno
- ✔ Gestione `m1` / `m2` PP505 corretta (before / after CONFIRM)
- ✔ Reset completo PTL all’avvio e su comando
- ✔ Report CSV dei prodotti lavorati
- ✔ Applicazione **self-contained** (un solo `.exe`)
- ✔ Nessuna dipendenza esterna a runtime

---

## 🧱 Architettura (high level)

┌───────────────┐
│ Barcode Input │
└───────┬───────┘
        ↓
┌─────────────────────┐
│ CartManager         │
│ (logica dominio)    │
└───────┬─────────────┘
        ↓
┌─────────────────────┐
│ PTL Command Service │───► Controller PTL (TCP/IP)
└─────────────────────┘



- **CartManager**: logica di business e coerenza
- **PTL Command Builder**: costruzione comandi PP505 corretti
- **Worker**: loop principale + comandi speciali
- **Report CSV**: generato solo a fine sessione

---

## 🖥️ Requisiti

### In fase di sviluppo
- .NET SDK **10**
- Windows (per runtime `win-x64`)

### In produzione
- **Nessun requisito**
- Non serve .NET installato
- Basta l’`exe`

---

## 📦 Installazione / Build

### 🔹 Build self-contained (un solo EXE)

Eseguire dalla root del progetto:

```powershell
dotnet publish src/PtlOrchestrator.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### 🔹 Output si troverà:

src/PtlOrchestrator.Console/bin/
└─ Release/
    └─ publish/
        ├─ PtlOrchestrator.exe
        └─ appsettings.json

##🔹 Struttura finale dell'applicativo:

/PtlOrchestrator/
 ├─ PtlOrchestrator.exe
 ├─ appsettings.json
 └─ report/
     └─ ptl-report-YYYYMMDD-HHMMSS.csv


