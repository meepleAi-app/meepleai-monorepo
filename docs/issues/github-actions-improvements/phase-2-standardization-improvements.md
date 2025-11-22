# Fase 2 - Standardizzazione e Miglioramenti

**Priorità**: 🟡 IMPORTANTE
**Timeline**: Entro 2 settimane (dopo Fase 1)
**Stima effort**: 6-8 ore
**Impatto**: Qualità del codice e consistenza

---

## 📋 Task Overview

| Task | File(s) | Effort | Status |
|------|---------|--------|--------|
| Standardizzare artifact versions | Tutti i workflow | 1h | ⬜ Todo |
| Riattivare coverage threshold | ci.yml | 2h | ⬜ Todo |
| Fixare e riattivare ESLint | ci.yml + Next.js config | 2h | ⬜ Todo |
| Standardizzare retention days | Tutti i workflow | 1h | ⬜ Todo |

---

## 🟡 Issue #1: Inconsistenza Versioni Artifact

### Problema
I workflow usano versioni diverse di `actions/upload-artifact` e `actions/download-artifact`.

**Severità**: 🟡 MEDIO
**Impatto**: Possibili incompatibilità, mancanza di nuove feature

### Analisi Versioni Attuali

```
✅ ci.yml:                    @v6
❌ security-scan.yml:         @v4
❌ lighthouse-ci.yml:         @v4
❌ k6-performance.yml:        @v4 (upload), @v6 (alcuni), @v3 (download baseline)
❌ storybook-deploy.yml:      Nessun artifact
❌ migration-guard.yml:       @v6
❌ dependabot-automerge.yml:  Nessun artifact
```

### Differenze tra v4 e v6

| Feature | v4 | v6 |
|---------|----|----|
| Backend storage | Azure Blob | GitHub Artifacts |
| Max artifact size | 5 GB | 10 GB |
| Parallel uploads | No | Yes |
| Retention API | Limited | Full control |
| Performance | Baseline | ~30% faster |
| Breaking changes | - | Artifact naming, merge strategy |

### Soluzione

#### Step 1: Aggiornare security-scan.yml

**File**: `.github/workflows/security-scan.yml`

```yaml
# CERCA E SOSTITUISCI (4 occorrenze)

# ❌ VECCHIO
- name: Upload .NET Vulnerability Report
  uses: actions/upload-artifact@v4

# ✅ NUOVO
- name: Upload .NET Vulnerability Report
  uses: actions/upload-artifact@v6

# Ripeti per:
# - Upload Frontend Vulnerability Report (linea 208)
# - Upload .NET Security Scan Report (linea 320)
# - Upload Semgrep Reports (linea 380)
```

#### Step 2: Aggiornare lighthouse-ci.yml

**File**: `.github/workflows/lighthouse-ci.yml`

```yaml
# CERCA E SOSTITUISCI (7 occorrenze)

# ❌ VECCHIO
uses: actions/upload-artifact@v4
uses: actions/download-artifact@v4

# ✅ NUOVO
uses: actions/upload-artifact@v6
uses: actions/download-artifact@v6

# Occorrenze:
# - Upload Next.js Build Artifact (linea 76)
# - Upload Playwright Report (linea 144)
# - Upload Lighthouse Reports (linea 164)
# - Download Next.js Build (linea 132, 199)
# - Upload Lighthouse CI Results (linea 212)
# - Download PR Lighthouse Results (linea 277)
```

#### Step 3: Aggiornare k6-performance.yml

**File**: `.github/workflows/k6-performance.yml`

