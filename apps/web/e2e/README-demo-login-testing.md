# Demo User Login Testing - Technical Summary

## Obiettivo
Testare il flusso UI completo per il login degli utenti demo:
- `user@meepleai.dev` / `Demo123!`
- `editor@meepleai.dev` / `Demo123!`
- `admin@meepleai.dev` / `Demo123!`

## Problema Riscontrato

Durante i test E2E in ambiente Playwright headless (Chromium), si verifica un problema persistente con un elemento `<nextjs-portal>` che intercetta tutti gli eventi di pointer, impedendo i click sui pulsanti.

### Sintomi
```
- <nextjs-portal></nextjs-portal> intercepts pointer events
- element is visible, enabled and stable
- retrying click action (timeout dopo 10s)
```

### Tentativi Effettuati

1. ✅ **`reducedMotion: 'reduce'`** - Disabilita animazioni framer-motion
   - Risultato: Parziale - elimina problemi di instabilità ma il portal persiste

2. ❌ **`force: true` sui click** - Bypassa il controllo di Playwright
   - Risultato: Il click avviene ma gli handler onClick NON vengono invocati
   - Il form non viene submitted, nessun redirect

3. ❌ **`waitForFunction()` per aspettare che il portal sparisca**
   - Risultato: Crash del browser (SIGSEGV) con flag `--single-process`

4. ❌ **Tentare pulsanti diversi** (nav vs hero)
   - Risultato: Stesso problema su tutti i pulsanti

5. ❌ **Timeout e attese extra**
   - Risultato: Il portal non sparisce mai naturalmente

## Soluzione Corrente nel Progetto

**Tutti gli altri test E2E nel progetto usano MOCK authentication** tramite `fixtures/auth.ts`.

### Esempio: `comments-enhanced.spec.ts`
```typescript
import { test, expect } from './fixtures/auth';

test.describe('EDIT-05: Enhanced Comments System', () => {
  test('can create top-level comment', async ({ editorPage: page }) => {
    // Già autenticato come Editor
    await page.goto(VERSIONS_URL);
    // ...test code
  });
});
```

### Vantaggi del Mock Auth
- ✅ Affidabile e veloce
- ✅ Nessun problema con portal/animazioni
- ✅ Testa la funzionalità post-login (che è l'obiettivo principale)
- ✅ Usato da 10+ test nel progetto

### Svantaggi
- ❌ Non testa il flusso UI reale di login
- ❌ Non verifica l'integrazione con il backend /api/v1/auth/login

## Opzioni Disponibili

### Opzione 1: Mock Auth (Raccomandato per CI/CD)
Usare `fixtures/auth.ts` per testare le funzionalità post-login.

```typescript
import { test, expect } from './fixtures/auth';

test('user can access chat', async ({ userPage }) => {
  await expect(userPage).toHaveURL('/chat');
});

test('editor can upload PDFs', async ({ editorPage }) => {
  await editorPage.goto('/upload');
  await expect(editorPage.getByRole('heading', { name: /Upload/i })).toBeVisible();
});
```

### Opzione 2: Test Backend API Direttamente
Testare gli endpoint `/api/v1/auth/login` con integration test (già esistenti in `apps/api/tests/`).

### Opzione 3: Test Manuale / Semi-Automatico
- Test manuali periodici del flusso di login
- Screenshot testing con Playwright UI mode (`pnpm test:e2e:ui`)
- Test in browser non-headless locale

### Opzione 4: Investigare Causa Root del Portal
Richiederebbe:
- Debug approfondito del rendering Next.js
- Possibile modifica del codice dell'app per l'ambiente test
- Potrebbe non essere risolvibile senza modifiche strutturali

## Raccomandazione

Per testare le funzionalità degli utenti demo:

1. **Usare mock auth per test automatici** (99% dei casi)
2. **Test API backend** per verificare logic di autenticazione
3. **Test manuali periodici** per verificare UX reale del login
4. **Documentare gli utenti demo** nel README per facilitare test manuali

## File Coinvolti

- `e2e/fixtures/auth.ts` - Mock authentication helpers (FUNZIONANTE)
- `e2e/chat-edit-delete.spec.ts` - Tentativo di real login (FALLITO)
- `e2e/demo-user-login.spec.ts` - Test dedicato login demo (FALLITO per portal issue)
- `e2e/comments-enhanced.spec.ts` - Esempio funzionante con mock auth

## Test Funzionanti con Mock Auth

```bash
# Test che già verificano le funzionalità dei demo users
pnpm test:e2e comments-enhanced.spec.ts
pnpm test:e2e admin-analytics.spec.ts
pnpm test:e2e admin-configuration.spec.ts
```

Questi test verificano che Editor e Admin possano:
- ✅ Accedere alle loro funzionalità
- ✅ Creare/modificare contenuti
- ✅ Vedere UI corrette in base al ruolo

---

**Data**: 2025-11-05
**Autore**: Claude (AI Assistant)
**Status**: nextjs-portal issue non risolta in headless Chromium
