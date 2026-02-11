# 📚 Consolidamento Documentazione - Summary Esecutivo

**Data**: 2026-02-11
**Status**: Ready for Execution

---

## 🎯 Obiettivi

1. **Eliminare duplicati** (22 file bounded contexts → 11 file)
2. **Standardizzare naming** (UPPERCASE/snake_case → kebab-case)
3. **Organizzare struttura** (unificare directory frammentate)
4. **Migliorare manutenibilità** (convenzioni chiare e uniformi)

---

## 📊 Cambiamenti Previsti

### Bounded Contexts (docs/09-bounded-contexts/)
```diff
- 11 file brevi (authentication.md, administration.md, etc.)
- 11 file -COMPLETE.md
+ 11 file unificati (versioni complete senza suffisso)

Esempio:
- authentication.md (323 righe) - versione breve eliminata
- authentication-COMPLETE.md (1422 righe) - rinominata
+ authentication.md (1422 righe - versione completa)
```

### Naming Standardization
```diff
Files da rinominare: ~35

Esempi:
- AZUL_TEST_INSTRUCTIONS.md → azul-test-instructions.md
- BGG_API_TOKEN_SETUP.md → bgg-api-token-setup.md
- EPIC-GC-001-SUMMARY.md → epic-gc-001-summary.md
- BackgroundRulebookAnalysis-ManualTesting.md → background-rulebook-analysis-manual-testing.md

Eccezioni (invariati):
✓ README.md
✓ CLAUDE.md
```

### Struttura Directory
```
Prima:
docs/
├── frontend/          (sparse)
├── design-system/     (sparse)
└── 07-frontend/       (parziale)

Dopo (manuale):
docs/
└── 07-frontend/
    ├── design-system/
    ├── components/
    ├── epics/
    └── migrations/
```

---

## 🚀 Quick Start

### 1. Backup & Preparazione (5 min)
```powershell
# Verifica branch
git branch --show-current  # Deve essere: main-dev

# Commit tutto
git status
git add .
git commit -m "docs: pre-consolidamento backup"
```

### 2. Dry Run (2 min)
```powershell
# Simulazione sicura (nessuna modifica)
cd D:\Repositories\meepleai-monorepo-dev
.\scripts\consolidate-docs-master.ps1 -DryRun
```

**Output atteso**:
```
✅ Files deleted: 10
✅ Files renamed: 11
✅ Files renamed: ~35
⚠️  DRY RUN completato - Nessuna modifica applicata
```

### 3. Esecuzione Reale (5 min)
```powershell
# Esecuzione effettiva
.\scripts\consolidate-docs-master.ps1
```

### 4. Verifica (2 min)
```powershell
# Controllo integrità
.\scripts\consolidate-docs-verify.ps1
```

**Output atteso**:
```
✅ Tutti i file seguono la convenzione kebab-case
✅ Nessun file *-COMPLETE.md trovato
⚠️  Trovati N potenziali link rotti (da aggiornare)
```

### 5. Aggiornamenti Manuali (1-2 ore)
- Aggiorna link rotti segnalati dalla verifica
- Unifica directory frontend (opzionale)
- Aggiorna `docs/index.md` e `docs/README.md`

### 6. Commit & PR
```powershell
git status
git diff --name-status

git add docs/ scripts/
git commit -m "docs: consolidate documentation structure

- Remove bounded contexts duplicates (11 files)
- Standardize naming to kebab-case (~35 files)
- Add consolidation scripts and documentation

See: docs/consolidamento-documentazione.md"

# Opzionale: crea PR per review
git checkout -b docs/consolidation-review
git push -u origin docs/consolidation-review
```

---

## 📋 Metriche

| Metrica | Prima | Dopo | Delta |
|---------|-------|------|-------|
| File totali | ~440 | ~405 | -35 |
| Duplicati BC | 22 | 11 | -11 |
| Naming non conforme | ~35 | 0 | -35 |
| Convenzione | Mixed | kebab-case | 100% |

**Spazio risparmiato**: ~500KB (rimozione duplicati)
**Tempo esecuzione**: 10-15 minuti (automatico)

---

## 📂 File Creati

### Documentazione
- `docs/consolidamento-documentazione.md` - Piano dettagliato completo
- `docs/CONSOLIDAMENTO-summary.md` - Questo documento
- `scripts/CONSOLIDAMENTO-README.md` - Guida esecuzione script

### Script PowerShell
- `scripts/consolidate-docs-master.ps1` - Master script (tutte le fasi)
- `scripts/consolidate-docs-phase1.ps1` - Fase 1: Bounded Contexts
- `scripts/consolidate-docs-phase2.ps1` - Fase 2: Naming standardization
- `scripts/consolidate-docs-verify.ps1` - Verifica integrità finale

---

## ✅ Checklist Esecuzione

### Pre-esecuzione
- [ ] Commit tutte le modifiche correnti
- [ ] Verifica di essere su `main-dev`
- [ ] Leggi piano completo: `docs/consolidamento-documentazione.md`

### Esecuzione Automatica
- [ ] Dry run: `.\scripts\consolidate-docs-master.ps1 -DryRun`
- [ ] Esecuzione: `.\scripts\consolidate-docs-master.ps1`
- [ ] Verifica: `.\scripts\consolidate-docs-verify.ps1`

### Post-esecuzione Manuale
- [ ] Aggiorna link rotti (da output verifica)
- [ ] (Opzionale) Unifica frontend docs
- [ ] Aggiorna `docs/index.md`
- [ ] Aggiorna `docs/README.md`
- [ ] Commit e PR

---

## 🔧 Supporto

### Script non eseguibile?
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Link rotti dopo consolidamento?
```powershell
# Trova tutti i riferimenti a un file specifico
Get-ChildItem -Path "docs" -Filter "*.md" -Recurse |
    Select-String -Pattern "old-filename.md"

# Sostituisci globalmente
Get-ChildItem -Path "docs" -Filter "*.md" -Recurse | ForEach-Object {
    (Get-Content $_.FullName) -replace 'old-name\.md', 'new-name.md' |
        Set-Content $_.FullName
}
```

### Rollback necessario?
```powershell
# I backup sono in:
# - docs/09-bounded-contexts-backup-[timestamp]
# - docs-backup-naming-[timestamp]

# Oppure usa git:
git status
git restore docs/  # Annulla tutte le modifiche non committate
```

---

## 📚 Risorse

- [Piano completo](consolidamento-documentazione.md) - Dettagli tecnici completi
- [Guida script](../scripts/CONSOLIDAMENTO-README.md) - Riferimento script PowerShell
- [Kebab-case convention](https://en.wikipedia.org/wiki/Letter_case#Kebab_case)

---

## 🎯 Next Steps (Dopo Consolidamento)

### Immediate (Incluse negli script)
- ✅ Rimozione duplicati bounded contexts
- ✅ Standardizzazione naming globale
- ✅ Verifica integrità automatica

### Manuali (1-2 ore)
- 🔄 Aggiornare link rotti
- 🔄 Unificare directory frontend
- 🔄 Rimuovere file obsoleti
- 🔄 Aggiornare index e README

### Future (Separate tasks)
- 🔜 Implementare CI/CD check per naming convention
- 🔜 Automatizzare verifica link rotti in pipeline
- 🔜 Creare template standard per nuovi documenti

---

**Ready to Execute**: ✅
**Estimated Time**: 15 minuti (auto) + 1-2 ore (manuale)
**Risk Level**: Basso (backup automatico + DRY RUN)
**Approval Required**: Sì