```yaml
# CERCA E SOSTITUISCI (5 occorrenze)

# ❌ VECCHIO
uses: actions/upload-artifact@v6  # Già corretto, ma verifica
uses: actions/upload-artifact@v4  # Se presente
uses: actions/download-artifact@v3  # CRITICO: v3 è deprecato

# ✅ NUOVO
uses: actions/upload-artifact@v6
uses: actions/download-artifact@v6

# Occorrenze:
# - Upload performance reports (linea 168)
# - Upload API logs (linea 246)
# - Download baseline report (linea 175) ← CAMBIARE DA v3 a v6
# - Save baseline report (linea 238)

# NOTA: actions/download-artifact@v3 usa il vecchio API
# Aggiorna alla v6 che è retrocompatibile
```

**Aggiornamento specifico per k6 baseline download** (linea 174-182):

```yaml
# ❌ VECCHIO
- name: Download baseline report
  if: github.event_name == 'pull_request'
  uses: dawidd6/action-download-artifact@v3
  continue-on-error: true
  with:
    workflow: k6-performance.yml
    branch: main
    name: k6-performance-reports-latest
    path: tests/k6/baseline/

# ✅ NUOVO (usa GitHub API ufficiale)
- name: Download baseline report
  if: github.event_name == 'pull_request'
  uses: actions/download-artifact@v6
  continue-on-error: true
  with:
    name: k6-performance-reports-latest
    path: tests/k6/baseline/
    repository: ${{ github.repository }}
    run-id: ${{ github.event.workflow_run.id }}
    github-token: ${{ secrets.GITHUB_TOKEN }}
```

**NOTA IMPORTANTE**: `actions/download-artifact@v6` può scaricare solo artifact dallo stesso workflow run. Per scaricare da run precedenti, dobbiamo usare un workaround:

```yaml
# ✅ SOLUZIONE MIGLIORE per download cross-run
- name: Get latest main workflow run
  if: github.event_name == 'pull_request'
  id: get-baseline-run
  uses: actions/github-script@v8
  with:
    script: |
      const runs = await github.rest.actions.listWorkflowRuns({
        owner: context.repo.owner,
        repo: context.repo.repo,
        workflow_id: 'k6-performance.yml',
        branch: 'main',
        status: 'success',
        per_page: 1
      });
      if (runs.data.workflow_runs.length > 0) {
        core.setOutput('run-id', runs.data.workflow_runs[0].id);
        core.setOutput('found', 'true');
      } else {
        core.setOutput('found', 'false');
      }

- name: Download baseline report
  if: github.event_name == 'pull_request' && steps.get-baseline-run.outputs.found == 'true'
  uses: actions/download-artifact@v6
  continue-on-error: true
  with:
    name: k6-performance-reports-latest
    path: tests/k6/baseline/
    run-id: ${{ steps.get-baseline-run.outputs.run-id }}
    github-token: ${{ secrets.GITHUB_TOKEN }}
```

### Testing

1. **Verifica sintassi**:
   ```bash
   # Installa actionlint
   brew install actionlint  # Mac
   # O
   sudo snap install actionlint  # Linux

   # Valida tutti i workflow
   actionlint .github/workflows/*.yml
   ```

2. **Test manuale**:
   - Trigger ogni workflow modificato
   - Verifica che gli artifact vengano uploadati
   - Verifica che i download funzionino
   - Controlla dimensioni e performance

3. **Verifica retention**:
   ```bash
   # Gli artifact dovrebbero apparire nella UI
   # GitHub → Actions → [Workflow Run] → Artifacts
   ```

### Criteri di Accettazione

- [ ] Tutti i workflow usano `@v6` per upload/download artifacts
- [ ] Nessuna action deprecata (v3, v2)
- [ ] K6 baseline download funziona con cross-run
- [ ] actionlint passa senza warning
- [ ] Tutti i workflow testati manualmente con successo

---

## 🟡 Issue #2: Coverage Threshold Disabilitato

### Problema
Il frontend test coverage è disabilitato con `|| true`, permettendo che scenda sotto il 90%.

**Severità**: 🟡 MEDIO
**Impatto**: Qualità del codice può degradare senza notifiche

### Codice Problematico

**File**: `.github/workflows/ci.yml:119`

