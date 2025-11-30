# 📋 FASE 1: Normalizzazione Priorità - Guida Esecutiva

**Obiettivo**: Convertire tutte le 132 issue con label priority in formato `[Px]` standardizzato nel titolo

**Tempo Stimato**: 2-4 ore (1h preparazione + 1-2h esecuzione + 1h validazione)

**Prerequisiti**:
- ✅ GitHub CLI (`gh`) installato e autenticato
- ✅ Permessi write sul repository
- ✅ `jq` installato per JSON processing
- ✅ Backup/snapshot dello stato corrente issue

---

## 📊 Situazione Attuale

**Problema**: Due sistemi di priorità coesistono

| Sistema | Issue Count | Formato | Status |
|---------|-------------|---------|--------|
| **Title-based (nuovo)** | 21 | `[P0]`, `[P1]`, `[P2]`, `[P3]` | ✅ Standard |
| **Label-based (legacy)** | 132 | `priority: high/medium/low` | ⚠️ Da normalizzare |

**Impatto**:
- Impossibile avere visione unica delle priorità
- Roadmap basata su stime invece di dati reali
- Confusione nel team su quale sistema usare

---

## 🎯 Obiettivo Post-Normalizzazione

**Tutti i 165 issue** avranno priorità esplicita nel titolo:
- `[P0] Issue Title` - Critical (blockers produzione)
- `[P1] Issue Title` - High (MVP blockers)
- `[P2] Issue Title` - Medium (MVP features)
- `[P3] Issue Title` - Low (nice-to-have MVP)

**Benefit**:
- ✅ Sistema unico di prioritizzazione
- ✅ Sorting automatico per priorità in GitHub
- ✅ Visibilità immediata in liste/boards
- ✅ Roadmap basata su dati reali

---

## 🗺️ Mapping Label → Title Prefix

Lo script applicherà questa conversione automaticamente:

| Label Corrente | Nuovo Prefisso | Descrizione |
|----------------|----------------|-------------|
| `priority-critical`, `priority: critical`, `critical` | `[P0]` | Critical - blockers produzione/sicurezza |
| `priority-high`, `priority: high` | `[P1]` | High - MVP blockers, dipendenze core |
| `priority-medium`, `priority: medium` | `[P2]` | Medium - MVP features, refactoring |
| `priority-low`, `priority: low` | `[P3]` | Low - nice-to-have, polish |

**Esclusioni**:
- ❌ Issue già con `[P0]`-`[P3]` nel titolo (skip, già normalizzate)
- ❌ Issue con label `deferred` (mantengono stato deferred)

---

## 🚀 Esecuzione Step-by-Step

### STEP 1: Preparazione (15 minuti)

#### 1.1 Verifica Prerequisites

```bash
# Verifica GitHub CLI
gh --version
# Deve mostrare: gh version X.X.X (2023 o successivo)

# Verifica autenticazione
gh auth status
# Deve mostrare: Logged in to github.com as YOUR_USERNAME

# Verifica jq
jq --version
# Deve mostrare: jq-X.X
```

#### 1.2 Naviga alla directory tools

```bash
cd /home/user/meepleai-monorepo/tools
```

#### 1.3 Verifica script eseguibile

```bash
ls -la triage-phase1-normalize.sh
# Deve mostrare: -rwxr-xr-x (x indica eseguibile)

# Se non eseguibile:
chmod +x triage-phase1-normalize.sh
```

---

### STEP 2: Dry Run (30-60 minuti)

**IMPORTANTE**: Eseguire SEMPRE il dry run prima dell'esecuzione reale!

```bash
./triage-phase1-normalize.sh --dry-run
```

**Output Atteso**:
```
2025-11-23 14:30:00 [INFO] === TRIAGE PHASE 1: Issue Normalization ===
2025-11-23 14:30:00 [INFO] Repository: DegrassiAaron/meepleai-monorepo
2025-11-23 14:30:00 [INFO] Mode: DRY RUN (preview only)
2025-11-23 14:30:00 [INFO] ✓ Prerequisites verified
2025-11-23 14:30:00 [INFO] Processing issues with label: priority-high
2025-11-23 14:30:02 [INFO]   Issue #1668:
2025-11-23 14:30:02 [INFO]     Old: Update Component Imports to Subdirectory Paths
2025-11-23 14:30:02 [INFO]     New: [P1] Update Component Imports to Subdirectory Paths
2025-11-23 14:30:02 [INFO]     ⚠ Would rename (dry run)
...
2025-11-23 14:35:00 [INFO] === Normalization Complete ===
2025-11-23 14:35:00 [INFO] Total issues processed: 132
2025-11-23 14:35:00 [INFO] Issues renamed: 120
2025-11-23 14:35:00 [INFO] Issues skipped: 12
2025-11-23 14:35:00 [INFO] Errors: 0
```

---

### STEP 3: Validazione Report (30 minuti)

#### 3.1 Leggi il report generato

```bash
# Il report sarà salvato come: triage-normalize-report-YYYYMMDD-HHMMSS.md
cat triage-normalize-report-*.md
```

#### 3.2 Verifica Checklist

