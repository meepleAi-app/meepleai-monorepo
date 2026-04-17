# PDF Git Removal — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rimuovere i PDF dei rulebook (1.3GB) dal repo git e dalla history, risolvere il CI disk-full sul runner staging, e garantire che i PDF vengano gestiti esclusivamente tramite blob storage S3/R2.

**Architecture:** I PDF in `data/rulebook/` sono seed data per il pipeline RAG — servono solo una tantum per creare gli embeddings, non per il build né per il runtime. La soluzione permanente è: (1) fix immediato via sparse checkout nel CI per sbloccare i deploy, (2) rimozione da git history con `git filter-repo`, (3) aggiunta a `.gitignore`. Il pipeline di embedding continua a funzionare tramite upload admin → S3 → pgvector, senza bisogno dei file in git.

**Tech Stack:** GitHub Actions `actions/checkout@v6` sparse-checkout, `git-filter-repo` (Python tool), bash, `.gitignore`

---

## Contesto

- `data/rulebook/` contiene 126 PDF tracciati in git (1.3GB)
- Il runner self-hosted è al 93% di disco (5.5GB liberi su 75GB)
- Il job `Build Images` fallisce con `No space left on device` durante la build dell'immagine Docker Web
- I PDF non sono mai usati nei Docker build context (`./apps/api`, `./apps/web`)
- Il pipeline RAG usa già `IBlobStorageService` → S3/R2 per i PDF caricati dagli admin
- Tutti i job CI usano `${{ vars.RUNNER }}` = self-hosted runner staging

---

## Task 1: Sparse checkout in deploy-staging.yml (fix immediato)

**Files:**
- Modify: `.github/workflows/deploy-staging.yml` (righe 73-75, 121-122, 188-189, 374-375, 461-462)

Aggiungere `sparse-checkout` ai checkout nei job che girano sul self-hosted runner, escludendo `data/`. Questo riduce il checkout da ~1.5GB a ~100MB.

- [ ] **Step 1: Modificare checkout di `detect-changes`**

Riga 73-75, sostituire:
```yaml
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0  # Full history needed to compare against github.event.before on merge commits
```
con:
```yaml
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0  # Full history needed to compare against github.event.before on merge commits
          sparse-checkout: |
            apps/
            infra/
            .github/
            tests/
            scripts/
            packages/
          sparse-checkout-cone-mode: true
```

- [ ] **Step 2: Modificare checkout di `pre-deploy-check`**

Riga 121-122, sostituire:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
```
con:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          sparse-checkout: |
            apps/
            .github/
          sparse-checkout-cone-mode: true
```

- [ ] **Step 3: Modificare checkout del job `build`**

Riga 188-189, sostituire:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
```
con:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          sparse-checkout: |
            apps/
            .github/
          sparse-checkout-cone-mode: true
```

- [ ] **Step 4: Modificare checkout del job `deploy`**

Riga 374-375, sostituire:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
```
con:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          sparse-checkout: |
            infra/
            .github/
          sparse-checkout-cone-mode: true
```

- [ ] **Step 5: Modificare checkout del job `validate`**

Riga 461-462, sostituire:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
```
con:
```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          sparse-checkout: |
            tests/
            apps/web/
            .github/
          sparse-checkout-cone-mode: true
```

- [ ] **Step 6: Verificare che il "Free disk space" step nel job `build` stampi spazio sufficiente**

Nessuna modifica necessaria al job `build` riga 197-206 — rimane invariato. Il sparse checkout da solo risolve il problema.

- [ ] **Step 7: Commit e push del fix CI**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "fix(ci): sparse checkout to exclude data/rulebook (1.3GB) from runner"
git push origin main-dev
```

- [ ] **Step 8: Merge main-dev → main-staging e verificare CI**

```bash
git checkout main-staging
git merge main-dev --no-edit
git push origin main-staging
```

Attendi che il job `Build Images` completi con successo su GitHub Actions.

---

## Task 2: Aggiungere data/rulebook a .gitignore

**Files:**
- Modify: `.gitignore`

Prevenire re-commit accidentali dei PDF in futuro.

- [ ] **Step 1: Aggiungere entry in .gitignore**

Aggiungere alla fine di `.gitignore`:
```gitignore

# Rulebook PDFs — managed via S3/blob storage, not git
# These are seed data for RAG embedding pipeline only
data/rulebook/
data/pdfDocs/
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: exclude data/rulebook and data/pdfDocs from git tracking"
git push origin main-dev
```

---

## Task 3: Rimuovere data/rulebook dalla git history (cleanup permanente)

**Files:**
- Modifica la git history di tutti i branch (main-dev, main-staging, main)
- Crea: `data/rulebook/.gitkeep` (opzionale, per documentare che la dir esiste ma non è tracciata)

> ⚠️ **ATTENZIONE**: Questo task riscrive la git history. Tutti i SHA dei commit cambieranno. Coordinare con il team prima di eseguire. Sul branch `main` potrebbe richiedere bypass delle branch protection rules.

### Pre-requisiti

- [ ] **Step 1: Installare git-filter-repo**

```bash
pip install git-filter-repo
# Verifica
git filter-repo --version
# Expected output: git-filter-repo==2.x.x
```

Se pip non è disponibile:
```bash
# Windows (in Git Bash o PowerShell)
winget install Python.Python.3
# oppure scarica da https://github.com/newren/git-filter-repo/releases
```

- [ ] **Step 2: Verificare lo stato del repo prima della modifica**

```bash
git status
# Expected: nothing to commit, working tree clean

git branch
# Expected: main-dev, main-staging, main

du -sh data/rulebook/
# Expected: ~1.3G
```

### Esecuzione filter-repo

- [ ] **Step 3: Creare backup locale del repo (precauzione)**