```yaml
# ❌ DISABILITATO
- name: Unit Tests with Coverage (Target >=90%)
  env:
    NODE_ENV: test
    CI: true
  run: pnpm test:coverage || true  # ← Permette failure
  # TODO: Re-enable coverage threshold enforcement after coverage improvements
```

### Analisi Situazione Attuale

```bash
# Verifica coverage attuale
cd apps/web
pnpm test:coverage

# Output atteso:
# Jest: Collected coverage from 823 files
# Statements   : 90.03% ( ... )
# Branches     : 89.54% ( ... )  ← SOTTO THRESHOLD
# Functions    : 88.23% ( ... )  ← SOTTO THRESHOLD
# Lines        : 90.15% ( ... )
```

**Coverage attuale**: ~90% statements, ma branches e functions sotto threshold.

### Root Cause

1. **Uncovered branches**: Condizioni if/else non testate
2. **Uncovered functions**: Callback e handler non testati
3. **Test mancanti**: Componenti complessi senza test

### Soluzione

#### Step 1: Identificare File Sotto Threshold

```bash
cd apps/web

# Genera report dettagliato
pnpm test:coverage

# Apri il report HTML
open coverage/lcov-report/index.html

# Cerca file con coverage < 90%
# Priorità: componenti critici, API clients, utility functions
```

#### Step 2: Aumentare Coverage nei File Critici

**Esempio**: Se `apps/web/src/lib/api/chat-client.ts` ha 85% coverage:

```typescript
// File: apps/web/src/lib/api/chat-client.ts (esempio)
export class ChatClient {
  async sendMessage(message: string): Promise<ChatResponse> {
    // ❌ Branch non testato: error handling
    if (!message || message.trim().length === 0) {
      throw new Error('Message cannot be empty');
    }

    try {
      const response = await fetch('/api/chat', {
        method: 'POST',
        body: JSON.stringify({ message })
      });

      // ❌ Branch non testato: non-200 response
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      return await response.json();
    } catch (error) {
      // ❌ Error handling non testato
      console.error('Chat error:', error);
      throw error;
    }
  }
}
```

**Test da aggiungere**: `apps/web/src/lib/api/__tests__/chat-client.test.ts`

```typescript
import { ChatClient } from '../chat-client';

describe('ChatClient', () => {
  let client: ChatClient;

  beforeEach(() => {
    client = new ChatClient();
  });

  // ✅ Test happy path (già esistente)
  it('should send message successfully', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ response: 'Hi!' })
    });

    const result = await client.sendMessage('Hello');
    expect(result.response).toBe('Hi!');
  });

  // ✅ AGGIUNGERE: Test empty message branch
  it('should throw error for empty message', async () => {
    await expect(client.sendMessage('')).rejects.toThrow('Message cannot be empty');
    await expect(client.sendMessage('   ')).rejects.toThrow('Message cannot be empty');
  });

  // ✅ AGGIUNGERE: Test HTTP error branch
  it('should throw error for non-OK response', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 500
    });

    await expect(client.sendMessage('Hello')).rejects.toThrow('HTTP 500');
  });

  // ✅ AGGIUNGERE: Test network error
  it('should handle network errors', async () => {
    global.fetch = jest.fn().mockRejectedValue(new Error('Network error'));

    await expect(client.sendMessage('Hello')).rejects.toThrow('Network error');
  });
});
```

#### Step 3: Configurare Jest per Enforcing Threshold

**File**: `apps/web/jest.config.js`

```javascript
// Verifica la configurazione attuale
module.exports = {
  // ... existing config

  // ✅ Assicurati che coverageThreshold sia configurato
  coverageThreshold: {
    global: {
      statements: 90,
      branches: 90,    // ← Questo era probabilmente più basso
      functions: 90,   // ← Questo era probabilmente più basso
      lines: 90
    }
  },

  // ✅ Opzionale: Per file specifici
  coverageThreshold: {
    global: {
      statements: 90,
      branches: 90,
      functions: 90,
      lines: 90
    },
    // File critici devono avere coverage ancora più alto
    './src/lib/api/**/*.ts': {
      statements: 95,
      branches: 95,
      functions: 95,
      lines: 95
    }
  }
};
```