- [ ] **Conteggio corretto**: Total processed ≈ 132 issue
- [ ] **Mapping sensato**: P1 count > P2 count > P3 count (atteso)
- [ ] **Nessun P0 nuovo**: P0 count dovrebbe essere 0 (solo 3 esistenti)
- [ ] **Issue critiche mappate**: #575, #576 devono essere in lista
- [ ] **Nessun errore**: Error count = 0
- [ ] **Titoli corretti**: Review sample di 10-15 issue nel report

#### 3.3 Spot Check Manuale

Verifica manualmente 5-10 issue nel report:

```bash
# Esempio: verifica issue #1668
gh issue view 1668 --repo DegrassiAaron/meepleai-monorepo

# Verifica che label "priority: high" corrisponda a [P1] proposto
```

#### 3.4 Review con Team

**⚠️ CHECKPOINT CRITICO**:
- Condividi report con almeno 1 altro team member
- Verifica consenso su mapping P0/P1/P2/P3
- Approva formalmente prima di procedere a STEP 4

---

### STEP 4: Esecuzione Reale (30-60 minuti)

**⚠️ WARNING**: Questo step MODIFICHERÀ le issue su GitHub!

#### 4.1 Conferma Finale

```bash
echo "ATTENZIONE: Questo modificherà 132 issue su GitHub."
echo "Hai revisionato il report dry-run? (yes/no)"
read confirmation

if [ "$confirmation" != "yes" ]; then
    echo "Esecuzione annullata."
    exit 1
fi
```

#### 4.2 Esegui Normalizzazione

```bash
./triage-phase1-normalize.sh --execute
```

**Monitoraggio**:
- Lo script processerà ~2-3 issue/secondo (rate limiting)
- Tempo totale: 120 issue × 0.5s = ~60 secondi + overhead API
- Stima totale: **2-5 minuti**

#### 4.3 Verifica Output

```bash
2025-11-23 15:00:00 [INFO] === TRIAGE PHASE 1: Issue Normalization ===
2025-11-23 15:00:00 [INFO] Repository: DegrassiAaron/meepleai-monorepo
2025-11-23 15:00:00 [INFO] Mode: EXECUTE (will modify issues)
...
2025-11-23 15:05:00 [INFO] === Normalization Complete ===
2025-11-23 15:05:00 [INFO] Total issues processed: 132
2025-11-23 15:05:00 [INFO] Issues renamed: 120
2025-11-23 15:05:00 [INFO] Issues skipped: 12
2025-11-23 15:05:00 [INFO] Errors: 0
2025-11-23 15:05:00 [INFO] ✓ Normalization successful!
```

---

### STEP 5: Validazione Post-Esecuzione (30 minuti)

#### 5.1 Verifica Conteggi GitHub

```bash
# Verifica conteggio issue [P0]
gh issue list --repo DegrassiAaron/meepleai-monorepo --search "[P0]" --state open --limit 100 | wc -l
# Atteso: 3 (solo le P0 esistenti)

# Verifica conteggio issue [P1]
gh issue list --repo DegrassiAaron/meepleai-monorepo --search "[P1]" --state open --limit 100 | wc -l
# Atteso: 15-30 (4 esistenti + nuovi da normalizzazione)

# Verifica conteggio issue [P2]
gh issue list --repo DegrassiAaron/meepleai-monorepo --search "[P2]" --state open --limit 100 | wc -l
# Atteso: 40-60

# Verifica conteggio issue [P3]
gh issue list --repo DegrassiAaron/meepleai-monorepo --search "[P3]" --state open --limit 100 | wc -l
# Atteso: 20-40
```

#### 5.2 Spot Check Issue Specifiche

Verifica che issue critiche identificate siano state normalizzate:

```bash
# Verifica #575 (Auth 2FA admin - atteso P1)
gh issue view 575 --repo DegrassiAaron/meepleai-monorepo | grep "title:"

# Verifica #576 (Security testing - atteso P1)
gh issue view 576 --repo DegrassiAaron/meepleai-monorepo | grep "title:"

# Verifica #1006 (Backend API - atteso P1)
gh issue view 1006 --repo DegrassiAaron/meepleai-monorepo | grep "title:"

# Verifica #1668 (Frontend refactor - atteso P2)
gh issue view 1668 --repo DegrassiAaron/meepleai-monorepo | grep "title:"
```

#### 5.3 Verifica Board/Project

Se usi GitHub Projects:
1. Apri board: https://github.com/DegrassiAaron/meepleai-monorepo/projects
2. Verifica che issue siano ordinate correttamente per priorità
3. Conferma che P0/P1 siano in cima

#### 5.4 Genera Report Finale

