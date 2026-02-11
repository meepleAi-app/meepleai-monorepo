# Piano di Consolidamento Documentazione

**Data**: 2026-02-11
**Obiettivo**: Consolidare la documentazione rimuovendo duplicati e applicando convenzioni uniformi

---

## 📊 Analisi Problemi Attuali

### 1. Duplicazioni Bounded Contexts
- 11 file normali + 11 file `-COMPLETE.md` = 22 file totali
- Versioni COMPLETE 4-5x più dettagliate (323 vs 1422 righe)
- **Azione**: Mantenere COMPLETE, eliminare versioni brevi

### 2. Convenzioni Naming Inconsistenti
```
UPPERCASE:    README.md, SKILLS-REFERENCE.md, INDEX.md
kebab-case:   quick-start.md, git-workflow.md
snake_case:   backend_testing.md, oauth_testing.md
Mixed:        guida-visualcode.md (italiano)
```

### 3. Struttura Directory Confusa
- Frontend docs sparse: `07-frontend/`, `frontend/`, `design-system/`
- Testing docs sparse: `05-testing/backend/`, `e2e/`, `frontend/`, `performance/`
- Multiple `04-*` directories con overlap

---

## 🎯 Convenzione Unica

### Regola Generale
**Tutti i file**: `kebab-case.md`

**Eccezioni**:
- `README.md` (standard GitHub)
- `CLAUDE.md` (progetto root)

### Esempi
```
✅ Corretti:
- quick-start-guide.md
- backend-testing-patterns.md
- oauth-integration.md
- bounded-context-template.md

❌ Da convertire:
- QUICK_START_GUIDE.md
- backend_testing_patterns.md
- OAuth_integration.md
- BoundedContextTemplate.md
```

---

## 📝 Fase 1: Bounded Contexts (09-bounded-contexts/)

### Azioni
1. **Rimuovere** 11 file brevi:
   - `administration.md` → DELETE
   - `authentication.md` → DELETE
   - `document-processing.md` → DELETE
   - `game-management.md` → DELETE
   - `knowledge-base.md` → DELETE
   - `shared-game-catalog.md` → DELETE
   - `system-configuration.md` → DELETE
   - `user-library.md` → DELETE
   - `user-notifications.md` → DELETE
   - `workflow-integration.md` → DELETE
   - (nota: `session-tracking.md` non esiste)

2. **Rinominare** 11 file COMPLETE (rimuovere `-COMPLETE`):
   - `administration-COMPLETE.md` → `administration.md`
   - `authentication-COMPLETE.md` → `authentication.md`
   - `document-processing-COMPLETE.md` → `document-processing.md`
   - `game-management-COMPLETE.md` → `game-management.md`
   - `knowledge-base-COMPLETE.md` → `knowledge-base.md`
   - `session-tracking-COMPLETE.md` → `session-tracking.md`
   - `shared-game-catalog-COMPLETE.md` → `shared-game-catalog.md`
   - `system-configuration-COMPLETE.md` → `system-configuration.md`
   - `user-library-COMPLETE.md` → `user-library.md`
   - `user-notifications-COMPLETE.md` → `user-notifications.md`
   - `workflow-integration-COMPLETE.md` → `workflow-integration.md`

3. **Aggiornare** riferimenti nel README.md

### Script PowerShell
```powershell
# Step 1: Delete short versions
$shortFiles = @(
    "administration", "authentication", "document-processing",
    "game-management", "knowledge-base", "shared-game-catalog",
    "system-configuration", "user-library", "user-notifications",
    "workflow-integration"
)

foreach ($file in $shortFiles) {
    $path = "docs\09-bounded-contexts\$file.md"
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "✅ Deleted: $file.md"
    }
}

# Step 2: Rename COMPLETE files
$completeFiles = Get-ChildItem "docs\09-bounded-contexts" -Filter "*-COMPLETE.md"
foreach ($file in $completeFiles) {
    $newName = $file.Name -replace "-COMPLETE", ""
    Rename-Item $file.FullName -NewName $newName
    Write-Host "✅ Renamed: $($file.Name) → $newName"
}
```

---

## 📝 Fase 2: Standardizzazione Naming (Globale)

