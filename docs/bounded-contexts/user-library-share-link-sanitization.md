# UserLibrary — Share Link Sanitization Rules

Quando un utente condivide la propria libreria via `LibraryShareLink`, il response di
`GetSharedLibraryQuery` viene sanitizzato. Questa tabella definisce cosa viene incluso.

## Matrice visibilità

| Campo | Sempre incluso | Solo se `IncludeNotes=true` | Mai incluso |
|-------|---------------|----------------------------|-------------|
| Titolo gioco | ✅ | | |
| Publisher, Anno, Categorie, Meccaniche | ✅ | | |
| Copertina, Thumbnail | ✅ | | |
| Stato (Owned/InPrestito/Wishlist) | ✅ | | |
| Rating medio (pubblico) | ✅ | | |
| `TimesPlayed`, `LastPlayedAt`, `WinRate` | ✅ | | |
| Etichette custom (nomi) | ✅ | | |
| Note personali (`LibraryNotes`) | | ✅ | |
| Info debitore (`StateNotes` di InPrestito) | | | ❌ |
| `TargetPrice` (wishlist) | | | ❌ |
| Config agente AI (`AgentConfiguration`) | | | ❌ |
| PDF privati (`CustomPdfMetadata`) | | | ❌ |
| ID interni (UserId, EntryId) | | | ❌ |

## Wishlist pubblica

La wishlist è inclusa nel share link con questi campi:
- Titolo gioco, cover, priority (High/Medium/Low)
- **TargetPrice escluso** (informazione finanziaria privata)

## Profilo pubblico

La wishlist con `WishlistVisibility.Public` è visibile anche nel profilo pubblico utente
(distinto dal share link). Il profilo pubblico NON richiede il token.

## Validazione lato backend

`GetSharedLibraryQueryHandler` DEVE verificare prima di restituire dati:
1. Token esiste nel DB
2. `RevokedAt == null`
3. `ExpiresAt == null || ExpiresAt > UtcNow`
4. Incremento `ViewCount` atomico (vedi Task 4 — OPS-002)
