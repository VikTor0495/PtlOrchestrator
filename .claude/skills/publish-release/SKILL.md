---
name: publish-release
description: Pubblica PtlOrchestrator come single-file self-contained per win-x64 (cartella publish con exe e appsettings.json). Usare solo quando l'utente chiede esplicitamente una release.
disable-model-invocation: true
---

Pubblica l'applicativo per la distribuzione in produzione.

1. Dalla root del progetto (`C:\LazialeDistribuzione\PtlOrchestrator`) esegui:

   ```
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
   ```

2. Se la build fallisce, riporta l'errore completo e fermati: non procedere.

3. Verifica che esistano `bin\Release\net10.0-windows\win-x64\publish\PtlOrchestrator.exe` e `appsettings.json` nella cartella publish.

4. Riporta il percorso della cartella publish e ricorda all'utente che:
   - `appsettings.json` e il CSV dei limiti (`LimitBarcode.FileName`) devono stare accanto all'exe sul PC di destinazione;
   - le modifiche ad `appsettings.json` si fanno solo ad applicazione spenta.

Non modificare il codice, non fare commit e non copiare nulla su altri percorsi senza che l'utente lo chieda.