### Conversione Snake_Case → Kebab-Case

**File da rinominare**:

#### 02-development/
- `AZUL_TEST_INSTRUCTIONS.md` → `azul-test-instructions.md`
- `BGG_API_TOKEN_SETUP.md` → `bgg-api-token-setup.md`
- `BRANCH_PROTECTION_SETUP.md` → `branch-protection-setup.md`
- `QUICK_START_GUIDE.md` → `quick-start-guide.md`
- `WORKFLOW_AUDIT_REPORT.md` → `workflow-audit-report.md`

#### 02-development/monitoring/
- `METRICS-LIMITATION.md` → `metrics-limitation.md`

#### 04-deployment/
- `CAPACITY_PLANNING.md` → `capacity-planning.md`
- `NEW-GUIDES-INDEX.md` → `new-guides-index.md`

#### 05-testing/
- `CI_CD_PIPELINE.md` → `ci-cd-pipeline.md`

#### 05-testing/backend/
- `BACKEND_E2E_TESTING.md` → `backend-e2e-testing.md`
- `INTEGRATION_TEST_OPTIMIZATION.md` → `integration-test-optimization.md`

#### 05-testing/e2e/
- `BackgroundRulebookAnalysis-ManualTesting.md` → `background-rulebook-analysis-manual-testing.md`
- `E2E_TEST_GUIDE.md` → `e2e-test-guide.md`
- `RulebookAnalysis-ManualTesting.md` → `rulebook-analysis-manual-testing.md`

#### 07-frontend/epics/
- `EPIC-GC-001-game-carousel-integration.md` → `epic-gc-001-game-carousel-integration.md`
- `EPIC-GC-001-SUMMARY.md` → `epic-gc-001-summary.md`

#### 04-features/admin-dashboard-enterprise/
- `EPICS-AND-ISSUES.md` → `epics-and-issues.md`
- `ISSUE-TRACKING.md` → `issue-tracking.md`
- `ROADMAP.html` → `roadmap.html` (no change)
- `SPECIFICATION.md` → `specification.md`

#### 04-features/private-games-proposal/
- `DESIGN.md` → `design.md`
- `IMPLEMENTATION-PLAN.md` → `implementation-plan.md`
- `USER-STORIES.md` → `user-stories.md`

#### 09-bounded-contexts/
- `DIAGRAM_SUMMARY.md` → `diagram-summary.md`

#### Root docs/
- `INDEX.md` → `index.md`
- `SKILLS-REFERENCE.md` → `skills-reference.md`
- `S3.md` → `s3.md` (già corretto)

### Script PowerShell Conversione Globale
```powershell
# Funzione per convertire a kebab-case
function Convert-ToKebabCase {
    param([string]$name)

    # UPPERCASE_SNAKE → kebab-case
    $result = $name -replace "_", "-"
    # PascalCase → kebab-case
    $result = $result -creplace '([A-Z])', '-$1'
    $result = $result.Trim('-').ToLower()

    return $result
}

# Trova tutti i file .md eccetto README e CLAUDE
$files = Get-ChildItem -Path "docs" -Filter "*.md" -Recurse |
    Where-Object { $_.Name -ne "README.md" -and $_.Name -ne "CLAUDE.md" }

foreach ($file in $files) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $newName = Convert-ToKebabCase $baseName

    # Se il nome è già corretto, skip
    if ($baseName -ceq $newName) { continue }

    $newFullName = Join-Path $file.DirectoryName "$newName.md"

    # Rinomina solo se diverso
    if ($file.FullName -ne $newFullName) {
        Rename-Item $file.FullName -NewName "$newName.md"
        Write-Host "✅ Renamed: $($file.Name) → $newName.md"
    }
}
```

---

## 📝 Fase 3: Consolidamento Struttura

### Frontend Documentation (Unificare in 07-frontend/)

**Azione**: Spostare tutto in `07-frontend/`

#### Da spostare:
- `docs/frontend/` → `docs/07-frontend/legacy/` (se utile) o DELETE (se obsoleto)
- `docs/design-system/` → `docs/07-frontend/design-system/`
- `docs/design-proposals/` → `docs/07-frontend/design-proposals/`

