# Consolidamento Documentazione - Guida Esecuzione

**Data creazione**: 2026-02-11
**Obiettivo**: Standardizzare naming e rimuovere duplicati nella documentazione

---

## 📋 Piano Completo

Vedi documento dettagliato: [`docs/consolidamento-documentazione.md`](../docs/consolidamento-documentazione.md)

---

## 🚀 Quick Start

### Prerequisiti
```powershell
# 1. Verifica di essere sul branch corretto
git branch --show-current  # Dovrebbe mostrare: main-dev

# 2. Commit tutto prima di iniziare (backup)
git status
git add .
git commit -m "docs: pre-consolidamento backup"
```

### Esecuzione Rapida (Tutte le Fasi)
```powershell
# DRY RUN (sicuro, nessuna modifica)
.\scripts\consolidate-docs-master.ps1 -DryRun

# Esecuzione reale
.\scripts\consolidate-docs-master.ps1

# Verifica risultati
.\scripts\consolidate-docs-verify.ps1
```

---

## 📝 Esecuzione Fase per Fase

### Fase 1: Bounded Contexts
**Azione**: Rimuove duplicati (brevi + COMPLETE)

```powershell
# DRY RUN
.\scripts\consolidate-docs-phase1.ps1 -DryRun

# Esecuzione
.\scripts\consolidate-docs-phase1.ps1

# Output atteso:
# ✅ Files deleted: 10
# ✅ Files renamed: 11
```

**Cosa fa**:
- Elimina 10 file brevi: `administration.md`, `authentication.md`, etc.
- Rinomina 11 file: `*-COMPLETE.md` → `*.md`
- Crea backup automatico in `docs/09-bounded-contexts-backup-[timestamp]`

### Fase 2: Naming Globale
**Azione**: Converte tutti i file in kebab-case

```powershell
# DRY RUN
.\scripts\consolidate-docs-phase2.ps1 -DryRun

# Esecuzione
.\scripts\consolidate-docs-phase2.ps1

# Output atteso:
# ✅ Files renamed: ~35
```

**Cosa fa**:
- Converte `UPPERCASE.md` → `lowercase.md`
- Converte `snake_case.md` → `kebab-case.md`
- Converte `PascalCase.md` → `kebab-case.md`
- Mantiene `README.md` e `CLAUDE.md` invariati
- Crea backup in `docs-backup-naming-[timestamp]`

### Verifica Finale
```powershell
.\scripts\consolidate-docs-verify.ps1
```

**Output atteso**:
```
✅ Tutti i file seguono la convenzione kebab-case
✅ Nessun file *-COMPLETE.md trovato
⚠️  Trovati N potenziali link rotti (da aggiornare)
```

---

## 🛠️ Opzioni Script

### Parametri Comuni

| Parametro | Descrizione | Default |
|-----------|-------------|---------|
| `-DryRun` | Simula operazioni senza modificare | `false` |
| `-NoBackup` | Skip creazione backup | `false` |

### Esempi

```powershell
# Solo simulazione (sicuro)
.\scripts\consolidate-docs-phase1.ps1 -DryRun

# Esecuzione senza backup (sconsigliato)
.\scripts\consolidate-docs-phase1.ps1 -NoBackup

# Master script con simulazione
.\scripts\consolidate-docs-master.ps1 -DryRun
```

---

## ✅ Checklist Post-Esecuzione

### Immediato
- [ ] Verifica script eseguiti senza errori
- [ ] Esegui `consolidate-docs-verify.ps1`
- [ ] Controlla backup creati

### Manuale
- [ ] Aggiorna link rotti (se trovati dalla verifica)
- [ ] Aggiorna `docs/INDEX.md` con nuovi nomi
- [ ] Verifica CLAUDE.md per riferimenti a file rinominati

### Git Workflow
```powershell
# 1. Verifica modifiche
git status
git diff --name-status

# 2. Commit consolidamento
git add docs/
git add scripts/
git commit -m "docs: consolidate documentation structure

- Remove bounded contexts duplicates (11 files)
- Standardize naming to kebab-case (~35 files)
- Update internal references
- Add consolidation scripts

See: docs/consolidamento-documentazione.md"

# 3. (Opzionale) Crea branch per revisione
git checkout -b docs/consolidation-review
git push -u origin docs/consolidation-review
# Poi crea PR per revisione team
```

---

## 🔧 Troubleshooting

### Problema: Script non eseguibile
```powershell
# Soluzione: Imposta ExecutionPolicy
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Problema: File già esistente (conflitto)
```
❌ CONFLICT: old-name.md → new-name.md (file already exists)
```

**Soluzione**: Rinomina manualmente uno dei due file prima di rieseguire lo script.

### Problema: Link rotti dopo consolidamento
```
⚠️  Trovati N potenziali link rotti
```

**Soluzione**:
1. Esamina l'output di `consolidate-docs-verify.ps1`
2. Aggiorna manualmente i link nei file segnalati
3. Oppure usa find-replace globale:
   ```powershell
   # Esempio: sostituire tutti i riferimenti a file rinominato
   Get-ChildItem -Path "docs" -Filter "*.md" -Recurse | ForEach-Object {
       (Get-Content $_.FullName) -replace 'old-name\.md', 'new-name.md' |
           Set-Content $_.FullName
   }
   ```

### Problema: Backup occupa troppo spazio
```powershell
# Rimuovi backup dopo verifica
Remove-Item "docs\*-backup-*" -Recurse -Force
Remove-Item "docs-backup-*" -Recurse -Force
```

---

## 📊 Metriche Attese

### Pre-Consolidamento
- File markdown totali: ~440
- Bounded contexts duplicati: 11 paia (22 file)
- Naming non conforme: ~35 file
- Struttura directory: frammentata (frontend, design-system, etc.)

### Post-Consolidamento
- File markdown totali: ~405 (-35)
- Bounded contexts duplicati: 0
- Naming non conforme: 0 (100% kebab-case)
- Struttura directory: più chiara e unificata

---

## 🎯 Prossimi Step (Manuali)

Dopo aver eseguito le fasi automatiche:

### 1. Unificare Frontend Docs (Manuale)
```powershell
# Spostare frontend/ → 07-frontend/legacy/
Move-Item "docs\frontend" "docs\07-frontend\legacy"

# Spostare design-system/ → 07-frontend/design-system/
Move-Item "docs\design-system" "docs\07-frontend\design-system"
```

### 2. Rimuovere File Obsoleti (Revisione)
```powershell
# Candidates per rimozione:
# - docs/01-architecture/overview/archive/consolidation-strategy.md
# - docs/02-development/guida-visualcode.md (unico file in italiano)
# - docs/issue-sequences.html

# Verifica contenuto prima di rimuovere
Get-Content "docs\02-development\guida-visualcode.md"
```

### 3. Aggiornare Index
```powershell
# Aggiorna manualmente:
# - docs/INDEX.md
# - docs/README.md
# - CLAUDE.md (se necessario)
```

---

## 📚 Riferimenti

- [Piano completo](../docs/consolidamento-documentazione.md)
- [Convenzione kebab-case](https://en.wikipedia.org/wiki/Letter_case#Kebab_case)
- [Markdown best practices](https://www.markdownguide.org/basic-syntax/)

---

**Maintainer**: Architecture Team
**Last Updated**: 2026-02-11