#### Step 4: Rimuovere `|| true` dal Workflow

**File**: `.github/workflows/ci.yml:119`

```yaml
# ❌ VECCHIO
- name: Unit Tests with Coverage (Target >=90%)
  env:
    NODE_ENV: test
    CI: true
  run: pnpm test:coverage || true
  # TODO: Re-enable after coverage improvements

# ✅ NUOVO
- name: Unit Tests with Coverage (Enforce >=90%)
  env:
    NODE_ENV: test
    CI: true
  run: pnpm test:coverage
  # Coverage threshold enforced: 90% for statements, branches, functions, lines
```

### Piano di Incremento Coverage

Se la coverage attuale è sotto il 90%, segui questo piano:

1. **Settimana 1**: Porta statements al 90%
   - Focus: componenti più usati
   - Aggiungi test per happy paths mancanti

2. **Settimana 2**: Porta branches al 90%
   - Focus: error handling e edge cases
   - Aggiungi test per condizioni if/else

3. **Fine Settimana 2**: Porta functions al 90%
   - Focus: callback e event handlers
   - Aggiungi test per funzioni di utility

4. **Enforcement**: Rimuovi `|| true` solo quando tutto è >= 90%

### Testing

```bash
# Step 1: Verifica coverage locale
cd apps/web
pnpm test:coverage

# Step 2: Se >= 90%, rimuovi || true e testa CI
git checkout -b feature/enable-coverage-threshold
# ... rimuovi || true dal workflow
git commit -m "feat: enable coverage threshold enforcement"
git push

# Step 3: Verifica che CI passi
# GitHub → Actions → CI workflow
```

### Criteri di Accettazione

- [ ] Coverage >= 90% per statements, branches, functions, lines
- [ ] `|| true` rimosso da ci.yml
- [ ] Jest configurato con coverageThreshold
- [ ] CI fallisce se coverage scende sotto 90%
- [ ] Report HTML disponibile negli artifacts
- [ ] Team formato su come mantenere coverage alto

---

## 🟡 Issue #3: ESLint Disabilitato

### Problema
ESLint è disabilitato a causa di un bug di Next.js 15.5.6 con circular dependencies.

**Severità**: 🟡 MEDIO
**Impatto**: Code style e quality issues non catturati in CI

### Codice Problematico

**File**: `.github/workflows/ci.yml:106-108`

```yaml
# ❌ DISABILITATO
# TODO: Re-enable after migrating to ESLint CLI (Next.js 15.5.6 circular dependency bug)
# - name: Lint
#   run: pnpm lint
```

### Root Cause

Next.js 15.5.6 ha un bug noto con ESLint che causa circular dependency warnings:

```
Warning: Circular dependency detected:
node_modules/next/dist/build/webpack/loaders/next-swc-loader.js ->
node_modules/next/dist/compiled/@vercel/turbopack-node/... ->
...
```

### Soluzione

#### Opzione 1: Upgrade Next.js (Raccomandato)

**Step 1**: Verifica se il bug è risolto in versioni più recenti

```bash
cd apps/web

# Controlla versione attuale
cat package.json | grep "next"
# Output: "next": "15.5.6"

# Controlla ultime versioni disponibili
pnpm info next versions --json | jq -r '.[-5:][]'
# Output: 15.5.7, 15.5.8, 15.6.0, ...
```

**Step 2**: Prova upgrade a una versione più recente

```bash
# Backup
cp package.json package.json.backup
cp pnpm-lock.yaml pnpm-lock.yaml.backup

# Upgrade Next.js
pnpm update next@latest

# Oppure versione specifica
pnpm update next@15.6.0
```

