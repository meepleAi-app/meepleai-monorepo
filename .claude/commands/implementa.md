# /implementa - Full Issue Implementation Workflow

> **Comando**: `/implementa <Issue N>`
> **Alias di**: `/sc:implement --uc Issue <Issue N>` con workflow completo

## Descrizione

Workflow completo di implementazione issue con:
- Selezione automatica agenti/MCP/skill ottimali per ogni step
- Creazione branch, implementazione, testing, PR, merge, cleanup

## Argomenti

- `$ARGUMENTS` - Numero issue GitHub (es: `2372`, `#2372`, `Issue 2372`)

## Workflow

### Phase 1: Preparazione
1. **Leggi documentazione** issue da GitHub (MCP: `issue_read`)
2. **Analizza requisiti** e DoD (Definition of Done)
3. **Ricerca codebase** per patterns esistenti (Agent: `Explore`)

### Phase 2: Pianificazione
1. **Pianifica 2 opzioni** di implementazione
2. **Scegli la migliore** (se confidenza < 95%, chiedi all'utente)
3. **Crea TodoWrite** con task breakdown

### Phase 3: Setup
1. **Crea branch** seguendo convenzione:
   - `feature/*` per nuove funzionalità
   - `fix/*` per bug fix
   - `docs/*` per documentazione
   - `test/*` per test
   - `refactor/*` per refactoring
   - `chore/*` per manutenzione
   - `release/*` per release
   - Formato: `<tipo>/<branch-corrente>-<issueId>`

### Phase 4: Implementazione
1. **Implementa** seguendo principi progetto (CQRS, DDD, SOLID)
2. **Se UI**: Crea Chromatic/Storybook tests
3. **Aggiorna GitHub issue** (task checkbox + DoD) durante implementazione
4. **Non skippare errori**: Replica, analizza, crea guardie

### Phase 5: Validazione
1. **Esegui tests** (`dotnet test`, `pnpm test`)
2. **Verifica build** senza warning

### Phase 6: PR e Code Review
1. **Crea PR** verso branch padre
2. **Esegui code review** automatica (`/code-review:code-review`)
3. **Risolvi problemi** con confidence score >= 75
4. **Ripeti code review** finché tutti i problemi >= 75 sono risolti
5. **Aggiorna issue** checkbox su GitHub via `mcp__MCP_DOCKER__issue_write`

### Phase 7: Chiusura
1. **Aggiorna issue** locale e GitHub (stato finale, task completati, DoD)
2. **Merge** PR al branch padre
3. **Cleanup** branch locale (`git branch -D`), prune remotes

## Regole

- **Non interrompere workflow** - pianifica prima, esegui dopo
- **Non aspettare CI** - siamo in alpha, non produzione
- **No nuovi warning** - mantieni codebase pulita
- **Confidenza < 95%** → Chiedi all'utente
- **Compiti complessi** → Crea sub-issue dedicata
- **Errori** → Non skippare, replica e crea guardie

## MCP Integration

| Step | MCP/Agent |
|------|-----------|
| Issue read | `mcp__MCP_DOCKER__issue_read` |
| Issue update | `mcp__MCP_DOCKER__issue_write` |
| Codebase explore | Agent `Explore` |
| Complex analysis | `mcp__sequential-thinking__sequentialthinking` |
| Symbol operations | `mcp__serena__*` |
| UI components | `mcp__magic__*` |
| Code review | Skill `/code-review:code-review` |
| PR operations | `gh pr create`, `gh pr merge` |

## Esempi

```bash
/implementa 2373
/implementa #2373
/implementa Issue 2373
```

## Output Atteso

```
Phase 1: Preparazione ✅
Phase 2: Pianificazione ✅
Phase 3: Setup (branch created) ✅
Phase 4: Implementazione ✅
Phase 5: Validazione ✅
Phase 6: PR e Code Review ✅
  - PR created: <url>
  - Code review: X issues found
  - Issues resolved (score >= 75): Y/Z
Phase 7: Chiusura ✅

Issue #<N> completata e chiusa.
PR: <url>
Branch: merged e cleaned
```

---

**Prompt da eseguire**:

Esegui `/sc:implement --uc Issue $ARGUMENTS` con il seguente workflow:

1. **Leggi documentazione**: Usa `mcp__MCP_DOCKER__issue_read` per leggere issue #$ARGUMENTS dal repo GitHub
2. **Effettua ricerca**: Usa Agent Explore per analizzare codebase e trovare patterns
3. **Crea branch**: `git checkout -b <tipo>/<branch-corrente>-$ARGUMENTS`
4. **Pianifica 2 opzioni**: Analizza e scegli la migliore (chiedi se confidenza < 95%)
5. **Implementa**: Segui principi progetto, crea Chromatic test se UI
6. **Aggiorna issue**: Task checkbox e DoD durante implementazione via `mcp__MCP_DOCKER__issue_write`
7. **Valida**: Esegui tests, verifica no warning
8. **Crea PR**: Usa `gh pr create` con summary
9. **Code review**: Esegui `/code-review:code-review` sulla PR
10. **Risolvi problemi**: Fix automatico di tutti i problemi con confidence score >= 75
11. **Ripeti code review**: Continua finché non ci sono più problemi con score >= 75
12. **Aggiorna issue checkbox**: Via `mcp__MCP_DOCKER__issue_write` aggiorna task completati
13. **Chiudi issue**: Stato `closed`, reason `completed`
14. **Merge**: Al branch padre
15. **Cleanup**: Elimina branch locale (`git branch -D`), prune remotes (`git remote prune origin`)

**Regole critiche**:
- Non skippare errori, replica e crea guardie
- Per compiti complessi crea sub-issue
- Se confidenza < 95% chiedi
- Non interrompere workflow, pianifica prima
- Non aspettare CI (alpha)
- No nuovi warning
- Code review: risolvi SOLO problemi con score >= 75 (ignora score < 75)
