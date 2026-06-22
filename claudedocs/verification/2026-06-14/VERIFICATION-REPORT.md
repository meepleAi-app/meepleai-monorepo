# Seed KB Coverage — Side-by-Side Verification Report

> **Date**: 2026-06-14
> **Branch**: `feature/seed-kb-coverage-evaluation-2026-06-14`
> **Spec**: `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`
> **User context**: admin@meepleai.app (NOT Free tier user — SP4 seed users not yet provisioned in this DB)

## Methodology

1. Started Playwright session against live stack (`localhost:3000`) + temp HTTP server for mockups (`localhost:8889/admin-mockups/design_files/`).
2. Captured screenshots: live page + corresponding mockup HTML.
3. Used a11y snapshot to enumerate page semantics (counts, tab labels, button refs).
4. Auth: admin user (Free tier SP4 users `marco@meepleai.test` etc. don't exist in current snapshot — would require fresh `make seed-sp4`).

## Captures

| # | Page | Live | Mockup | Note |
|---|------|------|--------|------|
| 1 | Login | `01-login-live.png` (4.5 KB) | `01-login-mockup.png` (357 KB, `auth-flow.html`) | Allineato |
| 2 | Libreria | `02-library-live.png` (571 KB) | `02-library-mockup.png` (2 MB, `sp4-library-desktop.html`) | Divergenza struttura cards (vedi §3) |

## Verifica delle metriche (M1-M4) sul snapshot live

Stato DB `meepleai_staging` post-snapshot (NON post `make seed-sp4`):

- **Totale giochi**: 160 (snapshot ricco, supera SP4 baseline di 8)
- **Giochi con `has_knowledge_base=true`**: 126 / 160 (78.8%)
- **Giochi con embeddings**: 125 / 160 (78.1%)
- **Embeddings totali**: 10 437

Filtrando sui 13 titoli target del seed SP4 espanso:
- 11/13 status `complete` (Azul, 7 Wonders Duel, Ark Nova, Brass, Carcassonne, Codenames, Pandemic, Spirit Island, Terraforming Mars, Ticket to Ride, Wingspan)
- 1/13 `no_kb` by-design (Gloomhaven `_targetState: failed`)
- 1/13 `no_kb` per dual-row anomaly (vedi §4 Catan IT/EN)

## Divergenze osservate (live vs mockup)

### 3.1 Login page (`/login` vs `auth-flow.html`)

Allineamento alto. Layout identico (card centrata + titolo "Accedi" + email/password + OAuth providers Google/Discord/GitHub + link "Password dimenticata?" + link Registrati). Token visivi (background `#f7f3ee` cream) coerenti con DS-15 canonical tokens.

**Nessuna divergenza significativa rilevata.**

### 3.2 Library page (`/library` vs `sp4-library-desktop.html`)

Mockup `sp4-library-desktop.html` è uno stage multi-frame (9 frame Desktop) che mostra:
- Empty state, single-game, multi-game grid, list view, drawer Add Game, filtri attivi.

Live `/library` come admin mostra UN solo frame (default grid view) con:
- Header: "La tua libreria" + subtitle
- CTA top right: "+ Aggiungi gioco" + "Importa BGG"
- Tab navigation count: **Tutti 22 / Giochi 1 / Agenti 1 / KB 20 / Sessioni 0 / Chat 0**
- 22 card entry (1 Game "Mage Knight Board Game", 20 KB PDF documents, 1 Agent "Rules Expert")

**Divergenze**:

1. **Card visual**: Live cards mostrano placeholder cover deterministico (placeholder pattern post-BGG-ban #2123 / ADR-059, vedi `cover-utils.ts`). Mockup mostra cover colorate con entity-game styling. ✅ **By design** — non è una regressione, è il risultato del ban legale BGG.

2. **Card label content**: Live cards label format è `KB Azioni <filename.pdf> <game-title>`. Mockup mostra titolo gioco prominente + metadata sotto. Live mette PDF filename come label primario, gioco secondario. **Possibile miglioramento UX**: invertire la prominenza (titolo gioco prima, PDF filename come secondary annotation).

3. **Tab "KB" presente**: ✅ Conforme. Il flusso utente Flow 1 (filtra per "KB indicizzata") è implementato come tab "KB" che restituisce 20 PDF nel caso corrente. Atteso comportamento differente per Free tier user (vedrebbe N giochi con `has_knowledge_base=true` invece di N PDF, perché la library scope per Free tier è "i giochi nella mia libreria" non "tutti i PDF accessibili").

4. **Mage Knight Board Game** appare come "Giochi 1": admin ha 1 gioco linkato alla sua library. Free tier marco avrebbe 8 giochi (per data.json `library.marco`). La differenza è coerente con il modello: admin non riceve seed library entries.

## Catan dual-row anomaly — RESOLVED (Q4 closure 2026-06-14)

DB `shared_games` containeva 2 row Catan distinte:
- `Catan` (EN) → `has_knowledge_base = true` (snapshot pre-esistente)
- `I Coloni di Catan` (IT) → `has_knowledge_base = false` (snapshot pre-esistente)

**Risoluzione applicata in questo PR**: titolo del seed rinominato `I Coloni di Catan` → `Catan` (canonical EN). Il seed ora matcha il record EN dello snapshot. Slug `catan` invariato per backward compat con kbDocs/agents/library/events.

**Translation IT deferred a follow-up**: il servizio di traduzione esiste (`OpenRouterTranslationService`) ma è wired solo per risposte LLM, non per game titles. Opzioni per il follow-up documentate in spec doc §9 Q4 closure.

## Verifica dei 3 flussi utente richiesti

| Flow | Stato verifica | Note |
|------|---|------|
| **F1** — Filter library by KB indexed + add to library | 🟡 Parziale | Tab "KB 20" presente (filtra). "Aggiungi gioco" CTA presente. Test completo richiede Free tier user + seed fresh. |
| **F2** — Chat with game agent | ⏳ Non eseguito | Richiede gioco con KB ready + Agent owned. Procedura documentata in spec doc §7. |
| **F3** — Start session | ⏳ Non eseguito | Richiede gioco con KB ready in library + permission. Procedura documentata in spec doc §7. |

## Raccomandazioni per chiusura completa Task 8

Per completare la verifica E2E side-by-side dei 3 flussi richiesti dall'utente:

1. **Provision Free tier user**: eseguire `make seed-sp4` in modalità che rispetti il DB esistente (probabilmente serve `--reset` first per evitare collisioni con Catan dual-row).
2. **Auth come `marco@meepleai.test`** con password seed default (`Sp4-Seed-Pwd!2026`).
3. **Eseguire i 3 flussi**:
   - F1: `/library` → tab "Giochi" → click "+ Aggiungi gioco" → drawer Add Game → filtra per KB indexed → seleziona + conferma → verifica entry in library
   - F2: `/library` → click su game card con KB → tab "Agent" → invia domanda canonica → verifica citations
   - F3: `/library` → click game card → click "Avvia sessione" → verifica redirect `/sessions/{id}/live`
4. **Mockup paragoni mirati**:
   - F1 add drawer: `sp4-add-game-pdf-dedup.html` o `sp4-library-desktop.html` frame "Add"
   - F2 chat: `sp4-game-chat-tab.html` o `sp4-game-detail.html`
   - F3 session live: `sp4-session-catan-live.html` (gioco-specifico Catan)

## Status finale Task 8

🟡 **Parziale**. Ho eseguito 2 capture side-by-side (login + library overview) + verificato struttura DB + verificato presenza tab "KB" come implementazione di Flow 1 filter. I 3 flussi completi richiedono provisioning Free tier user + interazione drawer/chat/session più estesa, documentata in spec doc §7 per esecuzione manuale.

**Decisione utente richiesta**: continuare verifica E2E nei prossimi step (richiederebbe `make seed-sp4` + nuovo round browser automation) oppure considerare il deliverable corrente sufficiente per la prima iterazione.