#### Struttura target:
```
07-frontend/
├── README.md
├── design-system/
│   └── cards.md
├── components/
│   ├── game-carousel.md
│   ├── meeple-card.md
│   └── batch-jobs.md
├── epics/
│   └── issues/
├── migrations/
└── layout-*.md files
```

### Testing Documentation (Consolidare 05-testing/)

**Struttura attuale**: OK, ma rinominare file

### Deployment (Consolidare 04-deployment/)

**Problema**: Overlap con `04-admin/`, `04-features/`, `04-infrastructure/`

**Azione**: Valutare se rinominare `04-deployment/` → `04-operations/`

---

## 📝 Fase 4: Rimozione File Obsoleti

### File da Verificare/Rimuovere

#### 01-architecture/overview/archive/
- `consolidation-strategy.md` - obsoleto?

#### 02-development/
- `guida-visualcode.md` - tradurre in inglese o rimuovere (unico file in italiano)

#### Root docs/
- `issue-sequences.html` - spostare in `docs/07-frontend/` o rimuovere

---

## ✅ Checklist Esecuzione

### Pre-requisiti
- [ ] Backup completo repository (git commit)
- [ ] Verifica branch corrente (`main-dev`)

### Esecuzione
- [ ] **Fase 1**: Consolidare Bounded Contexts (script PowerShell)
- [ ] **Fase 2**: Standardizzare naming globale (script PowerShell)
- [ ] **Fase 3**: Riorganizzare frontend docs
- [ ] **Fase 4**: Rimuovere file obsoleti
- [ ] **Fase 5**: Aggiornare tutti i link interni
- [ ] **Fase 6**: Aggiornare INDEX.md root

### Post-execution
- [ ] Test build documentazione (se presente sistema build)
- [ ] Verifica link rotti (strumento markdown-link-check)
- [ ] Commit con messaggio descrittivo
- [ ] PR per revisione

---

## 🔧 Strumenti Utili

### Verifica Link Rotti
```bash
npm install -g markdown-link-check
find docs -name "*.md" -exec markdown-link-check {} \;
```

### Trova Riferimenti a File
```powershell
# Cerca tutti i riferimenti a un file specifico
Get-ChildItem -Path "docs" -Filter "*.md" -Recurse |
    Select-String -Pattern "authentication-COMPLETE.md"
```

---

## 📊 Metriche Attese

### Prima
- File totali: ~440
- Duplicati bounded contexts: 11
- Naming inconsistente: ~35 file
- Directory frontend sparse: 3

### Dopo
- File totali: ~405 (-35)
- Duplicati: 0
- Naming uniforme: 100%
- Directory frontend: 1 unificata

---

## 🎯 Risultati Attesi

1. **Naming Uniforme**: Tutti i file in kebab-case
2. **Zero Duplicati**: Rimossi file COMPLETE + brevi
3. **Struttura Chiara**: Frontend unificato in 07-frontend/
4. **Manutenibilità**: Più facile navigare e aggiornare docs

---

---

## 🚀 Esecuzione Rapida

### Quick Start
```powershell
# 1. Commit corrente (backup)
git add . && git commit -m "docs: pre-consolidamento backup"

# 2. DRY RUN (simulazione sicura)
.\scripts\consolidate-docs-master.ps1 -DryRun

# 3. Esecuzione reale
.\scripts\consolidate-docs-master.ps1

# 4. Verifica risultati
.\scripts\consolidate-docs-verify.ps1
```

### Script Disponibili
- `consolidate-docs-master.ps1` - Esegue tutte le fasi automaticamente
- `consolidate-docs-phase1.ps1` - Solo Fase 1 (Bounded Contexts)
- `consolidate-docs-phase2.ps1` - Solo Fase 2 (Naming)
- `consolidate-docs-verify.ps1` - Verifica integrità finale

**Guida completa**: [`scripts/CONSOLIDAMENTO-README.md`](../scripts/CONSOLIDAMENTO-README.md)

---

**Approvazione**: Richiesta prima di esecuzione
**Tempo stimato**: 10-15 minuti (automatico) + 1-2 ore (aggiornamenti manuali)
**Rischio**: Basso (con backup git e DRY RUN)
