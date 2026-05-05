# Chat RAG (Domande sui regolamenti)

> **Cos'è in una frase**: una chat che risponde a domande sulle regole di un gioco da tavolo leggendo automaticamente il manuale PDF che hai caricato.

---

## In breve

| | |
|---|---|
| **A cosa serve** | Chiedere "come si gioca", "cosa succede se…", "quante carte pesco" senza sfogliare il manuale |
| **Per chi** | Giocatori, master di tavolo, demo a nuovi giocatori |
| **Cosa serve prima** | Un gioco nel catalogo + almeno un PDF del regolamento caricato |
| **Lingua** | Italiano e inglese (rileva automaticamente) |
| **Dove si trova** | Pagina del gioco → tab **Chat** |

---

## Come funziona (per l'utente)

1. **Apri il gioco** dalla tua Libreria o dal Catalogo giochi *(pagine in arrivo)*.
2. Vai sul tab **Chat**.
3. Scrivi una domanda in linguaggio naturale, es:
   > *"Quante carte si pescano all'inizio del turno?"*
4. La risposta arriva in pochi secondi, **con i riferimenti al manuale** (numero di pagina o paragrafo).
5. Puoi continuare la conversazione: la chat ricorda le domande precedenti.

### Esempi di domande utili

| Categoria | Esempio |
|-----------|---------|
| Regole base | *"Come si vince la partita?"* |
| Casi particolari | *"Cosa succede se finisco le carte nel mazzo?"* |
| Setup | *"Quante risorse iniziali ha ogni giocatore?"* |
| Confronti | *"Differenza tra azione standard e azione bonus?"* |
| Fasi del turno | *"Spiegami la fase di mantenimento"* |

### Esempi di domande **NON** adatte

- *"Conviene questo gioco?"* (recensione, non regola)
- *"Inventa una variante"* (la chat risponde solo in base al manuale)
- *"Qual è il prezzo?"* (info commerciali non presenti nel manuale)

---

## Cosa fa la chat dietro le quinte

In termini semplici: la chat **non sa nulla del gioco a memoria**. Ogni volta che fai una domanda:

1. Cerca nel manuale i passaggi più rilevanti (ricerca per significato + per parole chiave).
2. Manda quei passaggi a un modello AI insieme alla tua domanda.
3. Riceve una risposta che cita il manuale.
4. Te la mostra **con i link al testo originale**, così puoi verificare.

Questo si chiama **RAG** (*Retrieval-Augmented Generation*) — generazione assistita da recupero. Significa che l'AI non inventa: pesca dal manuale.

> Vuoi i dettagli tecnici? Vedi `docs/for-claude/architecture/` (audience: sviluppatori) — la sezione RAG arriverà in Phase 3 della reorganizzazione docs.

---

## Affidabilità delle risposte

Ogni risposta ha:

- **Sorgenti citate**: pezzi di manuale evidenziati, cliccabili.
- **Indice di confidenza**: alto / medio / basso.
- **Avviso "non ho trovato"**: se il manuale non contiene la risposta, la chat lo dice esplicitamente invece di inventare.

### Cosa fare se la risposta sembra sbagliata

1. **Apri la sorgente citata** — verifica nel manuale.
2. **Riformula la domanda** — più specifica = risposta migliore.
3. **Segnala** con il pulsante 👎 sotto la risposta. Va a migliorare il sistema.
4. Per casi-limite, aggiungi una *House Rule* per forzare un comportamento *(pagina Agents in arrivo)*.

---

## Limiti noti

- ❌ **Non legge le immagini del manuale**. Se la regola sta solo in una illustrazione, non la trova. Workaround: usa una House Rule testuale (pagina Agents in arrivo).
- ❌ **Non confronta più giochi** in una sola domanda. Apri una chat per gioco.
- ⚠️ **Manuali molto lunghi** (>200 pagine): può richiedere fino a 30 secondi alla prima domanda (poi è veloce).
- ⚠️ **Edizioni diverse**: se hai caricato il manuale dell'edizione 2018 e chiedi della 2023, le differenze non emergono. Carica il manuale corretto.

---

## Privacy

- Le tue domande **non vengono usate** per addestrare modelli pubblici.
- I PDF caricati restano **nel tuo account** (o nel workspace del tavolo, se sei admin).
- La chat è log-gata per debug e qualità — pagina Privacy & Dati *in arrivo*.

---

## Costi

- **Alpha gratis** durante la fase Alpha (illimitate domande).
- **Post-Alpha**: a credito (1 domanda ≈ 1 credito). Pagina Crediti & Piani *in arrivo*.

---

## Funzioni correlate

Le pagine seguenti saranno create durante Phase 5 della reorganizzazione docs:

- Caricamento PDF regolamento — passo obbligatorio prima della chat
- Catalogo giochi & BGG — aggiungere il gioco
- Libreria personale — collezione, tag
- Agents & House Rules — comportamenti personalizzati
- FAQ generale

---

## Domande frequenti su questa funzione

**Posso fare domande in inglese su un manuale italiano?**
Sì. La chat traduce internamente. La risposta arriva nella lingua della tua domanda.

**Risposte diverse alla stessa domanda?**
Possibile (modelli generativi). I fatti citati dalle sorgenti sono però costanti. Se vedi contraddizioni nelle citazioni, segnala.

**Il manuale è cambiato (errata): aggiorno?**
Sì. Carica il nuovo PDF, l'indice si rigenera in pochi minuti. La vecchia versione resta accessibile come storico.

**Posso esportare la conversazione?**
Sì, dal menu ⋯ in alto a destra della chat → "Esporta in Markdown".

---

> **Pagina di test pilota** — template per tutte le altre pagine `for-users/features/*`. Adattare la struttura "In breve / Come funziona / Limiti / Costi / Correlate / FAQ" alle altre 4 feature Alpha.

**Ultima revisione**: 2026-05-05
