# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build e publish

- Target: `net10.0-windows` (.NET 10 SDK). Il README cita ancora .NET 7/8: è obsoleto.
- Build: `dotnet build -c Release`
- Publish di produzione (single-file, self-contained, come da README):
  `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true`
- Nessun progetto di test. Analisi statica attiva via `AnalysisLevel` nel csproj e regole in `.editorconfig` (CRLF, 4 spazi): il build mostra ~13 warning preesistenti da non aumentare.
- Dopo ogni modifica a un `.cs`, un hook PostToolUse esegue `dotnet format` sul file (vedi `.claude/settings.json`).

## Gotcha

- La libreria Lightstep è referenziata da `libs/Lightstep/AioiSystems.Lightstep.dll` (HintPath nel csproj): non rimuoverla né aggiornarla senza verifica.
- `appsettings.json` va modificato solo a applicazione spenta (vedi `readme.txt`). Il CSV dei limiti (`LimitBarcode.FileName`, es. `prodotti_config.csv`) deve stare nella stessa cartella dell'eseguibile, altrimenti l'app non parte.
- I report CSV finali vanno nella cartella `report/` accanto all'exe, generati solo a fine sessione (RESET / QUIT).
- Il worker dipende da hardware reale (controller PTL via TCP/IP, moduli seriali): avviarlo fuori dal sito non verifica la logica PTL.

## Convenzioni

- Dominio, messaggi di log e commenti sono in italiano: mantenere la stessa lingua nelle modifiche.
