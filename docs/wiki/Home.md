# PTL Orchestrator — Wiki

Sistema di **pick-to-light** per carrelli/ceste: l'operatore scansiona un codice, il sistema accende la lampada
PTL (controller Lightstep, protocollo PP505) sulla cesta giusta e registra ogni operazione.

L'evolutiva porta il vecchio eseguibile console a un sistema a due livelli:
**postazioni** (GUI WPF + DB locale SQLite, funzionano anche offline) e **server centrale** (API + PostgreSQL).

## Indice
- [Architettura](Architettura.md) — progetti, flussi, decisioni
- [Setup e Build](Setup-e-Build.md) — come far partire tutto su un nuovo PC
- [Configurazione](Configurazione.md) — sezioni di `appsettings.json`
- [API](Api.md) — endpoint del server centrale
- [Convenzioni](Convenzioni.md) — stile, test, flusso git
- [Roadmap](Roadmap.md) — stato dei lavori e prossimi passi

## Stato in una riga
Core, Contracts e Api completi e testati; Station headless completa (storage, sync eventi/operatori, scanner seriale, composition root).
Mancano: pagina diagnostica, rifiniture server, migrazione della Console. Vedi [Roadmap](Roadmap.md).
