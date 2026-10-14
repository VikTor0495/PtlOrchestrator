# PtlOrchestrator — istruzioni per l'AI

Prima di lavorare leggi la wiki in `docs/wiki/` (parti da `Home.md`) e la `Roadmap.md`.

## Regole di lavoro
- Lingua: italiano (commenti XML e commit seguono lo stile esistente).
- Il PC di sviluppo può spegnersi in qualsiasi momento: **commit piccoli e `git push` dopo ogni passo completato**.
- Dopo ogni passo della roadmap: aggiorna `docs/wiki/Roadmap.md` (spunta il passo) nello stesso commit.
- Prima del commit: `dotnet test PtlOrchestrator.sln` deve passare.
- Branch di lavoro: `feature/evolutiva-gui-db` (mai committare direttamente su `master`).
- Commit in stile conventional (`feat(station): ...`, `docs: ...`, `test: ...`).
- Se cambi architettura, contratti o configurazione, aggiorna la pagina wiki relativa.
