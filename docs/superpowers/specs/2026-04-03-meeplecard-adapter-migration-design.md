# MeepleCard Adapter Migration Design

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** Migrare i 5 adapter principali e 3 wrapper al nuovo sistema visuale unificato MeepleCard (SymbolStrip con entity-tinted background, identity chip con icone, ManaLinkFooter, linkedEntities), introducendo un layer di mapper puri e collassando i wrapper ridondanti.

**Date:** 2026-04-03

---

## Contesto

La recente evoluzione del sistema MeepleCard ha introdotto:
- **SymbolStrip** con sfondo entity-tinted, metric pills a destra, identity chips (icon-first) a sinistra
- **`linkedEntities` + `ManaLinkFooter`** — pip colorati per entity collegate (sostituisce `navigateTo` deprecated)
- **`identityChip1Icon` / `identityChip2Icon`** — chip circolari emoji al posto di testo troncato
- **`getEntityDarkGradient`** — background entity-tinted in gaming theme
- **`ColorSchemeProvider`** — gaming theme accessibile globalmente

Gli adapter di dominio (`MeepleGameCard`, `MeeplePlayerCard`, ecc.) non sfruttano ancora queste feature. Usano ancora `navigateTo` (deprecated) e non passano le nuove props al SymbolStrip.

---

## Scope

### L1 — Visual parity
Aggiornare i 5 adapter principali per passare le nuove props al sistema visuale.

### L2 — API cleanup
- Migrare da `navigateTo` → `linkedEntities` negli adapter principali
- Consolidare il `BggGameCard` duplicato

### L3 — Wrapper collapse
Eliminare i wrapper sottili ridondanti:
- `PrivateGameCard.tsx` → assorbito da `MeepleLibraryGameCard` con prop `isPrivate`
- `SharedLibraryGameCard.tsx` → assorbito da `MeepleGameCatalogCard`
- `BggGameCard` in `/components/games/` → eliminato, unica source in `/components/bgg/`

---

## Architettura

### Layer di mapper puri

```
src/lib/card-mappers/
├── index.ts                    # re-export pubblico
├── game-card-mapper.ts         # buildGameCardProps(game) → Partial<MeepleCardProps>
├── player-card-mapper.ts       # buildPlayerCardProps(player) → Partial<MeepleCardProps>
├── session-card-mapper.ts      # buildSessionCardProps(session) → Partial<MeepleCardProps>
├── kb-card-mapper.ts           # buildKbCardProps(document) → Partial<MeepleCardProps>
└── shared-utils.ts             # formatPlayTime(), buildLinkedEntities(), mechToIcon()
```

**Principio:** I mapper sono funzioni pure `(DTO) → Partial<MeepleCardProps>`. Non importano React. Testabili in isolamento con Jest puro.

**Flusso:**
```
Domain DTO
    → mapper() → Partial<MeepleCardProps>
    → Adapter merge con props locali (actions, callbacks)
    → <MeepleCard {...mergedProps} />
```

---

## Mapping dati per adapter

### `game-card-mapper.ts` — fonte: `Game` / `SharedGameDto`

| MeepleCard prop | Sorgente dominio | Note |
|---|---|---|
| `playerCountDisplay` | `${game.minPlayers}-${game.maxPlayers}p` | Rimosso da `metadata[]` |
| `playTimeDisplay` | `formatPlayTime(game.averagePlayTime)` | Es. `"45min"`, `"2h"`, `"2-3h"` |
| `identityChip1` | `game.primaryMechanism ?? game.genre` | Testo per tooltip |
| `identityChip1Icon` | `mechToIcon(game.primaryMechanism)` | Emoji: `"♟️"` `"🎲"` `"🗺️"` `"🤝"` |
| `identityChip2` | `game.secondaryMechanism` | Opzionale |
| `identityChip2Icon` | `mechToIcon(game.secondaryMechanism)` | Opzionale |
| `linkedEntities` | `buildLinkedEntities({ agentCount, kbCount })` | Sostituisce `navigateTo` |
| `metadata[]` | Solo: publisher, anno, BGG rating | **Non** playerCount/playTime |

### `player-card-mapper.ts` — fonte: `SessionPlayer` / `UserProfileDto`

| MeepleCard prop | Sorgente dominio | Note |
|---|---|---|
| `avatarUrl` | `player.avatarUrl` | Cover per entity `player` |
| `gamesPlayed` | `player.gamesPlayedCount` | SymbolStrip metric |
| `winRate` | `player.winRate` | 0–100, SymbolStrip metric |
| `identityChip1` | `player.rank ?? player.favoriteGameGenre` | |
| `identityChip1Icon` | `rankToIcon(player.rank)` | `"👑"` `"⭐"` `"🎖️"` |

### `session-card-mapper.ts` — fonte: `GameSessionDto`

