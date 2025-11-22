# GitHub Actions - Implementation Checklist Completa

**Ultimo aggiornamento**: 2025-11-19
**Voto corrente**: 7.5/10
**Voto target**: 9/10

---

## 📊 Progress Tracker

```
Fase 1 (CRITICO)     [ ] 0/3   (0%)   Entro: 1 settimana
Fase 2 (IMPORTANTE)  [ ] 0/4   (0%)   Entro: 2 settimane
Fase 3 (MIGLIORAMENT)[ ] 0/5   (0%)   Entro: 1 mese
────────────────────────────────────────────────────
TOTALE               [ ] 0/12  (0%)
```

---

## 🔴 FASE 1 - Fix Critici Sicurezza (Entro 1 settimana)

### Setup Iniziale
- [ ] **Crea branch**: `feature/phase-1-security-fixes`
- [ ] **Notifica team**: Email/Slack con piano implementazione
- [ ] **Backup**: Verifica che tutto sia committato e pushato

---

### Issue 1.1: Credenziali Hardcoded K6 (2h)

**Priorità**: 🔴 CRITICO

#### Setup Secrets
- [ ] Genera password PostgreSQL sicura
  ```bash
  openssl rand -base64 32
  ```
- [ ] GitHub → Settings → Secrets → New repository secret
- [ ] Crea `TEST_POSTGRES_USER` = `meeple_test`
- [ ] Crea `TEST_POSTGRES_PASSWORD` = `<password-generata>`
- [ ] Crea `TEST_DB_CONNECTION_STRING`
  ```
  Host=localhost;Port=5432;Database=meepleai_test;Username=meeple_test;Password=<password>;Maximum Pool Size=100
  ```

#### Modifica Workflow
- [ ] Apri `.github/workflows/k6-performance.yml`
- [ ] **Linea 42-43**: Sostituisci con secrets
  ```yaml
  POSTGRES_USER: ${{ secrets.TEST_POSTGRES_USER || 'postgres' }}
  POSTGRES_PASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
  ```
- [ ] **Linea 92-95**: Aggiorna comando create database
  ```yaml
  env:
    PGPASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
  run: |
    psql -h localhost -U ${{ secrets.TEST_POSTGRES_USER || 'postgres' }} -c "CREATE DATABASE meepleai_test;" || true
  ```
- [ ] **Linea 96-102**: Usa TEST_DB_CONNECTION_STRING
  ```yaml
  env:
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING || 'Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres' }}
  ```
- [ ] **Linea 111-122**: Usa TEST_DB_CONNECTION_STRING
- [ ] **Linea 262-269**: Aggiorna cleanup
  ```yaml
  env:
    PGPASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
  run: |
    psql -h localhost -U ${{ secrets.TEST_POSTGRES_USER || 'postgres' }} -c "DROP DATABASE IF EXISTS meepleai_test;" 2>/dev/null || true
  ```

#### Testing
- [ ] Commit modifiche
- [ ] Push al branch
- [ ] GitHub → Actions → K6 Performance Tests → Run workflow
- [ ] Seleziona branch, test_type: `smoke`
- [ ] Run workflow
- [ ] Verifica logs: cerca `***` (secrets mascherati)
- [ ] Verifica workflow completa con successo

---

### Issue 1.2: Permissions Esplicite (2h)

**Priorità**: 🔴 ALTO

#### ci.yml
- [ ] Apri `.github/workflows/ci.yml`
- [ ] Dopo linea 28 (dopo `concurrency:`), aggiungi:
  ```yaml
  permissions:
    contents: read
    checks: write
    pull-requests: read
  ```
- [ ] Commit: `security: add explicit permissions to ci workflow`

#### migration-guard.yml
- [ ] Apri `.github/workflows/migration-guard.yml`
- [ ] Dopo linea 7 (dopo `paths:`), aggiungi:
  ```yaml
  permissions:
    contents: read
    pull-requests: write
  ```
- [ ] Commit: `security: add explicit permissions to migration-guard workflow`

#### lighthouse-ci.yml
- [ ] Apri `.github/workflows/lighthouse-ci.yml`
- [ ] Dopo linea 17 (dopo `concurrency:`), aggiungi:
  ```yaml
  permissions:
    contents: read
    pull-requests: write
    checks: write
  ```