**Step 3**: Testa che ESLint funzioni

```bash
# Test locale
pnpm lint

# Se funziona:
# ✅ Il bug è risolto, procedi
# ❌ Se ancora broken, prova Opzione 2
```

**Step 4**: Riattiva nel workflow

```yaml
# ✅ NUOVO
- name: Lint
  run: pnpm lint
```

#### Opzione 2: Use ESLint CLI Direttamente (Workaround)

Se l'upgrade non risolve, bypassa Next.js ESLint wrapper:

**Step 1**: Configura ESLint standalone

**File**: `apps/web/.eslintrc.json`

```json
{
  "extends": [
    "next/core-web-vitals",
    "plugin:@typescript-eslint/recommended",
    "prettier"
  ],
  "parser": "@typescript-eslint/parser",
  "parserOptions": {
    "ecmaVersion": 2021,
    "sourceType": "module",
    "ecmaFeatures": {
      "jsx": true
    },
    "project": "./tsconfig.json"
  },
  "plugins": ["@typescript-eslint"],
  "rules": {
    // Custom rules...
  },
  "ignorePatterns": [
    "node_modules/",
    ".next/",
    "out/",
    "coverage/",
    "storybook-static/",
    "playwright-report/",
    "lighthouse-reports/"
  ]
}
```

**Step 2**: Aggiorna script in package.json

```json
{
  "scripts": {
    // ❌ VECCHIO
    "lint": "next lint",

    // ✅ NUOVO: usa ESLint direttamente
    "lint": "eslint . --ext .js,.jsx,.ts,.tsx --max-warnings 0",
    "lint:fix": "eslint . --ext .js,.jsx,.ts,.tsx --fix"
  }
}
```

**Step 3**: Verifica che funzioni

```bash
cd apps/web

# Test
pnpm lint

# Se ci sono errori, fixali
pnpm lint:fix

# Verifica
pnpm lint
# ✅ Dovrebbe passare senza circular dependency warnings
```

**Step 4**: Riattiva nel workflow

```yaml
# ✅ NUOVO
- name: Lint
  run: pnpm lint
```

#### Opzione 3: Temporaneo - Lint Solo File Modificati (Interim)

Se entrambe le opzioni falliscono, linta solo i file modificati nel PR:

```yaml
# ✅ INTERIM SOLUTION
- name: Lint Changed Files Only
  run: |
    # Get list of changed TS/TSX files
    CHANGED_FILES=$(git diff --name-only --diff-filter=ACMRT ${{ github.event.pull_request.base.sha }} ${{ github.sha }} | grep -E '\.(ts|tsx|js|jsx)$' || true)

    if [ -n "$CHANGED_FILES" ]; then
      echo "Linting changed files:"
      echo "$CHANGED_FILES"
      pnpm eslint $CHANGED_FILES --max-warnings 0
    else
      echo "No TypeScript/JavaScript files changed"
    fi
```

### Testing

```bash
# Test locale
cd apps/web

# 1. Verifica che lint funzioni
pnpm lint

# 2. Verifica che typecheck funzioni
pnpm typecheck

# 3. Verifica che build funzioni
pnpm build

# 4. Se tutto passa, commit e push
git add .
git commit -m "fix: re-enable ESLint in CI"
git push

# 5. Verifica CI
# GitHub → Actions → CI workflow → Check "Lint" step
```

### Criteri di Accettazione

- [ ] ESLint esegue senza circular dependency warnings
- [ ] Lint step riattivato in ci.yml
- [ ] Nessun lint error nel codebase
- [ ] CI fallisce se vengono introdotti lint errors
- [ ] Documentazione aggiornata con soluzione scelta

---

## 🟡 Issue #4: Standardizzare Retention Days

### Problema
Gli artifact hanno retention inconsistenti, rendendo difficile trovare report storici.

**Severità**: 🟡 BASSO
**Impatto**: Confusione, spreco storage, difficoltà debug

