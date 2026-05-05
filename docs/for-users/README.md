# MeepleAI — Guida utente

> **Audience contract**: chi usa MeepleAI come app (giocatori, master di tavolo, admin di workspace).
> **Stile**: enciclopedico (Wikipedia-like). Spiega *cosa fa* l'app, non *come è costruita*.
> Per la parte tecnica vai su [`../for-developers/`](../for-developers/) (contributor) o [`../for-claude/`](../for-claude/) (architect/AI).

---

## Cos'è MeepleAI

MeepleAI è un assistente AI per giochi da tavolo. Carichi il manuale del gioco in PDF e l'app risponde alle tue domande sulle regole citando il manuale.

Le funzioni principali:

- **[Chat sulle regole (RAG)](./features/rag-chat.md)** — chiedi e ricevi risposta con citazioni dal manuale
- **Caricamento PDF** *(da scrivere)* — aggiungi un manuale, l'app lo indicizza
- **Catalogo giochi & BGG** *(da scrivere)* — aggiungi giochi, importa da BoardGameGeek
- **Libreria personale** *(da scrivere)* — collezione, wishlist, storico partite
- **Account & accesso** *(da scrivere)* — registrazione, login, OAuth, 2FA

---

## Come è organizzata questa guida

| Sezione | Per chi |
|---------|---------|
| `features/` | Una pagina per funzione, stile Wikipedia (cosa fa, come si usa, limiti, FAQ) |
| `roles/` | Guide per ruolo: utente, editor, admin |
| `tutorials/` | Walkthrough end-to-end (dal primo accesso alla prima chat) |
| `brand/` | Identità, contenuti, tono di voce |
| `faq.md` *(TBD)* | Domande frequenti generali |

---

## Quale pagina mi serve?

- *"Come funziona la chat?"* → [Chat sulle regole](./features/rag-chat.md)
- *"Come carico un manuale?"* → `features/pdf-upload.md` *(da scrivere)*
- *"Sono admin, come gestisco gli utenti?"* → `roles/admin.md` *(da migrare da user-guides/)*
- *"Quanto costa?"* → `billing.md` *(da scrivere)*
- *"Privacy / dati personali?"* → `privacy.md` *(da scrivere)*

---

## Status

🚧 **Stub root** — Phase 0 di [reorganizzazione docs](../MIGRATION-PLAN.md). Una pagina pilota esiste già: [Chat RAG](./features/rag-chat.md). Il resto del contenuto user-facing migra/viene scritto in Phase 5.

**Lingua**: italiano-first (versione inglese pianificata post-Alpha).