| MeepleCard prop | Sorgente dominio | Note |
|---|---|---|
| `sessionPlayers` | `session.players` | Score table + turn sequence |
| `sessionTurn` | `session.currentTurn` | |
| `sessionRoundScores` | `session.roundScores` | |
| `sessionActions` | `{ onEditScore, onStart, onPause, onResume, onJoin }` | Da props adapter |
| `linkedEntities` | `[{ entityType: 'game', count: session.games.length }, ...]` | Gioco/i collegati come pip Mana in footer |
| `stateLabel` | `sessionStatusToLabel(session.status)` | `{ text: 'Live', variant: 'success' }` |
| `identityChip1` | **non usato** — il gioco appare come `linkedEntities` | |

> **Decisione di design:** Il gioco collegato alla sessione non va in `identityChip1` (testo abbreviato), ma come `linkedEntity` con `entityType: 'game'` nel `ManaLinkFooter`. Clic sul pip naviga alla MeepleCard del gioco.

### `kb-card-mapper.ts` — fonte: `PdfDocumentDto`

| MeepleCard prop | Sorgente dominio | Note |
|---|---|---|
| `pageCount` | `document.pageCount` | SymbolStrip metric |
| `chunkCount` | `document.chunkCount` | SymbolStrip metric |
| `identityChip1` | `document.fileType.toUpperCase()` | Es. `"PDF"` |
| `identityChip1Icon` | `"📄"` | Costante |
| `stateLabel` | `processingStateToLabel(document.processingState)` | `{ text: 'Indicizzato', variant: 'success' }` |

### `MeepleLibraryGameCard` — delta minimo (già al 95%)

| MeepleCard prop | Sorgente dominio | Note |
|---|---|---|
| `identityChip1` | `entry.game.primaryMechanism` | Nuovo |
| `identityChip1Icon` | `mechToIcon(entry.game.primaryMechanism)` | Nuovo |
| `metadata[]` | Rimuovere playerCount/playTime | Ora nel SymbolStrip |

---

## Utility condivise (`shared-utils.ts`)

```typescript
// Formatta durata in minuti → stringa leggibile
formatPlayTime(minutes: number): string
// Es: 45 → "45min", 90 → "90min", 120 → "2h", 150 → "2-3h"

// Mappa meccanismo di gioco → emoji
mechToIcon(mechanism: string | undefined): string | undefined
// "Worker Placement" → "⚙️", "Deck Building" → "🃏", "Cooperative" → "🤝"
// "Area Control" → "🗺️", "Auction" → "💰", "Dice" → "🎲", default → undefined

// Mappa rank giocatore → emoji
rankToIcon(rank: string | undefined): string | undefined
// "Master" → "👑", "Expert" → "⭐", "Veteran" → "🎖️", default → undefined

// Costruisce linkedEntities da conteggi
buildLinkedEntities(counts: {
  agentCount?: number;
  kbCount?: number;
  sessionCount?: number;
  gameCount?: number;
}): LinkedEntityInfo[]
// Filtra i conteggi > 0

// Mappa sessionStatus → stateLabel
sessionStatusToLabel(status: SessionStatus): { text: string; variant: 'success' | 'warning' | 'error' | 'info' }

// Mappa processingState → stateLabel
processingStateToLabel(state: ProcessingState): { text: string; variant: 'success' | 'warning' | 'error' | 'info' }
```

---

## L3 — Wrapper Collapse

### `PrivateGameCard.tsx` → eliminato

**Consumatori attuali** (da aggiornare):
- `/components/library/private-game-detail/` — sostituire con `MeepleLibraryGameCard` + prop `isPrivate?: boolean`

**Change in `MeepleLibraryGameCard`:** aggiungere prop opzionale `isPrivate?: boolean` che condiziona il rendering del badge "Privato" e disabilita le azioni di condivisione.

### `SharedLibraryGameCard.tsx` → eliminato

**Consumatori attuali** (da aggiornare):
- Shared library view — sostituire con `MeepleGameCatalogCard`

### `BggGameCard` duplicato → consolidato

- **Eliminare:** `/components/games/BggGameCard.tsx`
- **Mantenere:** `/components/bgg/BggGameCard.tsx` come unica source
- **Aggiornare:** tutti gli import che puntano a `/games/BggGameCard`

---

## Testing Strategy

### Tier 1 — Mapper puri (Jest, no React)

Per ogni mapper, testare:
- Mapping corretto di ogni prop dalla sorgente di dominio
- `buildLinkedEntities` filtra i conteggi a zero
- `formatPlayTime` gestisce tutti i range (0, 45, 90, 120, 150+ minuti)
- `mechToIcon` restituisce `undefined` per meccanismi sconosciuti
- `metadata[]` non contiene più playerCount/playTime dopo la migrazione