- [ ] Commit: `security: add explicit permissions to lighthouse-ci workflow`

#### storybook-deploy.yml
- [ ] Apri `.github/workflows/storybook-deploy.yml`
- [ ] Dopo linea 15 (dopo ultimo `paths:`), aggiungi:
  ```yaml
  permissions:
    contents: read
    pull-requests: write
  ```
- [ ] Commit: `security: add explicit permissions to storybook-deploy workflow`

#### k6-performance.yml
- [ ] Apri `.github/workflows/k6-performance.yml`
- [ ] Dopo linea 29 (dopo `env:`), aggiungi:
  ```yaml
  permissions:
    contents: read
    pull-requests: write
    actions: read
  ```
- [ ] Commit: `security: add explicit permissions to k6-performance workflow`

#### Testing
- [ ] Push tutti i commit
- [ ] Crea draft PR
- [ ] Verifica che tutti i workflow partano
- [ ] Controlla logs per errori tipo "Resource not accessible by integration"
- [ ] Se errori, aggiungi permission mancante e re-push

---

### Issue 1.3: SecurityCodeScan Deprecato (1h)

**Priorità**: 🟡 MEDIO

#### Aggiorna Progetto
- [ ] Apri `apps/api/src/Api/Api.csproj`
- [ ] Trova `<ItemGroup>` degli analyzer
- [ ] Aggiungi:
  ```xml
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.32.0.97167">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Meziantou.Analyzer" Version="2.0.163">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
  ```

#### Configura Regole
- [ ] Crea/modifica `apps/api/.editorconfig`
- [ ] Copia le security rules da `phase-1-critical-security-fixes.md` (linee con `dotnet_diagnostic.CA*`)
- [ ] Commit: `security: add modern .NET security analyzers`