```bash
# Dalla directory parent del repo
cp -r meepleai-monorepo-dev meepleai-monorepo-dev-backup-$(date +%Y%m%d)
echo "Backup creato"
```

- [ ] **Step 4: Eseguire git filter-repo per rimuovere data/rulebook/ dalla history**

```bash
cd D:/Repositories/meepleai-monorepo-dev

# Rimuove data/rulebook/ da TUTTA la history (tutti i branch)
git filter-repo --path data/rulebook/ --invert-paths --force

# Verifica: data/rulebook non deve più apparire in nessun commit
git log --all --full-history -- data/rulebook/ | head -5
# Expected: nessun output
```

- [ ] **Step 5: Verificare che i file non siano più tracciati**

```bash
git ls-files data/rulebook/ | wc -l
# Expected: 0

du -sh data/rulebook/ 2>/dev/null || echo "Directory non esiste"
# Expected: "Directory non esiste" oppure "0B"
```

- [ ] **Step 6: Verificare integrità repo**

```bash
git log --oneline -5
# Expected: stessi commit ma con SHA diversi

git status
# Expected: nothing to commit, working tree clean

git fsck --no-dangling
# Expected: nessun errore
```

- [ ] **Step 7: Force push su tutti i branch**

> ⚠️ Force push riscrive la history remota. Assicurarsi che nessuno stia lavorando su branch da questi.

```bash
# Force push main-dev
git push origin main-dev --force-with-lease

# Checkout main-staging e force push
git checkout main-staging
git push origin main-staging --force-with-lease

# Checkout main e force push (potrebbe richiedere bypass branch protection)
git checkout main
git push origin main --force-with-lease
```

Se `--force-with-lease` fallisce per branch protection su `main`:
```bash
# Usa l'opzione con force puro (solo se necessario e autorizzato)
git push origin main --force
```

- [ ] **Step 8: Pulire il clone della directory di lavoro**

```bash
# Verifica finale della dimensione repo
git count-objects -vH
# Il campo "size-pack" dovrebbe essere significativamente ridotto

# Garbage collection per liberare oggetti orfani
git gc --aggressive --prune=now
git count-objects -vH
```

---

## Task 4: Pulizia del self-hosted runner dopo il force push

**Files:** nessuno (operazione SSH sul server)

Dopo il force push, il runner staging ha ancora la vecchia history nella cache `_work`. Il prossimo `actions/checkout` fallirà se tenta di fare `git checkout` su SHA che non esistono più.

- [ ] **Step 1: Eliminare la cache checkout del runner sul server**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 "
  echo '=== Removing stale runner checkout cache ==='
  rm -rf /home/deploy/actions-runner/_work/meepleai-monorepo/meepleai-monorepo
  echo 'Done — next CI run will do fresh clone'
  echo ''
  echo '=== Disk after cleanup ==='
  df -h /
"
```

Expected output:
```
=== Removing stale runner checkout cache ===
Done — next CI run will do fresh clone

=== Disk after cleanup ===
Filesystem      Size  Used Avail Use% Mounted on
/dev/sda1        75G   56G   17G  77% /
```

(Il disco dovrebbe liberare ~6.2GB dal `_work` directory)

- [ ] **Step 2: Triggera un nuovo deploy per verificare che il checkout funzioni**

```bash
# Dalla macchina locale — merge main-dev → main-staging per triggherare CI
git checkout main-dev
git checkout main-staging
git merge main-dev --no-edit
git push origin main-staging
```

Attendi il completamento del workflow su GitHub Actions. Il job `Build Images` deve completare con successo.

---

## Task 5: Verifica finale

- [ ] **Step 1: Verificare disco sul runner dopo CI riuscito**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 "df -h / && docker system df"
```

Expected: disco al 75-80% (vs 97% precedente)

- [ ] **Step 2: Verificare health staging**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "curl -sf http://localhost:8080/health | python3 -m json.tool | head -20"
```

Expected: `"status": "Healthy"`

- [ ] **Step 3: Verificare che data/rulebook non sia in git**

```bash
git ls-files data/rulebook/
# Expected: nessun output (0 files)

git log --all --oneline -- data/rulebook/ | head
# Expected: nessun output
```

- [ ] **Step 4: Commit di chiusura con documentazione**

Aggiungere commento in `.github/workflows/deploy-staging.yml` sopra i checkout con sparse-checkout:

```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          # sparse-checkout excludes data/ (1.3GB PDFs) — runner disk is limited
          # PDFs are managed via S3 blob storage, not git
          sparse-checkout: |
            apps/
            .github/
          sparse-checkout-cone-mode: true
```

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "docs(ci): add comment explaining sparse-checkout rationale"
git push origin main-dev
```

---

## Self-Review

**Spec coverage:**
- ✅ Fix immediato CI (sparse checkout) → Task 1
- ✅ Prevenire re-commit PDF (gitignore) → Task 2
- ✅ Rimozione permanente da history (filter-repo) → Task 3
- ✅ Runner cleanup (cache stale) → Task 4
- ✅ Verifica finale → Task 5

**Placeholder scan:** Nessun TODO, nessun placeholder. Tutti i comandi sono completi con expected output.

**Rischi:**
- Task 3 (filter-repo) riscrive SHA — coordinare con il team
- Task 3 potrebbe richiedere bypass branch protection su `main`
- Task 4 Step 1 elimina il checkout cache — il prossimo run fa fresh clone (più lento, ~2-3 min extra)

**Ordine di esecuzione raccomandato:**
1. Task 1 → sblocca il CI oggi
2. Task 2 → prevenzione (5 min)
3. Task 3 + 4 → pulizia permanente (30 min, da coordinare)
4. Task 5 → verifica