```bash
# Salva snapshot post-normalizzazione
gh issue list --repo DegrassiAaron/meepleai-monorepo --state open --limit 1000 --json number,title,labels \
  > issue-snapshot-post-normalization-$(date +%Y%m%d).json

# Crea summary
cat > normalization-summary.md <<EOF
# Normalizzazione Completata

**Data**: $(date '+%Y-%m-%d %H:%M:%S')

## Risultati

- ✅ Total processed: [FROM SCRIPT OUTPUT]
- ✅ Successfully renamed: [FROM SCRIPT OUTPUT]
- ✅ Skipped (already normalized): [FROM SCRIPT OUTPUT]
- ❌ Errors: 0

## Breakdown Post-Normalizzazione

| Priority | Count | % of Total |
|----------|-------|------------|
| [P0] | 3 | 2% |
| [P1] | [COUNT] | [%] |
| [P2] | [COUNT] | [%] |
| [P3] | [COUNT] | [%] |
| No Priority | [COUNT] | [%] |
| TOTAL | 165 | 100% |

## Next Steps

1. ✅ FASE 1 completata: Normalizzazione
2. ⏭️ Avviare FASE 2: Triage critico (security, API, test)
3. 📊 Aggiornare ROADMAP.md con dati reali
EOF
```

---

## 🚨 Troubleshooting

### Errore: "gh: command not found"

**Soluzione**:
```bash
# MacOS
brew install gh

# Linux (Debian/Ubuntu)
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null
sudo apt update
sudo apt install gh

# Windows (PowerShell)
winget install --id GitHub.cli
```

### Errore: "Not authenticated"

**Soluzione**:
```bash
gh auth login
# Segui wizard interattivo, scegli:
# - GitHub.com
# - HTTPS
# - Login via browser
```

### Errore: "Permission denied"

**Soluzione**:
- Verifica permessi repository: Settings → Collaborators
- Necessita almeno: **Write** access
- Se admin, può serve: **Maintain** o **Admin**

### Errore: "Rate limit exceeded"

**Soluzione**:
- Lo script include rate limiting (0.5s/issue)
- Se errore persiste, aumenta sleep time nello script:
  ```bash
  # Line ~170: sleep 0.5
  sleep 1.0  # Cambia a 1 secondo
  ```

### Errore: "jq: command not found"

**Soluzione**:
```bash
# MacOS
brew install jq

# Linux
sudo apt-get install jq

# Windows (via Chocolatey)
choco install jq
```

---

## 📝 Rollback Plan

**Se qualcosa va storto durante esecuzione**:

### Opzione 1: Rollback Manuale (Singole Issue)

```bash
# Identifica issue problematiche dal log
grep "ERROR" triage-normalize-*.log

# Ripristina manualmente titolo originale
gh issue edit <ISSUE_NUMBER> --repo DegrassiAaron/meepleai-monorepo --title "Original Title"
```

### Opzione 2: Rollback Completo (da snapshot)

```bash
# 1. Recupera snapshot pre-normalizzazione
# (creato manualmente prima di esecuzione)
cat issue-snapshot-pre-normalization.json

# 2. Crea script rollback
cat > rollback.sh <<'EOF'
#!/bin/bash
jq -r '.[] | "\(.number)|\(.title)"' issue-snapshot-pre-normalization.json | \
while IFS='|' read -r num title; do
  echo "Rolling back #$num to: $title"
  gh issue edit "$num" --repo DegrassiAaron/meepleai-monorepo --title "$title"
  sleep 0.5
done
EOF

chmod +x rollback.sh
./rollback.sh
```

---

## ✅ Checklist Completamento FASE 1

### Pre-Esecuzione
- [ ] Prerequisites verificati (gh, jq, auth)
- [ ] Script scaricato e eseguibile
- [ ] Snapshot pre-normalizzazione creato
- [ ] Dry-run eseguito con successo
- [ ] Report dry-run revisionato
- [ ] Team ha approvato mapping
- [ ] Rollback plan pronto

### Esecuzione
- [ ] Conferma finale ottenuta
- [ ] Script eseguito in modalità --execute
- [ ] Output monitorato (nessun errore)
- [ ] Log salvato per reference

### Post-Esecuzione
- [ ] Conteggi GitHub verificati
- [ ] Spot check issue critiche OK
- [ ] Snapshot post-normalizzazione creato
- [ ] Report finale generato
- [ ] Team notificato completamento
- [ ] ROADMAP.md aggiornato con link report

### Cleanup
- [ ] Log archiviati in docs/07-project-management/triage-logs/
- [ ] Script commitato in tools/
- [ ] Guida aggiornata con lessons learned

---

## 🎓 Lessons Learned

**Da aggiornare post-esecuzione**:

### Cosa ha funzionato bene
- TBD (post-esecuzione)

### Cosa migliorare per prossima volta
- TBD (post-esecuzione)

### Raccomandazioni future
- Enforcing: Tutte nuove issue DEVONO avere [Px] (GitHub Action?)
- Weekly triage: Evitare backlog 132+ issue non prioritizzate
- Template issue: Pre-compilare campo Priority nel template

---

## 📞 Support

**Issues con lo script**:
- Controllare log file: `triage-normalize-*.log`
- Verificare report: `triage-normalize-report-*.md`
- Eseguire dry-run per debug

**Domande**:
- Team Lead: [NOME]
- Slack: #triage-phase1

---

**Versione Guida**: 1.0
**Ultima Revisione**: 2025-11-23
**Owner**: Engineering Team
**Status**: Ready for Execution
