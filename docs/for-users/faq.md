# FAQ — Domande frequenti

Domande generali su MeepleAI. Per FAQ specifiche di una funzione, vedi la pagina della funzione (es. [Chat RAG](./features/rag-chat.md#domande-frequenti-su-questa-funzione)).

---

## Iniziare

### Cos'è MeepleAI?

Un assistente AI per giochi da tavolo. Carichi il manuale di un gioco in PDF e l'app risponde alle tue domande sulle regole citando il manuale. Inoltre tieni traccia della tua collezione e delle partite.

### Devo pagare?

No durante la fase **Alpha** (gratis e senza limiti). Post-Alpha sarà a credito (1 chat ≈ 1 credito), con un piano free generoso. Dettagli: pagina *Crediti & Piani* (in arrivo).

### Funziona con il mio gioco preferito?

Sì se:
- Il gioco è su BoardGameGeek (~140k giochi → import 5 secondi), oppure lo aggiungi a mano.
- Hai accesso al PDF del manuale (Caricamento manuale PDF richiesto per la chat AI).

Funziona con tutti i giochi da tavolo classici (eurogame, ameritrash, party, war, fillers).

### MeepleAI sostituisce BoardGameGeek?

No, è complementare. BGG resta la **fonte dati comunitaria** (catalogo, voti, peso). MeepleAI aggiunge: **AI sulle regole, gestione collezione personale, sessioni live, statistiche**.

---

## Account e dati

### I miei dati sono al sicuro?

- Account su database PostgreSQL cifrato.
- PDF in storage S3-compatibile cifrato.
- Connessione HTTPS sempre.
- 2FA opzionale (raccomandato per admin).
- GDPR compliant: puoi esportare e cancellare i tuoi dati in qualsiasi momento.

### Le mie domande chat addestrano l'AI?

**No**. Le domande non vengono usate per addestrare modelli pubblici di terze parti. Sono log-gate per debug e qualità — vedi `privacy.md` *(in arrivo)* per dettagli.

### Posso esportare la mia collezione?

Sì: **Impostazioni** → **Privacy** → **Esporta** → ZIP con libreria, cronologia chat, impostazioni (JSON+CSV).

### Cancellazione account?

**Impostazioni** → **Privacy** → **Cancella**. Soft-delete 30 giorni (annullabile). Dopo 30 giorni: cancellazione irreversibile.

---

## AI e regole

### L'AI può sbagliare?

Sì, capita. Ogni risposta cita il **passaggio del manuale** da cui pesca: clicca per verificare. Se la citazione non corrisponde alla risposta, segnala con 👎. Per casi-limite usa una *House Rule*.

### Cosa succede se chiedo qualcosa non nel manuale?

L'AI risponde *"Non ho trovato questa informazione nel manuale"*. Non inventa. Se serve aggiungi il manuale dell'errata o una House Rule.

### Supporto multi-lingua?

- **Manuali**: IT, EN, ES, FR, DE indicizzati.
- **Domande**: qualunque lingua europea — l'AI traduce internamente.
- **UI**: IT, EN.
- **OCR scansioni**: IT, EN.

### Quanto è veloce?

Tipicamente **2-4 secondi** per risposta. Manuali molto lunghi (>200 pagine): fino a 30s alla prima domanda, poi cache.

---

## Limiti e roadmap

### Cosa ancora non c'è (Alpha)?

- App nativa iOS/Android (per ora PWA — comunque installabile sul telefono).
- SSO enterprise (SAML/Okta).
- Modalità offline completa.
- Multi-currency / fatturazione automatica (post-Alpha).

### Quando esce la Beta?

Vedi la roadmap pubblica (in arrivo). La fase Alpha si chiude quando le 5 feature core (auth, catalogo, PDF, chat, libreria) sono **stabili in produzione** con feedback utente positivo.

### Posso contribuire?

Repo proprietario in fase Alpha. Possibile contribution model open dopo Beta. Per ora: feedback via 👎 sui messaggi chat, o contattando il team.

---

## Problemi tecnici

### La chat dice "Errore" — cosa faccio?

1. **Refresh** (F5 / pull-to-refresh).
2. Verifica connessione internet.
3. Se il PDF era stato appena caricato, aspetta che il pallino verde "Pronto" appaia.
4. Se persiste: contatta support con screenshot + ID gioco.

### Il PDF non si carica

- Controlla **dimensione < 50 MB** (vedi [Caricamento PDF](./features/pdf-upload.md)).
- Controlla **formato**: solo `.pdf`. Per scansioni, salva come PDF (non come immagine).
- Se è un PDF protetto da password, sproteggi prima dell'upload.

### Login OAuth Google/GitHub fallisce

- Cancella i cookie del sito MeepleAI.
- Ritenta da finestra anonima.
- Se persiste: usa email+password per accedere e poi collega OAuth da **Impostazioni** → **Sicurezza**.

---

## Pagine correlate

- **Funzioni**: [Chat RAG](./features/rag-chat.md) · [PDF](./features/pdf-upload.md) · [Catalogo BGG](./features/games-bgg.md) · [Libreria](./features/library.md) · [Account](./features/auth.md)
- **Hub**: [README guida utente](./README.md)

---

**Ultima revisione**: 2026-05-06