### Analisi Retention Attuale

```yaml
# ci.yml
retention-days: 7      # Playwright reports, coverage
retention-days: 30     # RAG evaluation

# security-scan.yml
retention-days: 7      # Vulnerability reports
retention-days: 30     # Security scans, Semgrep

# lighthouse-ci.yml
retention-days: 7      # Lighthouse reports
retention-days: 1      # Build artifacts

# k6-performance.yml
retention-days: 30     # Performance reports
retention-days: 7      # API logs
retention-days: 90     # Baseline (main branch)

# migration-guard.yml
retention-days: 30     # Migration SQL scripts
```

### Soluzione: Strategia di Retention Standardizzata

| Tipo Artifact | Retention | Rationale |
|---------------|-----------|-----------|
| **Build artifacts temporanei** | 1 giorno | Solo per trasferire tra job |
| **Debug artifacts** (logs, reports) | 7 giorni | Debug recente |
| **Test reports** (E2E, coverage) | 14 giorni | Review e analisi |
| **Security scans** | 30 giorni | Compliance audit |
| **Performance baselines** | 90 giorni | Trend analysis |
| **Production artifacts** | 180 giorni | Rollback capability |

#### Implementazione

**File**: Tutti i workflow con artifact upload

```yaml
# ✅ TEMPLATE STANDARDIZZATO

# 1. Build artifacts (temporary transfer)
- name: Upload Next.js Build Artifact
  uses: actions/upload-artifact@v6
  with:
    name: nextjs-build-${{ github.sha }}
    path: apps/web/.next/
    retention-days: 1  # ← Solo per trasferire tra job

# 2. Debug artifacts
- name: Upload API Logs
  if: always()
  uses: actions/upload-artifact@v6
  with:
    name: api-logs-${{ github.run_number }}
    path: api.log
    retention-days: 7  # ← Debug recente

# 3. Test reports
- name: Upload Playwright Report
  if: failure()
  uses: actions/upload-artifact@v6
  with:
    name: playwright-report-${{ github.run_number }}
    path: apps/web/playwright-report/
    retention-days: 14  # ← Review e analisi (CAMBIATO da 7)

- name: Upload Web Coverage Report
  if: failure()
  uses: actions/upload-artifact@v6
  with:
    name: web-coverage-report-${{ github.run_number }}
    path: apps/web/coverage-report/
    retention-days: 14  # ← Review e analisi (CAMBIATO da 7)

# 4. Security scans
- name: Upload Semgrep Reports
  uses: actions/upload-artifact@v6
  if: always()
  with:
    name: semgrep-security-reports
    path: semgrep-results.sarif
    retention-days: 30  # ← Compliance audit

# 5. Performance baselines
- name: Save baseline report
  if: github.ref == 'refs/heads/main' && github.event_name == 'schedule'
  uses: actions/upload-artifact@v6
  with:
    name: k6-performance-reports-latest
    path: tests/k6/reports/summary.json
    retention-days: 90  # ← Trend analysis

# 6. Migration SQL (important for rollback)
- name: Upload SQL Migration Scripts
  if: steps.generate_sql.outputs.sql_generated == 'true'
  uses: actions/upload-artifact@v6
  with:
    name: migration-sql-preview-${{ github.event.pull_request.number }}
    path: apps/api/migration-*.sql
    retention-days: 30  # ← Rollback capability (CAMBIATO da 30, ok)
```

#### Modifiche Specifiche per File

**ci.yml**:
```yaml
# Linea 202, 261, 403
retention-days: 7  → retention-days: 14

# Linea 606
retention-days: 30  # ← OK, già corretto per RAG evaluation
```

**lighthouse-ci.yml**:
```yaml
# Linea 83
retention-days: 1  # ← OK, build artifact temporaneo

# Linea 148, 168, 216
retention-days: 7  → retention-days: 14
```

