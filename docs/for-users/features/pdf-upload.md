# Caricamento manuale PDF

> **Cos'è in una frase**: carichi il PDF del manuale di un gioco e MeepleAI lo "legge" pezzo per pezzo per poter rispondere alle tue domande in [Chat RAG](./rag-chat.md).

---

## In breve

| | |
|---|---|
| **A cosa serve** | Insegnare a MeepleAI le regole di un gioco |
| **Per chi** | Admin di workspace, editor, owner del gioco |
| **Cosa serve prima** | Il gioco già nel catalogo ([Catalogo & BGG](./games-bgg.md)) |
| **Formati supportati** | PDF (testo o scansionato con OCR), max 50 MB |
| **Dove si trova** | Pagina del gioco → tab **Manuale** → pulsante **Carica PDF** |

---

## Come funziona (per l'utente)

1. Apri il gioco dalla [Libreria](./library.md) o dal Catalogo.
2. Tab **Manuale** → **Carica PDF**.
3. Scegli il file. Inizia l'**indicizzazione** (1-5 minuti per manuali tipici).
4. Quando vedi ✅ "Pronto", la [Chat](./rag-chat.md) può rispondere.

### Cosa succede dietro le quinte

| Step | Cosa fa | Tempo tipico |
|------|---------|--------------|
| **1. Estrazione testo** | Legge il PDF, gestisce OCR se scansionato | ~30s |
| **2. Chunking** | Divide in paragrafi gestibili (~500 token) | ~10s |
| **3. Embedding** | Converte ogni chunk in vettore numerico | ~1 min/100 chunk |
| **4. Indicizzazione** | Salva nel database vettoriale (pgvector) | ~10s |

Lo stato è visibile in real-time con barra di progresso.

---

## Cosa caricare

### ✅ Adatto

- Manuale ufficiale del gioco (preferito: in italiano se disponibile)
- Errata e FAQ ufficiali (carica come PDF separato dello stesso gioco)
- Espansioni (carica come PDF separato, etichetta come espansione)

### ❌ Non adatto

- Recensioni o tutorial blog
- Foto del manuale (usa lo scanner del telefono per generare PDF testuale)
- Manuali con copyright restrittivo che non possiedi
- Manuali in lingue non supportate (al momento: IT, EN, ES, FR, DE)

---

## Manuali scansionati (OCR)

Se il PDF è una **scansione** (immagini, non testo selezionabile):

- L'OCR è automatico ma più lento (~3 min per 100 pagine)
- Qualità della scansione conta: 300 DPI minimo, no foto storte
- Lingue OCR supportate: IT, EN
- Se il risultato è scarso, MeepleAI lo segnala con ⚠️ "Bassa qualità OCR"

**Workaround per manuali storti**: usa Adobe Scan o Microsoft Lens prima di caricare.

---

## Aggiornare un manuale

Hai trovato un'errata? La nuova edizione è cambiata?

1. Vai sul tab **Manuale**.
2. Clicca **Sostituisci PDF**.
3. Il vecchio resta come **storico** (puoi confrontare).
4. Reindicizzazione automatica in pochi minuti.

La [Chat](./rag-chat.md) usa **sempre l'ultima versione**.

---

## Privacy

- Il PDF è memorizzato cifrato in S3-compatible storage.
- **Non** viene condiviso con altri workspace o utenti senza tuo consenso.
- Puoi cancellarlo in qualsiasi momento dal tab Manuale → ⋯ → Elimina.
- L'indice vettoriale viene cancellato insieme al PDF.

---

## Limiti noti

- ❌ **Max 50 MB per file**. Manuali più grandi vanno divisi in capitoli.
- ❌ **Non legge tabelle complesse** alla perfezione (riferimenti incrociati, regole con frecce).
- ⚠️ **Manuali multi-lingua** (IT+EN nello stesso PDF): MeepleAI rileva la lingua dominante.
- ⚠️ **Errata sparse**: meglio caricare tutti gli errata in un unico PDF "Errata Bundle" piuttosto che 10 PDF separati.

---

## Funzioni correlate

- [Chat sulle regole (RAG)](./rag-chat.md) — usa il manuale caricato
- [Catalogo giochi & BGG](./games-bgg.md) — aggiungi il gioco prima
- [Libreria personale](./library.md) — vedi i giochi col manuale caricato

---

**Ultima revisione**: 2026-05-06