#### Aggiorna Workflow
- [ ] Apri `.github/workflows/security-scan.yml`
- [ ] **Rimuovi completamente** lo step "Install Security Code Scan Analyzer" (linee 249-252)
- [ ] **Sostituisci** lo step "Build with Security Analysis" (linee 254-318) con:
  ```yaml
  - name: Build with Modern Security Analyzers
    id: security_build
    run: |
      dotnet build --configuration Release /p:TreatWarningsAsErrors=true > security-scan.log 2>&1 || true
      cat security-scan.log

      set +e
      SECURITY_WARNINGS=$(grep -iE "warning (CA[0-9]{4}|S[0-9]{4}):" security-scan.log || true)
      set -e

      if [ -n "$SECURITY_WARNINGS" ]; then
        echo "## ⚠️ Security Warnings Found" >> $GITHUB_STEP_SUMMARY
        echo "**Total:** $(echo "$SECURITY_WARNINGS" | wc -l)" >> $GITHUB_STEP_SUMMARY
        echo "<details><summary>Details</summary>" >> $GITHUB_STEP_SUMMARY
        echo '```' >> $GITHUB_STEP_SUMMARY
        echo "$SECURITY_WARNINGS" >> $GITHUB_STEP_SUMMARY
        echo '```' >> $GITHUB_STEP_SUMMARY
        echo "</details>" >> $GITHUB_STEP_SUMMARY
        exit 1
      else
        echo "✅ No security warnings" >> $GITHUB_STEP_SUMMARY
      fi
  ```

#### Testing
- [ ] Build locale: `cd apps/api/src/Api && dotnet build --configuration Release`
- [ ] Verifica warnings: dovrebbero apparire nuovi analyzer warnings
- [ ] Fixa tutti i warning trovati (se ce ne sono)
- [ ] Commit: `fix: resolve security analyzer warnings`
- [ ] Push e verifica CI

---

### Completamento Fase 1
- [ ] Tutti gli issue 1.1, 1.2, 1.3 completati
- [ ] Tutti i workflow testati manualmente
- [ ] Nessun security warning o error
- [ ] **Crea PR**: "🔴 Phase 1: Critical Security Fixes"
- [ ] PR description include checklist Fase 1
- [ ] Request review dal team lead
- [ ] CI passa tutti i check
- [ ] Merge su main
- [ ] **Aggiorna README.md** in questa cartella con progress

**Criterio di successo**: Voto sicurezza da 7/10 → 9/10

---

## 🟡 FASE 2 - Standardizzazione (Entro 2 settimane)

### Setup
- [ ] **Attendi** completamento e merge Fase 1
- [ ] **Crea branch**: `feature/phase-2-standardization`
- [ ] **Notifica team**: Inizio Fase 2

---

### Issue 2.1: Standardizzare Artifact Versions (1h)

**Priorità**: 🟡 MEDIO

#### Find & Replace Globale
- [ ] Apri terminale nel root del progetto
- [ ] **security-scan.yml**: Cerca `@v4`, sostituisci con `@v6` (4 occorrenze)
- [ ] **lighthouse-ci.yml**: Cerca `@v4`, sostituisci con `@v6` (7 occorrenze)
- [ ] **k6-performance.yml**:
  - Cerca `@v4`, sostituisci con `@v6`
  - **SPECIALE**: Linea 175 usa `dawidd6/action-download-artifact@v3`
  - Sostituisci con il nuovo codice (vedi `phase-2-standardization-improvements.md` linee 80-115)

#### Verifica
- [ ] `grep -r "upload-artifact@v[0-5]" .github/workflows/`
- [ ] Verifica che torni ZERO risultati (o solo v6)
- [ ] `grep -r "download-artifact@v[0-5]" .github/workflows/`
- [ ] Verifica che torni ZERO risultati (o solo v6)

#### Testing
- [ ] Commit: `chore: standardize artifact actions to v6`
- [ ] Push
- [ ] Trigger manualmente ogni workflow modificato
- [ ] Verifica upload/download funzionano

---

### Issue 2.2: Riattivare Coverage Threshold (2h)

**Priorità**: 🟡 MEDIO

#### Verifica Coverage Attuale
- [ ] `cd apps/web`
- [ ] `pnpm test:coverage`
- [ ] Annota i valori:
  ```
  Statements: ____%
  Branches:   ____%
  Functions:  ____%
  Lines:      ____%
  ```
- [ ] Se TUTTI >= 90%, procedi. Altrimenti, segui piano incremento

#### Se Coverage < 90%
- [ ] Apri `coverage/lcov-report/index.html`
- [ ] Identifica i 10 file con coverage più bassa
- [ ] Per ogni file:
  - [ ] Leggi il codice
  - [ ] Identifica branch/function non testate
  - [ ] Scrivi test mancanti
  - [ ] Verifica coverage aumenti
- [ ] Ripeti finché tutti i metric >= 90%

#### Rimuovi || true
- [ ] Apri `.github/workflows/ci.yml`
- [ ] **Linea 119**: Rimuovi `|| true`
  ```yaml
  # PRIMA
  run: pnpm test:coverage || true

  # DOPO
  run: pnpm test:coverage
  ```
- [ ] **Linea 119-122**: Aggiorna commento
  ```yaml
  # PRIMA
  # TODO: Re-enable after coverage improvements

  # DOPO
  # Coverage threshold enforced: 90% for statements, branches, functions, lines
  ```

#### Verifica Jest Config
- [ ] Apri `apps/web/jest.config.js`
- [ ] Verifica `coverageThreshold`:
  ```javascript
  coverageThreshold: {
    global: {
      statements: 90,
      branches: 90,
      functions: 90,
      lines: 90
    }
  }
  ```

#### Testing
- [ ] `cd apps/web && pnpm test:coverage`
- [ ] Verifica che passi (exit code 0)
- [ ] Commit: `test: enforce 90% coverage threshold`
- [ ] Push e verifica CI

---

### Issue 2.3: Riattivare ESLint (2h)

**Priorità**: 🟡 MEDIO

#### Opzione A: Prova Upgrade Next.js
- [ ] `cd apps/web`
- [ ] `pnpm update next@latest`
- [ ] `pnpm lint`
- [ ] Se funziona senza circular dependency warning:
  - [ ] ✅ Procedi con riattivazione
- [ ] Se ancora broken:
  - [ ] `git restore package.json pnpm-lock.yaml`
  - [ ] Prova Opzione B

#### Opzione B: ESLint CLI Diretto
- [ ] Apri `apps/web/package.json`
- [ ] Modifica script:
  ```json
  "lint": "eslint . --ext .js,.jsx,.ts,.tsx --max-warnings 0"
  ```
- [ ] `pnpm lint`
- [ ] Se funziona, procedi

#### Riattiva nel Workflow
- [ ] Apri `.github/workflows/ci.yml`
- [ ] **Linea 106-108**: Decommenta
  ```yaml
  # PRIMA
  # TODO: Re-enable after migrating to ESLint CLI
  # - name: Lint
  #   run: pnpm lint

  # DOPO
  - name: Lint
    run: pnpm lint
  ```

#### Fix Errori
- [ ] `cd apps/web && pnpm lint`
- [ ] Se ci sono errori:
  - [ ] `pnpm lint:fix` (auto-fix)
  - [ ] Fixa manualmente i rimanenti
  - [ ] `pnpm lint` (verifica 0 errori)

#### Testing
- [ ] Commit: `fix: re-enable ESLint in CI`
- [ ] Push
- [ ] Verifica CI passa "Lint" step

---

### Issue 2.4: Standardizzare Retention Days (1h)

**Priorità**: 🟡 BASSO

#### Modifica ci.yml
- [ ] **Linea 202**: `retention-days: 7` → `14`
- [ ] **Linea 261**: `retention-days: 7` → `14`
- [ ] **Linea 403**: `retention-days: 7` → `14`
- [ ] **Linea 606**: `retention-days: 30` (già corretto)

#### Modifica lighthouse-ci.yml
- [ ] **Linea 83**: `retention-days: 1` (corretto, build artifact)
- [ ] **Linea 148**: `retention-days: 7` → `14`
- [ ] **Linea 168**: `retention-days: 7` → `14`
- [ ] **Linea 216**: `retention-days: 7` → `14`

#### Modifica security-scan.yml
- [ ] **Linea 156**: `retention-days: 7` → `30`
- [ ] **Linea 215**: `retention-days: 7` → `30`
- [ ] **Linea 325**: `retention-days: 7` → `30`
- [ ] **Linea 387**: `retention-days: 30` (già corretto)

#### Modifica k6-performance.yml
- [ ] **Linea 172**: `retention-days: 30` (già corretto)
- [ ] **Linea 249**: `retention-days: 7` (già corretto, logs)
- [ ] **Linea 242**: `retention-days: 90` (già corretto, baseline)

#### Documentazione
- [ ] Crea `docs/02-development/ci-cd/artifact-retention-policy.md`
- [ ] Copia contenuto da `phase-2-standardization-improvements.md` (sezione Documentazione)

#### Testing
- [ ] Nessun test specifico (solo metadata)
- [ ] Commit: `chore: standardize artifact retention policies`

---

### Completamento Fase 2
- [ ] Tutti gli issue 2.1-2.4 completati
- [ ] actionlint passa: `actionlint .github/workflows/*.yml`
- [ ] Tutti i workflow testati
- [ ] **Crea PR**: "🟡 Phase 2: Standardization Improvements"
- [ ] Request review
- [ ] CI passa
- [ ] Merge su main
- [ ] **Aggiorna progress** nel README.md

**Criterio di successo**: Consistenza 100%, qualità codice migliorata

---

## 🟢 FASE 3 - Ottimizzazioni (Entro 1 mese)

### Setup
- [ ] **Attendi** merge Fase 2
- [ ] **Crea branch**: `feature/phase-3-optimizations`
- [ ] **Documenta baseline**: Tempo medio workflow attuale

---

### Issue 3.1: Caching Storybook (1h)

- [ ] Apri `.github/workflows/storybook-deploy.yml`
- [ ] **Linea 36-40**: Aggiungi cache
  ```yaml
  - name: Setup Node.js
    uses: actions/setup-node@v6
    with:
      node-version: '20'
      cache: 'pnpm'
      cache-dependency-path: apps/web/pnpm-lock.yaml
  ```
- [ ] Commit: `perf: add pnpm caching to Storybook workflow`
- [ ] Test: Trigger workflow 2 volte, verifica cache hit nel secondo run

---

### Issue 3.2: Notification K6 (2h)

#### Opzione Slack (Raccomandato)
- [ ] Crea Slack Incoming Webhook (vedi `phase-3-optimization-enhancements.md`)
- [ ] Copia Webhook URL
- [ ] GitHub → Settings → Secrets → New: `SLACK_WEBHOOK_URL`
- [ ] Apri `.github/workflows/k6-performance.yml`
- [ ] **Linea 272-284**: Sostituisci con codice Slack notification
- [ ] Commit: `feat: add Slack notifications for k6 failures`

#### Opzione GitHub Issue
- [ ] Apri `.github/workflows/k6-performance.yml`
- [ ] **Linea 272**: Aggiungi `permissions: issues: write`
- [ ] **Linea 276-284**: Sostituisci con codice GitHub issue creation
- [ ] Commit: `feat: auto-create issues for k6 failures`

#### Testing
- [ ] Trigger manualmente workflow con failure simulato
- [ ] Verifica che notifica venga inviata/issue creato

---

### Issue 3.3: Composite Actions (4h)

#### Setup Frontend Action
- [ ] `mkdir -p .github/actions/setup-frontend`
- [ ] Crea `.github/actions/setup-frontend/action.yml`
- [ ] Copia contenuto da `phase-3-optimization-enhancements.md`
- [ ] Testa: `actionlint .github/actions/setup-frontend/action.yml`

#### Setup Backend Action
- [ ] `mkdir -p .github/actions/setup-backend`
- [ ] Crea `.github/actions/setup-backend/action.yml`
- [ ] Copia contenuto da documento
- [ ] Testa: `actionlint .github/actions/setup-backend/action.yml`

#### Setup Playwright Action
- [ ] `mkdir -p .github/actions/setup-playwright`
- [ ] Crea `.github/actions/setup-playwright/action.yml`
- [ ] Copia contenuto da documento
- [ ] Testa: `actionlint .github/actions/setup-playwright/action.yml`

#### Aggiorna Workflow ci.yml
- [ ] Cerca tutti i setup pnpm/Node.js
- [ ] Sostituisci con: `- uses: ./.github/actions/setup-frontend`
- [ ] Cerca tutti i setup .NET
- [ ] Sostituisci con: `- uses: ./.github/actions/setup-backend`
- [ ] Cerca tutti i setup Playwright
- [ ] Sostituisci con: `- uses: ./.github/actions/setup-playwright`

#### Aggiorna Altri Workflow
- [ ] Ripeti per `lighthouse-ci.yml`
- [ ] Ripeti per `storybook-deploy.yml`
- [ ] Ripeti per `security-scan.yml`
- [ ] Ripeti per `k6-performance.yml`

#### Documentazione
- [ ] Crea `.github/actions/README.md`
- [ ] Documenta ogni composite action

#### Testing
- [ ] `actionlint .github/workflows/*.yml`
- [ ] Trigger manualmente ogni workflow modificato
- [ ] Verifica tutti funzionano correttamente

---

### Issue 3.4: Workflow Badges (30m)

- [ ] Apri `README.md` (root del repo)
- [ ] Trova la sezione titolo
- [ ] Aggiungi badge:
  ```markdown
  [![CI](https://github.com/DegrassiAaron/meepleai-monorepo/workflows/ci/badge.svg)](https://github.com/DegrassiAaron/meepleai-monorepo/actions/workflows/ci.yml)
  [![Security](https://github.com/DegrassiAaron/meepleai-monorepo/workflows/Security%20Scanning/badge.svg)](https://github.com/DegrassiAaron/meepleai-monorepo/actions/workflows/security-scan.yml)
  [![K6](https://github.com/DegrassiAaron/meepleai-monorepo/workflows/K6%20Performance%20Tests/badge.svg)](https://github.com/DegrassiAaron/meepleai-monorepo/actions/workflows/k6-performance.yml)
  ```
- [ ] Commit: `docs: add workflow status badges to README`
- [ ] Verifica che badge mostrano status corretto

---

### Issue 3.5: Ottimizzazione Caching (2h)

#### .NET Build Cache
- [ ] Verifica quali workflow hanno già .NET build cache
- [ ] Aggiungi ai workflow mancanti (vedi documento Fase 3)

#### Next.js Cache
- [ ] Aggiungi `.next/cache` caching a `ci.yml`
- [ ] Aggiungi a `lighthouse-ci.yml`

#### Storybook Cache
- [ ] Aggiungi Storybook build cache a `storybook-deploy.yml`

#### Verifica Effectiveness
- [ ] Aggiungi "Cache Statistics" step a ogni workflow
- [ ] Monitora cache hit rate per 1 settimana
- [ ] Target: >80% hit rate

---

### Completamento Fase 3
- [ ] Tutti gli issue 3.1-3.5 completati
- [ ] Performance migliorata (documenta metriche)
- [ ] Cache hit rate > 80%
- [ ] **Crea PR**: "🟢 Phase 3: Optimization Enhancements"
- [ ] PR include:
  - [ ] Performance comparison (before/after)
  - [ ] Cache hit rate metrics
  - [ ] Screenshot badge nel README
- [ ] Request review
- [ ] CI passa
- [ ] Merge su main
- [ ] **Aggiorna progress** nel README.md

**Criterio di successo**: Voto globale da 7.5/10 → 9/10

---

## 📊 Metriche Finali

### Before (Inizio)
```
Voto globale:           7.5/10
Sicurezza:              7/10
Tempo medio CI:         ~14 min
Costo mensile:          $24
Coverage enforcement:   ❌
ESLint:                 ❌
Permissions:            ⚠️ Implicite
Artifact versions:      Inconsistenti
Cache hit rate:         ~70%
Workflow LOC:           ~2,800
Duplicazione:           21%
```

### After (Target)
```
Voto globale:           9/10  ✅
Sicurezza:              9/10  ✅
Tempo medio CI:         ~10 min  ✅
Costo mensile:          $16  ✅
Coverage enforcement:   ✅
ESLint:                 ✅
Permissions:            ✅ Least privilege
Artifact versions:      v6 (100%)  ✅
Cache hit rate:         ~85%  ✅
Workflow LOC:           ~2,200  ✅
Duplicazione:           4%  ✅
```

### ROI Totale
- **Tempo risparmiato**: 4 min/run × 500 runs = **2000 min/mese** (~33 ore)
- **Costo risparmiato**: $8/mese (**$96/anno**)
- **Codice ridotto**: -600 righe (**-21%**)
- **Sicurezza**: 2 critical issues risolti = **Inestimabile**

---

## 🎉 Celebrazione Completamento

Quando tutte e 3 le fasi sono complete:

- [ ] **Annuncia al team**: Slack/email con risultati
- [ ] **Aggiorna docs**:
  - [ ] CLAUDE.md con nuove best practices
  - [ ] SECURITY.md con nuovi analyzer
  - [ ] Changelog con tutte le modifiche
- [ ] **Crea tag**: `git tag -a v2.0-workflows -m "Workflows optimization complete"`
- [ ] **Push tag**: `git push origin v2.0-workflows`
- [ ] **Celebra**: 🎊🎉🚀

---

## 🔧 Troubleshooting

### Se CI fallisce dopo una modifica:
1. **Non panic**: Controlla i logs
2. **Identifica** quale workflow fallisce
3. **Leggi l'errore** attentamente
4. **Cerca** l'errore nella documentazione GitHub Actions
5. **Rollback** se necessario: `git revert <commit-hash>`
6. **Fix** il problema
7. **Re-test** in un nuovo branch
8. **Merge** solo quando tutto è verde

### Se cache non funziona:
1. Verifica cache key: deve cambiare quando dipendenze cambiano
2. Verifica cache path: deve esistere dopo lo step precedente
3. Controlla cache size: max 10 GB per repository
4. Forza cache invalidation: modifica cache key manualmente

### Se notification non viene inviata:
1. Verifica secret configurato correttamente
2. Testa webhook manualmente con `curl`
3. Controlla logs workflow per errori
4. Verifica permissions del job

---

## 📚 Riferimenti Rapidi

- **Documentazione Fase 1**: [phase-1-critical-security-fixes.md](./phase-1-critical-security-fixes.md)
- **Documentazione Fase 2**: [phase-2-standardization-improvements.md](./phase-2-standardization-improvements.md)
- **Documentazione Fase 3**: [phase-3-optimization-enhancements.md](./phase-3-optimization-enhancements.md)
- **Master README**: [README.md](./README.md)
- **GitHub Actions Docs**: https://docs.github.com/en/actions

---

**Creato**: 2025-11-19
**Ultimo aggiornamento**: 2025-11-19
**Prossima review**: Dopo completamento totale
