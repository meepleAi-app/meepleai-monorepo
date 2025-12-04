# Guida Utente - MeepleAI

**Versione**: 1.0
**Ultima Modifica**: 2025-12-04
**Lingua**: Italiano

---

## Indice

1. [Introduzione](#-introduzione)
2. [Primi Passi](#-primi-passi)
3. [Caricare un Regolamento](#-caricare-un-regolamento)
4. [Fare Domande](#-fare-domande)
5. [Interfaccia Chat](#-interfaccia-chat)
6. [Funzionalita Avanzate](#-funzionalita-avanzate)
7. [Domande Frequenti (FAQ)](#-domande-frequenti-faq)
8. [Supporto](#-supporto)

---

## Introduzione

**MeepleAI** e il tuo assistente AI per i regolamenti dei giochi da tavolo. Progettato pensando al mercato italiano, ti aiuta a trovare risposte rapide e accurate alle tue domande durante le partite.

### Cosa Puo Fare MeepleAI

| Funzionalita | Descrizione |
|--------------|-------------|
| **Ricerca Regole** | Trova rapidamente sezioni specifiche del regolamento |
| **Risposte AI** | Ottieni spiegazioni chiare in linguaggio naturale |
| **Multi-Gioco** | Gestisci regolamenti di piu giochi contemporaneamente |
| **Citazioni** | Ogni risposta include riferimenti al regolamento originale |
| **Italiano First** | Interfaccia e risposte ottimizzate per l'italiano |

### Perche MeepleAI

- **Precisione >95%**: Sistema di validazione multi-modello per garantire accuratezza
- **Zero Invenzioni**: Se non sa la risposta, te lo dice ("Non ho trovato questa informazione")
- **Risposte Rapide**: Ricerca semantica avanzata per trovare le informazioni in secondi
- **Fonte Citata**: Ogni risposta include il riferimento alla pagina del regolamento

---

## Primi Passi

### 1. Accedi all'Applicazione

Vai su [meepleai.dev](https://meepleai.dev) e accedi con:
- **Email e Password**: Registrazione classica
- **Google**: Accesso rapido con account Google
- **Discord**: Per la community di giocatori
- **GitHub**: Per sviluppatori

### 2. Dashboard Principale

Dopo l'accesso, vedrai la dashboard con:

```
+------------------------------------------+
|  MeepleAI                    [Profilo]   |
+------------------------------------------+
|                                          |
|  I Tuoi Giochi          [+ Nuovo Gioco]  |
|  +----------------+  +----------------+  |
|  | Terraforming   |  | Wingspan       |  |
|  | Mars           |  |                |  |
|  | 3 domande      |  | 1 domanda      |  |
|  +----------------+  +----------------+  |
|                                          |
|  Domande Recenti                         |
|  - "Come funziona la fase di produzione?"|
|  - "Quante carte si pescano?"            |
|                                          |
+------------------------------------------+
```

### 3. Configura le Preferenze

1. Vai su **Impostazioni** (icona ingranaggio)
2. Seleziona la **lingua preferita** (italiano/inglese)
3. Configura le **notifiche**
4. Scegli il **tema** (chiaro/scuro)

---

## Caricare un Regolamento

### Formati Supportati

| Formato | Supporto | Note |
|---------|----------|------|
| **PDF** | Completo | Formato consigliato |
| **PDF Scansionato** | Completo | OCR automatico |
| **Immagini (JPG/PNG)** | Parziale | Solo singole pagine |

### Procedura di Caricamento

1. **Clicca su "+ Nuovo Gioco"** nella dashboard
2. **Inserisci i dettagli**:
   - Nome del gioco
   - Editore (opzionale)
   - Anno (opzionale)
3. **Carica il PDF** del regolamento
4. **Attendi l'elaborazione** (1-3 minuti per regolamenti standard)

### Stato Elaborazione

Durante l'elaborazione vedrai:

| Stato | Significato |
|-------|-------------|
| **In coda** | Il documento e in attesa di elaborazione |
| **Elaborazione** | Estrazione testo in corso |
| **Indicizzazione** | Creazione indici di ricerca |
| **Pronto** | Puoi iniziare a fare domande! |

### Qualita dell'Elaborazione

Dopo l'elaborazione, vedrai un punteggio qualita:

| Punteggio | Significato |
|-----------|-------------|
| **90-100%** | Eccellente - PDF ben formattato |
| **70-89%** | Buono - Alcune tabelle potrebbero mancare |
| **50-69%** | Sufficiente - Verifica le risposte |
| **<50%** | Basso - Considera un PDF migliore |

---

## Fare Domande

### Come Formulare Domande Efficaci

**Domande Chiare** (consigliate):
- "Come funziona la fase di produzione in Terraforming Mars?"
- "Quante carte si pescano all'inizio del turno?"
- "Cosa succede quando un giocatore rimane senza risorse?"

**Domande Meno Efficaci** (da evitare):
- "Come si gioca?" (troppo generica)
- "Cosa devo fare?" (manca contesto)
- "E' legale?" (ambigua)

### Tipi di Domande Supportate

| Tipo | Esempio | Supporto |
|------|---------|----------|
| **Regole Base** | "Quante azioni per turno?" | Ottimo |
| **Casi Specifici** | "Se ho 2 carte e pesco, quante ne tengo?" | Ottimo |
| **Setup** | "Come si prepara il gioco per 4 giocatori?" | Ottimo |
| **Interazioni** | "La carta X si combina con la carta Y?" | Buono |
| **Strategia** | "Qual e la strategia migliore?" | Non supportato* |

*MeepleAI risponde solo su regole, non su strategia

### Comprendere le Risposte

Ogni risposta include:

```
+--------------------------------------------------+
| DOMANDA: Come funziona la fase di produzione?    |
+--------------------------------------------------+
| RISPOSTA:                                        |
| Durante la fase di produzione, ogni giocatore    |
| riceve risorse in base ai suoi indicatori di     |
| produzione...                                    |
|                                                  |
| FONTE: Regolamento Terraforming Mars, p. 12     |
| CONFIDENZA: 95%                                  |
+--------------------------------------------------+
```

- **Risposta**: La spiegazione in linguaggio naturale
- **Fonte**: Pagina del regolamento di riferimento
- **Confidenza**: Quanto il sistema e sicuro (>70% = affidabile)

---

## Interfaccia Chat

### Layout della Chat

```
+------------------------------------------+
|  Terraforming Mars - Chat        [Menu]  |
+------------------------------------------+
|  [Tu]: Come funziona la produzione?      |
|                                          |
|  [MeepleAI]: Durante la fase di          |
|  produzione, ogni giocatore riceve...    |
|  > Fonte: Regolamento p.12               |
|  > Confidenza: 95%                       |
|                                          |
|  [Tu]: E per le carte blu?               |
|                                          |
|  [MeepleAI]: Le carte blu (eventi)...    |
|  > Fonte: Regolamento p.8                |
|  > Confidenza: 92%                       |
|                                          |
+------------------------------------------+
|  [Scrivi la tua domanda...]    [Invia]   |
+------------------------------------------+
```

### Funzioni Chat

| Azione | Come Fare | Descrizione |
|--------|-----------|-------------|
| **Nuova Domanda** | Scrivi e premi Invio | Fai una nuova domanda |
| **Segui** | Rispondi alla risposta | Approfondisci un argomento |
| **Copia** | Icona copia sulla risposta | Copia la risposta |
| **Segnala** | Icona flag | Segnala risposta errata |
| **Cronologia** | Menu > Cronologia | Vedi domande precedenti |

### Contesto della Conversazione

MeepleAI mantiene il contesto della conversazione. Puoi fare domande di follow-up:

1. "Come funziona la produzione?" (domanda iniziale)
2. "E per l'acciaio?" (follow-up - capisce che parli di produzione acciaio)
3. "Quanto ne ricevo per tessera?" (follow-up - continua sul tema)

---

## Funzionalita Avanzate

### Ricerca Multi-Gioco

Se hai piu giochi caricati, puoi:

1. Andare su **Ricerca Globale**
2. Digitare la domanda
3. Selezionare i giochi in cui cercare
4. Ottenere risposte comparative

### Setup Guidato

Per ogni gioco, MeepleAI puo guidarti nel setup:

1. Apri il gioco dalla dashboard
2. Clicca su **"Guida Setup"**
3. Inserisci il numero di giocatori
4. Segui i passaggi step-by-step

### Sessioni di Gioco

Durante una partita, puoi:

1. Avviare una **Sessione di Gioco**
2. Registrare i giocatori
3. Tracciare le regole consultate
4. Salvare note per partite future

### Editor Regole (Beta)

Per regole casalinghe o varianti:

1. Vai su **Editor** dalla dashboard
2. Crea una nuova specifica
3. Aggiungi le tue regole personalizzate
4. MeepleAI le includera nelle risposte

---

## Domande Frequenti (FAQ)

### Caricamento e Elaborazione

**D: Quanto tempo ci vuole per elaborare un PDF?**
> Dipende dalla dimensione: 1-3 minuti per regolamenti standard (20-50 pagine), 5-10 minuti per manuali complessi (100+ pagine).

**D: Il mio PDF e protetto da password, funziona?**
> No, devi prima rimuovere la protezione. Usa strumenti come Adobe Acrobat o servizi online.

**D: Posso caricare regolamenti in lingue diverse dall'italiano?**
> Si, MeepleAI supporta italiano, inglese, francese, tedesco e spagnolo. Le risposte saranno nella lingua della tua preferenza.

### Qualita delle Risposte

**D: MeepleAI puo sbagliare?**
> Si, come ogni sistema AI. Per questo mostriamo sempre la fonte e la confidenza. Se la confidenza e <70%, verifica sul regolamento.

**D: Cosa significa "Non ho trovato questa informazione"?**
> Significa che la risposta non e nel regolamento caricato. Potrebbe essere una regola non scritta, una variante, o una domanda di strategia.

**D: Come segnalo una risposta errata?**
> Clicca sull'icona flag accanto alla risposta. Il nostro team verifichera e migliorera il sistema.

### Account e Privacy

**D: I miei regolamenti sono privati?**
> Si, i tuoi regolamenti sono visibili solo a te. Non li condividiamo con altri utenti.

**D: Posso eliminare i miei dati?**
> Si, vai su Impostazioni > Privacy > Elimina Account. Tutti i tuoi dati saranno cancellati entro 30 giorni.

**D: MeepleAI funziona offline?**
> No, richiede connessione internet per l'elaborazione AI.

### Problemi Tecnici

**D: La chat non risponde, cosa faccio?**
> 1. Ricarica la pagina (F5)
> 2. Verifica la connessione internet
> 3. Prova a uscire e rientrare
> 4. Contatta supporto se persiste

**D: L'elaborazione PDF e bloccata?**
> Se resta "In elaborazione" per piu di 15 minuti, contatta il supporto con il nome del file.

---

## Supporto

### Canali di Supporto

| Canale | Tempo Risposta | Per |
|--------|----------------|-----|
| **FAQ** | Immediato | Domande comuni |
| **Email** | 24-48 ore | Problemi tecnici |
| **Discord** | 1-4 ore | Community e suggerimenti |
| **In-App** | 24 ore | Bug report |

### Contatti

- **Email**: support@meepleai.dev
- **Discord**: [discord.gg/meepleai](https://discord.gg/meepleai)
- **GitHub Issues**: Per segnalazioni tecniche

### Feedback

Aiutaci a migliorare MeepleAI:

1. **Segnala errori**: Usa l'icona flag sulle risposte
2. **Suggerisci funzionalita**: Discord #suggerimenti
3. **Valuta le risposte**: Pollice su/giu dopo ogni risposta

---

## Glossario

| Termine | Significato |
|---------|-------------|
| **Confidenza** | Quanto il sistema e sicuro della risposta (0-100%) |
| **OCR** | Riconoscimento ottico caratteri (per PDF scansionati) |
| **RAG** | Retrieval-Augmented Generation (tecnologia AI usata) |
| **Indicizzazione** | Processo di preparazione per la ricerca |
| **Sessione** | Una partita tracciata in MeepleAI |

---

**Buon Gioco!**

*MeepleAI - Il tuo arbitro AI per i giochi da tavolo*

---

**Documento**: Guida Utente MeepleAI
**Audience**: Utenti finali (giocatori)
**Lingua**: Italiano
**Ultimo Aggiornamento**: 2025-12-04
