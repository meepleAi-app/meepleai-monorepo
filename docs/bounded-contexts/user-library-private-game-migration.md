# UserLibrary — PrivateGame Migration Data Flow

## Flusso completo

```
PrivateGame (id=AAA)
    ↓ ProposePrivateGameCommand
GameSuggestion (status=Pending)
    ↓ Admin approva → GameSuggestionApprovedEvent
CreateProposalMigrationOnApprovalHandler → ProposalMigration (SharedGameId=BBB)
    ↓ HandleMigrationChoiceCommand (Choice=LinkToCatalog)
HandleMigrationChoiceCommandHandler:
    UserLibraryEntry.SharedGameId = BBB
    UserLibraryEntry.PrivateGameId = null
```

## Sessioni di gioco (GameSession)

Le sessioni NON vengono aggiornate durante la migrazione — e non devono esserlo.

**Perché:** `GameSession.UserLibraryEntryId` punta all'entry, non al gioco direttamente.
Dopo la migrazione, la stessa entry ora punta a `SharedGameId=BBB`. Le sessioni
rimangono associate all'entry e risultano automaticamente collegate al nuovo SharedGame.

```sql
-- Prima della migrazione
SELECT * FROM GameSessions gs
JOIN UserLibraryEntries ule ON gs.UserLibraryEntryId = ule.Id
WHERE ule.PrivateGameId = 'AAA'  -- funziona

-- Dopo la migrazione (stesso risultato)
SELECT * FROM GameSessions gs
JOIN UserLibraryEntries ule ON gs.UserLibraryEntryId = ule.Id
WHERE ule.SharedGameId = 'BBB'  -- funziona automaticamente
```

## Etichette custom (GameLabel)

Le etichette sono collegate tramite `UserGameLabel(UserLibraryEntryId, GameLabelId)`.
La entry è la stessa dopo la migrazione → etichette **conservate automaticamente**.

## PDF privati (CustomPdfMetadata)

`UserLibraryEntry.CustomPdfMetadata` (JSON) rimane sull'entry invariato.
Il PDF è ancora accessibile all'utente dopo la migrazione.

## Scelta KeepPrivate

Se l'utente sceglie `KeepPrivate`, `UserLibraryEntry` mantiene `PrivateGameId=AAA`
e `SharedGameId` rimane null. Il gioco nel catalogo esiste ma l'utente non è collegato.