**k6-performance.yml**:
```yaml
# Linea 172
retention-days: 30  # ← OK, performance reports

# Linea 249
retention-days: 7  # ← OK, API logs debug

# Linea 242
retention-days: 90  # ← OK, baseline
```

**migration-guard.yml**:
```yaml
# Linea 139
retention-days: 30  # ← OK, migration SQL
```

**security-scan.yml**:
```yaml
# Linea 156
retention-days: 7  → retention-days: 30  # Vulnerability reports

# Linea 215
retention-days: 7  → retention-days: 30  # Frontend vulnerability

# Linea 325
retention-days: 7  → retention-days: 30  # Security scan report

# Linea 387
retention-days: 30  # ← OK, Semgrep reports
```

### Documentazione

Crea un nuovo documento: `docs/02-development/ci-cd/artifact-retention-policy.md`

```markdown
# CI/CD Artifact Retention Policy

## Retention Standards

| Category | Retention | Examples |
|----------|-----------|----------|
| Build artifacts | 1 day | Next.js builds, compiled binaries |
| Debug artifacts | 7 days | API logs, test logs |
| Test reports | 14 days | Playwright, Jest coverage |
| Security scans | 30 days | CodeQL, Semgrep, dependency scans |
| Performance data | 90 days | k6 baselines, Lighthouse trends |

## Rationale

- **1 day**: Enough to transfer between workflow jobs
- **7 days**: Recent debugging, most bugs found within a week
- **14 days**: Test analysis, PR reviews can take 1-2 weeks
- **30 days**: Compliance requirements, security audits
- **90 days**: Performance trends, quarterly comparisons

## Cost Impact

GitHub Actions storage:
- Free tier: 500 MB
- Pro: 2 GB
- Estimated usage: ~1.5 GB/month
- Cost: Free (within limits)

## Cleanup

Old artifacts are automatically deleted by GitHub after retention expires.
Manual cleanup: Settings → Actions → Artifacts → Delete old artifacts
```

### Testing

Nessun test specifico necessario (solo metadata change).

### Criteri di Accettazione

- [ ] Tutti i workflow usano retention standardizzata
- [ ] Documentazione policy creata
- [ ] CLAUDE.md aggiornato con reference alla policy
- [ ] Team formato sulle nuove retention

---

## 📝 Checklist Fase 2

### Pre-Implementation
- [ ] Fase 1 completata e mergiata
- [ ] Branch creato: `feature/phase-2-standardization`
- [ ] Team review delle modifiche proposte

### Implementation
- [ ] Issue #1: Artifact versions standardizzate
- [ ] Issue #2: Coverage threshold riattivato
- [ ] Issue #3: ESLint riattivato
- [ ] Issue #4: Retention days standardizzati

### Testing
- [ ] actionlint passa per tutti i workflow
- [ ] Tutti i workflow testati manualmente
- [ ] Artifact upload/download funzionano
- [ ] Coverage enforcement blocca PR se < 90%
- [ ] ESLint blocca PR se ci sono errori

### Documentation
- [ ] Artifact retention policy documentata
- [ ] CLAUDE.md aggiornato
- [ ] Migration guide per il team
- [ ] Changelog aggiornato

### Review & Merge
- [ ] PR creata
- [ ] Code review approvato
- [ ] CI passa tutti i check
- [ ] Merge su main

---

## 📊 Metriche di Successo

### Before
- Artifact versions: Inconsistenti (v3, v4, v6)
- Coverage enforcement: ❌ Disabilitato
- ESLint: ❌ Disabilitato
- Retention: Inconsistente (1-90 giorni)

### After
- Artifact versions: ✅ Tutte v6
- Coverage enforcement: ✅ Attivo (>=90%)
- ESLint: ✅ Attivo e funzionante
- Retention: ✅ Standardizzata per categoria

---

**Creato**: 2025-11-19
**Ultimo aggiornamento**: 2025-11-19
