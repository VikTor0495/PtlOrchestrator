PTL ORCHESTRATOR - GUIDA RAPIDA
==============================

Questa guida spiega come configurare e utilizzare l'applicativo PTL Orchestrator.


CONFIGURAZIONE (appsettings.json)
=================================

Il file appsettings.json serve a:
- collegare l’applicazione al controller PTL (Lightstep)
- definire i carrelli e i cestelli disponibili
- configurare il file limiti barcode

Il file va modificato SOLO a applicazione spenta.

---

1. Sezione "Lightstep"
----------------------
Questa sezione configura la connessione al controller PTL.

Campi:
- LicenzeKey: Chiave di licenza del sistema Lightstep. NON modificare se non fornita dal fornitore.
- ControllerIp: Indirizzo IP del controller PTL.
- ControllerPort: Porta TCP di comunicazione del controller.
- ConnectionTimeout: Timeout in millisecondi per la connessione (es. 3000).

---

2. Sezione "Carts"
------------------
Definisce i carrelli gestiti dall’applicazione.
Ogni carrello ha:
- CartId: Identificativo numerico univoco
- Baskets: Lista dei cestelli associati

---

3. Sezione "Baskets"
--------------------
Ogni carrello contiene uno o più cestelli.
Campi:
- BasketId: Identificativo del cestello (es. "0001")

---

4. Sezione "LimitBarcode"
-------------------------
Configura il file CSV dei limiti barcode.
Campi:
- FileName: Nome del file CSV (es. prodotti_config.csv)
- BarcodeColumnIndex: Indice colonna barcode (es. 0)
- LimitColumnIndex: Indice colonna limite (es. 1)

---

5. Aggiungere un nuovo carrello
-------------------------------
- Duplicare un blocco nella sezione "Carts"
- Assegnare un nuovo CartId
- Configurare i relativi BasketId

---

6. Aggiungere/modificare un cestello
------------------------------------
- Inserire un nuovo oggetto nella lista "Baskets"
- Specificare BasketId

Attenzione:
- BasketId errato → il carrello non risponderà

---

7. Riavvio
----------
Dopo qualsiasi modifica:
1. Salvare il file appsettings.json
2. Avviare l’applicazione
3. Verificare dai log che la connessione al controller PTL sia attiva

Le modifiche NON hanno effetto senza riavvio (spegnere e riaccendere l'app).


AVVIO DELL’APPLICAZIONE
=======================


1. Verifiche preliminari
------------------------
- Assicurarsi che il controller PTL sia acceso e collegato in rete.
- Verificare che il file appsettings.json sia presente nella stessa cartella di PtlOrchestrator.exe.
- Controllare che i valori di ControllerIp e ControllerPort siano corretti.
- **Verificare che il file CSV dei limiti barcode (es. prodotti_config.csv) sia presente nella stessa cartella dell’eseguibile, come specificato in appsettings.json, altrimenti l’applicazione non si avvia correttamente!**

---

2. Avvio
--------
- Fare doppio clic su PtlOrchestrator.exe
  oppure
- Avviare PtlOrchestrator.exe da riga di comando.

---

3. Comandi disponibili durante l’esecuzione
-------------------------------------------
- status : Mostra lo stato dei carrelli
- reset  : Reset di tutti i carrelli (genera report)
- exit/quit : Esci dal programma (genera report)
- help/? : Mostra i comandi disponibili

---

4. Arresto
----------
- Chiudere l’applicazione solo quando non ci sono operazioni in corso.
- Non spegnere il controller PTL durante l’esecuzione.

Per fermare l’applicazione:
- digitare il comando 'quit' oppure 'exit'.

Comportamento all’arresto:
- l’applicazione termina le operazioni in corso
- viene generato un report finale
- il report viene salvato nella cartella "report" (se la cartella non esiste, viene creata automaticamente)
 - il report viene salvato in formato CSV nella cartella "report" (se la cartella non esiste, viene creata automaticamente)

---

5. Reset applicazione
---------------------
È possibile eseguire un reset completo dell’applicazione digitando:

reset

Il comando reset:
- azzera lo stato corrente dell’applicazione
- annulla eventuali operazioni in corso
- riporta l’applicazione nello stato iniziale

Il reset non modifica il file 'appsettings.json'.

---

Per ulteriori dettagli consultare i log generati dall’applicazione.
