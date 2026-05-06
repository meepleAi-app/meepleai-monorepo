# MeepleAI — Guida utente

> **Audience contract**: chi usa MeepleAI come app (giocatori, master di tavolo, admin di workspace).
> **Stile**: enciclopedico (Wikipedia-like). Spiega *cosa fa* l'app, non *come è costruita*.
> Per la parte tecnica vai su [`../for-developers/`](../for-developers/) (contributor) o [`../for-claude/`](../for-claude/) (architect/AI).

---

## Cos'è MeepleAI

MeepleAI è un assistente AI per giochi da tavolo. Carichi il manuale del gioco in PDF e l'app risponde alle tue domande sulle regole citando il manuale.

Le **5 funzioni Alpha** sono coperte:

1. [**Account & accesso**](./features/auth.md) — registrazione, login, OAuth, 2FA
2. [**Catalogo giochi & BGG**](./features/games-bgg.md) — aggiungi giochi, importa da BoardGameGeek
3. [**Caricamento PDF manuale**](./features/pdf-upload.md) — abilita la chat AI sul gioco
4. [**Chat sulle regole (RAG)**](./features/rag-chat.md) — chiedi e ricevi risposta con citazioni dal manuale
5. [**Libreria personale**](./features/library.md) — collezione, wishlist, storico partite

➡️ Domande generali? Vai a [**FAQ**](./faq.md).
➡️ Cosa fa MeepleAI in dettaglio? Vedi il [Project Brief](./meepleai-project-brief.md).

---

## Quale pagina mi serve?

| Devo… | Pagina |
|-------|--------|
| Capire come funziona la chat | [Chat RAG](./features/rag-chat.md) |
| Caricare il manuale di un gioco | [Caricamento PDF](./features/pdf-upload.md) |
| Aggiungere giochi alla libreria | [Catalogo & BGG](./features/games-bgg.md) |
| Vedere/organizzare la mia collezione | [Libreria personale](./features/library.md) |
| Creare un account / abilitare 2FA | [Account & accesso](./features/auth.md) |
| Risposta a una domanda generale | [FAQ](./faq.md) |

---

## Lingua

Italiano-first. Versione inglese pianificata post-Alpha.

---

## Status

✅ **Phase 5 completa** — 5 pagine feature + FAQ scritte. Le funzioni *Sessions live*, *Agents/House Rules*, *Billing*, *Privacy* sono in roadmap (in arrivo dopo Beta).

**Ultima revisione**: 2026-05-06
