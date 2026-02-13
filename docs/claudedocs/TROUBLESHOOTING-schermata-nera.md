# 🐛 Troubleshooting: Schermata Nera - 6 Errori Turbopack

**Issue:** Pagina `/admin/shared-games/new` mostra schermata nera con 6 errori
**Date:** 2026-02-12

---

## ✅ Verifiche Già Fatte

- [x] Server Next.js in esecuzione (porta 3000)
- [x] TypeScript compila senza errori
- [x] Build completa con successo (exit code 0)
- [x] Route GET /admin/shared-games/new ritorna 200 OK
- [x] HTML renderizzato server-side (59KB di output)
- [x] Fix applicato: admin-client-mock.ts disabilitato
- [x] Fix applicato: PdfUploadSection export aggiunto

**Conclusione:** Il server funziona, il problema è **client-side runtime** nel browser.

---

## 🔍 Passi per Identificare Errori

### **Step 1: Apri DevTools (RICHIESTO)**

Nel browser dove vedi la schermata nera:

1. **Premi F12** (o Ctrl+Shift+I)
2. **Vai alla tab "Console"**
3. **Cerca messaggi rossi** con icona ⛔ o ❌

### **Step 2: Copia Errori Completi**

Per ogni errore rosso, espandilo e copia:
- Messaggio completo
- File e riga dove si verifica
- Stack trace (se presente)

**Formato richiesto:**
```
Error: Module not found: Can't resolve '@/components/xxx'
  at GameForm.tsx:42:5
  at ...
```

### **Step 3: Controlla Tab "Network"**

1. Tab **Network** in DevTools
2. Filtra per **JS** o **Fetch/XHR**
3. Cerca richieste **Failed** (colore rosso)
4. Clicca sulla richiesta fallita
5. Copia URL e errore

---

## 🎯 Errori Comuni Turbopack

### **Import Non Risolti**
```javascript
// Errore
Module not found: Can't resolve '@/components/xxx'

// Causa
- Componente mancante
- Export barrel incompleto
- Path alias errato
```

### **Type Import Issues**
```javascript
// Errore
Cannot find module './schemas/xxx' or its corresponding type declarations

// Causa
- Schema file mancante
- Export type non dichiarato
```

### **Circular Dependencies**
```javascript
// Errore
Circular dependency detected

// Causa
- File A importa B, B importa A
- Barrel export che reimporta se stesso
```

### **Runtime Errors**
```javascript
// Errore
TypeError: Cannot read properties of undefined (reading 'xxx')

// Causa
- Hook chiamato fuori da componente
- Context provider mancante
- API response undefined
```

---

## 🛠️ Fix Temporaneo (Se Bloccato)

Se gli errori sono troppi e bloccanti, possiamo:

**Option A: Disable problematic imports**
```typescript
// GameForm.tsx - comment out problematic imports
// import { PdfUploadSection } from './PdfUploadSection';
```

**Option B: Use fallback component**
```typescript
// Create minimal GameForm without PDF upload
```

**Option C: Roll back to last working version**
```bash
git status
git stash  # Save current changes
git checkout HEAD~1 -- apps/web/src/components/admin/shared-games/
```

---

## 📋 Informazioni da Fornire

Per risolvere rapidamente, ho bisogno di:

1. **Screenshot della console** con errori visibili (F12 → Console)
2. **Copia/incolla di TUTTI i messaggi di errore rossi**
3. **Stack trace** completo del primo errore

Oppure:

4. **Screenshot della schermata nera** completa
5. **DevTools aperto** nella screenshot

---

**Aspetto gli errori per procedere con il fix!**