```typescript
// Esempio: game-card-mapper.test.ts
describe('buildGameCardProps', () => {
  it('builds playerCountDisplay from min/max players', ...)
  it('omits playerCount from metadata', ...)
  it('builds linkedEntities only for counts > 0', ...)
  it('returns undefined identityChip1Icon for unknown mechanism', ...)
});
```

### Tier 2 — Adapter integration (React Testing Library)

Per ogni adapter migrato, 2–3 test essenziali:

```typescript
// MeepleGameCard.test.tsx
it('renders mana pip for linked agent when agentCount > 0', ...)
it('passes identityChip1Icon to SymbolStrip', ...)
it('does NOT render playerCount chip in footer metadata', ...)

// MeepleSessionCard.test.tsx
it('renders mana pip for linked game', ...)
it('renders stateLabel "Live" for active session', ...)

// MeepleKbCard.test.tsx
it('passes pageCount and chunkCount to SymbolStrip', ...)
it('renders stateLabel "Indicizzato" for indexed document', ...)
```

### Tier 3 — Wrapper collapse (regressione)

Per ogni wrapper eliminato, 1 test di regressione sul componente sostituto:

```typescript
// MeepleLibraryGameCard.test.tsx
it('renders correctly with isPrivate=true', ...)
it('handles game with null sharedGameId (private game)', ...)
```

---

## Definition of Done

### Must Have
- [ ] `src/lib/card-mappers/` creato con 4 mapper + shared-utils
- [ ] `MeepleGameCard` usa mapper, passa identityChip, linkedEntities, SymbolStrip metric
- [ ] `MeeplePlayerCard` usa mapper, passa avatarUrl, gamesPlayed, winRate, identityChip
- [ ] `MeepleSessionCard` usa mapper, passa sessionPlayers/turn/scores, linkedEntities (game pip)
- [ ] `MeepleKbCard` usa mapper, passa pageCount, chunkCount, stateLabel
- [ ] `MeepleLibraryGameCard` passa identityChip1/Icon, rimuove playerCount/playTime da metadata
- [ ] `PrivateGameCard.tsx` eliminato, consumatori migrati a `MeepleLibraryGameCard + isPrivate`
- [ ] `SharedLibraryGameCard.tsx` eliminato, consumatori migrati a `MeepleGameCatalogCard`
- [ ] `BggGameCard` duplicato eliminato, import aggiornati
- [ ] Test Tier 1: mapper puri coperti al 100%
- [ ] Test Tier 2: almeno 2 test per adapter migrato
- [ ] Test Tier 3: 1 test di regressione per wrapper eliminato

### Should Have
- [ ] `navigateTo` rimosso dagli adapter che usano ora `linkedEntities`
- [ ] Gaming theme verificato manualmente su `/library` e `/games`

### Won't Have (fuori scope)
- [ ] Rimozione completa del tipo `navigateTo` da `MeepleCardProps` (Fase 2 futura)
- [ ] Collapse degli adapter grandi (MeepleGameCard, MeepleSessionCard) in MeepleCard diretto
- [ ] Nuova pagina di showcase per il gaming theme

---

## File creati / modificati / eliminati

### Creati
- `src/lib/card-mappers/index.ts`
- `src/lib/card-mappers/game-card-mapper.ts`
- `src/lib/card-mappers/player-card-mapper.ts`
- `src/lib/card-mappers/session-card-mapper.ts`
- `src/lib/card-mappers/kb-card-mapper.ts`
- `src/lib/card-mappers/shared-utils.ts`
- `src/lib/card-mappers/__tests__/game-card-mapper.test.ts`
- `src/lib/card-mappers/__tests__/player-card-mapper.test.ts`
- `src/lib/card-mappers/__tests__/session-card-mapper.test.ts`
- `src/lib/card-mappers/__tests__/kb-card-mapper.test.ts`
- `src/lib/card-mappers/__tests__/shared-utils.test.ts`

### Modificati
- `src/components/games/MeepleGameCard.tsx`
- `src/components/players/MeeplePlayerCard.tsx`
- `src/components/session/MeepleSessionCard.tsx`
- `src/components/documents/MeepleKbCard.tsx`
- `src/components/library/MeepleLibraryGameCard.tsx` (+ prop `isPrivate`)
- Consumatori di `PrivateGameCard` (aggiornamento import)
- Consumatori di `SharedLibraryGameCard` (aggiornamento import)
- Consumatori di `BggGameCard` in `/games/` (aggiornamento import)

### Eliminati
- `src/components/library/PrivateGameCard.tsx`
- `src/components/library/SharedLibraryGameCard.tsx`
- `src/components/games/BggGameCard.tsx`

---

*Spec approvata il: 2026-04-03*
*Autore: Claude Sonnet 4.6 via spec-panel + brainstorming*
