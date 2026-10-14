# PTL Orchestrator

Sistema **Pick-to-Light** per carrelli e ceste: l'operatore scansiona un codice a barre, il sistema accende la lampada PTL (controller Lightstep) sulla cesta giusta e registra ogni operazione.

## Come è fatto

```
[Server centrale]            [PC di ogni postazione]
 Api + PostgreSQL  <──HTTP──  Station (WPF) ── usa ──> Core ──> PTL Lightstep
                                 │                       ▲
                                 ├─ SQLite locale        │
                                 └─ pistola seriale ─────┘
```

| Parte | Cos'è | Dove gira |
|---|---|---|
| **Api** | Programma a sé stante. Riceve gli eventi dalle postazioni, tiene l'elenco degli operatori, produce i report dei turni. Non sa nulla del PTL. | Una sola volta, sul server centrale (con PostgreSQL) |
| **Core** | Libreria con la logica che pilota il PTL: carrelli, ceste, comandi al controller, login operatore, smistamento delle scansioni. Non è un programma. | Dentro la Station |
| **Station** | L'applicazione della postazione (finestra WPF). Legge la pistola seriale, usa il Core, salva tutto in un database locale e parla con l'Api in background. **Funziona anche senza rete**: invia gli eventi appena il server torna raggiungibile. | Un PC per ogni postazione |
| **Contracts** | I dati condivisi tra Station e Api. | — |
| **Console** | La versione vecchia, tenuta solo come storico. Non usa il Core nuovo: niente login operatore, niente collegamento all'Api. Da dismettere dopo la validazione sul campo della Station. | — |

## Documentazione

Tutto il resto (architettura, installazione, configurazione, endpoint dell'Api, roadmap dei lavori) è nella wiki: [`docs/wiki/Home.md`](docs/wiki/Home.md).
