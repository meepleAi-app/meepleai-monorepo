# Git Hooks Configuration

**Configurazione Git hooks per sviluppo con Visual Studio e Claude Code**

---

## 🎯 Requisito

**IDE (Visual Studio/VS Code)**: Skip pre-commit hooks per velocità
**Claude Code (CLI)**: Esegui pre-commit hooks per validazione qualità

---

## 🔧 Soluzioni Disponibili

### ✅ Soluzione 1: VS Code Settings (RECOMMENDED)

**Visual Studio Code nativo supporta skip hooks** via settings.

**1. Apri Settings JSON**:
- `Ctrl+Shift+P` → "Preferences: Open User Settings (JSON)"
- Oppure: File → Preferences → Settings → Extensions → Git

**2. Aggiungi configurazione**:
```json
{
  "git.allowNoVerifyCommit": true,
  "git.postCommitCommand": "none"
}
```

**3. Uso**:
- **Commit con hooks**: Source Control → Commit (default ✅)
- **Commit senza hooks**: Source Control → Commit → Click checkbox "Skip hooks" ⬜

**Benefit**: UI checkbox per controllo granulare, nessuna modifica ai hook

---

### ✅ Soluzione 2: Husky Native Variable (SIMPLE)

**Husky supporta** `HUSKY=0` per disabilitare tutti hook.

**Configurazione Visual Studio Code**:

**1. Crea file `.vscode/settings.json`** (se non esiste):
```json
{
  "git.allowNoVerifyCommit": true,
  "terminal.integrated.env.windows": {
    "HUSKY": "0"
  }
}
```

**2. Uso**:
- **Visual Studio terminal**: `HUSKY=0` automaticamente settata → hooks skipped
- **Claude Code / External terminal**: Nessuna env var → hooks eseguiti

**Benefit**: Automatic skip in VS Code, manual execution in external terminals

---

### ✅ Soluzione 3: Git Alias (FLEXIBLE)

**Crea alias Git** per commit con/senza hook.

**Setup**:
```bash
# Alias per skip hooks
git config alias.cm 'commit --no-verify'

# Alias normale (con hooks)
git config alias.commit-full 'commit'
```

**Uso**:
- **Visual Studio Source Control terminal**: `git cm -m "message"` (skip hooks)
- **Claude Code**: `git commit -m "message"` (con hooks)

**Benefit**: Controllo esplicito, funziona in ogni IDE

---

### ⚠️ Soluzione 4: Hook Condizionale (ADVANCED)

**Modificare hook** per rilevare chiamata da IDE.

**Modifica `.husky/pre-commit`** (aggiungi all'inizio):
```bash
#!/bin/sh
. "$(dirname "$0")/_/husky.sh"

# Skip hooks if SKIP_HOOKS=1 environment variable is set
if [ "$SKIP_HOOKS" = "1" ]; then
  echo "ℹ️  Skipping pre-commit hooks (SKIP_HOOKS=1)"
  exit 0
fi

# ... resto del hook
```

**Configurazione VS Code** (`.vscode/settings.json`):
```json
{
  "terminal.integrated.env.windows": {
    "SKIP_HOOKS": "1"
  }
}
```

**Benefit**: Automatic skip in VS Code integrated terminal, hook eseguiti da CLI esterno

**Drawback**: Richiede modifica hook (può confliggere con aggiornamenti Husky)

---

## 🎯 Soluzione Raccomandata

### Setup Completo (Soluzione 1 + 2)

**Configura VS Code** (`.vscode/settings.json` - committable):
```json
{
  "git.allowNoVerifyCommit": true,
  "git.enableCommitSigning": false,
  "git.postCommitCommand": "none",
  "git.showCommitInput": true,
  "terminal.integrated.env.windows": {
    "HUSKY": "0"
  }
}
```

**Comportamento**:
- **VS Code Source Control UI**:
  - Checkbox "Skip hooks" disponibile ✅
  - Oppure auto-skip se terminal usa `HUSKY=0`
- **VS Code Integrated Terminal**: `HUSKY=0` attiva → hooks skipped
- **Claude Code / Git Bash / External terminal**: Hooks eseguiti normalmente

---

## 🧪 Test Configuration

**1. Verifica Hooks Attivi**:
```bash
# Da external terminal (hooks DEVONO eseguire)
git commit -m "test" --allow-empty
# Output: 🚀 Running pre-commit checks...
```

**2. Verifica Skip da VS Code**:
```bash
# Da VS Code integrated terminal
git commit -m "test" --allow-empty
# Output: (nessun hook output se HUSKY=0 configurato)
```

**3. Verifica Checkbox UI**:
- Apri Source Control panel in VS Code
- Scrivi commit message
- Verifica presenza checkbox "Skip hooks" ⬜
- Commit con/senza hook a scelta

---

## 🔧 Implementazione Rapida

**Opzione Quick (2 minuti)**:
```bash
# Abilita skip hooks in VS Code globalmente
code --user-data-dir ~ --wait

# Aggiungi a User Settings:
# "git.allowNoVerifyCommit": true
```

**Opzione Project (5 minuti)**:
```bash
# Crea .vscode/settings.json con configurazione completa
cat > .vscode/settings.json << 'EOF'
{
  "git.allowNoVerifyCommit": true,
  "terminal.integrated.env.windows": {
    "HUSKY": "0"
  }
}
EOF

# Committa configurazione (opzionale - per team)
git add .vscode/settings.json
git commit -m "chore(vscode): Configure Git hooks skip for IDE commits"
```

---

## 📋 Hooks Attuali

**Pre-Commit** (`.husky/pre-commit`):
- ✅ Frontend: lint-staged + typecheck (tutte branch)
- ✅ Backend: `dotnet format --verify` (main-dev, main)
- ✅ Security: `detect-secrets` (main-dev, main)
- ✅ Security: `semgrep` (solo main)

**Commit-Msg** (`.husky/commit-msg`):
- Conventional commits validation (opzionale)

**Pre-Push** (`.husky/pre-push`):
- Test suite execution (opzionale)

---

## 🎯 Workflow Comparison

### Scenario 1: Commit da Visual Studio UI

**Con configurazione**:
1. Source Control → Write message
2. Click checkbox "Skip hooks" ⬜
3. Commit → **Instant** (no hook execution)
4. Push quando pronto

**Tempo**: ~5 secondi

---

### Scenario 2: Commit da Claude Code CLI

**Comportamento attuale** (invariato):
1. `git commit -m "message"`
2. Hook eseguiti automaticamente:
   - lint-staged (~10s)
   - typecheck (~15s)
   - dotnet format (~8s)
   - detect-secrets (~5s)
3. Commit created
4. Push

**Tempo**: ~40 secondi (con validation ✅)

---

## ⚠️ Best Practices

### ✅ Quando Skippare Hooks (Visual Studio)
- WIP commits (work in progress)
- Quick fixes durante debugging
- Commit frequenti per backup locale
- Refactoring incrementale

### ❌ NON Skippare Hooks Prima Di
- Push a remote (main-dev, main)
- Pull request creation
- Merge commits
- Release tags

### 🤖 Claude Code Sempre Con Hooks
- Claude commits vanno direttamente a remote
- Validation è essenziale per quality gates
- Hook execution è parte del workflow automatico

---

## 📖 Related Documentation

- [Development Guide](./README.md) - Git workflow
- [Testing Guide](../05-testing/README.md) - Pre-commit tests
- [Git Workflow](../../CLAUDE.md#git-workflow) - Branching strategy

---

**Last Updated**: 2026-01-18
**Maintainer**: Development Team
**Hook Manager**: Husky
**Status**: ✅ Configured
