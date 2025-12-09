# Playwright UI Mode - Visual Testing Guide

## 🎯 Obiettivo
Usare Playwright UI mode per fare visual testing e debuggare problemi come il `<nextjs-portal>` che intercetta i click.

## 🚀 Quick Start

```bash
cd apps/web

# 1. Assicurati che il backend sia running
# Terminal 1
cd apps/api/src/Api && dotnet run

# Terminal 2 - Run UI mode
cd apps/web
pnpm test:e2e:ui demo-user-login.spec.ts
```

## 📊 Features di UI Mode

### 1. **Watch Mode** - Esecuzione Visuale
- ✅ Vedi il browser mentre esegue il test
- ✅ Pausa/Play/Step through dei test
- ✅ Timeline delle azioni
- ✅ Screenshot automatici ad ogni step

### 2. **Inspector** - Debug Interattivo
- 🔍 Ispeziona DOM in tempo reale
- 🎯 Highlight elementi durante hover
- 📝 Vedi selettori Playwright
- ⏯️ Pausa su errori

### 3. **Trace Viewer** - Post-Mortem Analysis
- 📹 Replay del test frame-by-frame
- 🌐 Network requests timeline
- 📸 Screenshot automatici
- 🖱️ Console logs

## 🐛 Debuggare il Portal Issue

### Scenario 1: Test che Fallisce (demo-user-login.spec.ts)

```bash
pnpm test:e2e:ui demo-user-login.spec.ts
```

**Cosa vedere nell'UI:**

1. **Test Explorer** (lato sinistro)
   - Lista dei test
   - Stato: ✅ passing / ❌ failing
   - Click su test per eseguirlo

2. **Browser Window** (centro)
   - Vedi il browser in azione
   - Il test naviga a `/`
   - Click sul pulsante "Get Started"
   - ❌ **QUI SI FERMA** → timeout dopo 10s
   - Errore: "nextjs-portal intercepts pointer events"

3. **Actions Timeline** (lato destro)
   ```
   ✅ page.goto('/')
   ✅ page.waitForLoadState('domcontentloaded')
   ❌ page.getByTestId('hero-get-started').click()
      └─ Error: Target closed
         └─ <nextjs-portal></nextjs-portal> intercepts pointer events
   ```

4. **DOM Snapshot** (click su azione fallita)
   ```html
   <body>
     <div id="__next">
       <button data-testid="hero-get-started">Get Started</button>
       <!-- ❌ QUESTO È IL PROBLEMA -->
       <nextjs-portal></nextjs-portal>  <!-- z-index molto alto -->
     </div>
   </body>
   ```

### Scenario 2: Test che Funziona (comments-enhanced.spec.ts)

```bash
pnpm test:e2e:ui comments-enhanced.spec.ts
```

**Differenza chiave:**

1. **Mock Auth Setup**
   ```
   ✅ Mock route setup: /api/v1/auth/me
   ✅ page.goto('/versions?gameId=demo-chess')
   ✅ page.locator('textarea').fill('Test comment')
   ✅ page.click('button:has-text("Aggiungi")')
   ✅ Test completo in ~2 secondi
   ```

2. **No Portal Issue**
   - Il mock auth bypassa la home page
   - Vai direttamente alla pagina autenticata
   - Nessun modal di login → nessun portal

## 🔧 Features Avanzate

### 1. **Slow Motion** - Rallenta Esecuzione
```bash
# Aggiungi al playwright.config.ts
use: {
  launchOptions: {
    slowMo: 1000, // 1 secondo tra ogni azione
  },
}
```

### 2. **Debug Mode** - Pausa Automatica
```typescript
// Aggiungi nel test
test('debug portal', async ({ page }) => {
  await page.goto('/');

  // Pausa qui - ispeziona DOM manualmente
  await page.pause();

  await page.getByTestId('hero-get-started').click();
});
```

Poi run con:
```bash
pnpm test:e2e:ui demo-user-login.spec.ts
```

### 3. **Screenshots on Failure**
Già configurato in `playwright.config.ts`:
```typescript
use: {
  screenshot: 'only-on-failure',
  trace: 'retain-on-failure',
}
```

Screenshot salvati in: `test-results/`

### 4. **Headed Mode** - Browser Visibile
```bash
# Run in browser non-headless (più affidabile per portal issue)
pnpm playwright test demo-user-login.spec.ts --headed --project=chromium

# O con debug
pnpm playwright test demo-user-login.spec.ts --debug
```

## 🎬 Workflow Consigliato per Visual Testing

### Step 1: Verifica Test Funzionante (Mock Auth)
```bash
pnpm test:e2e:ui comments-enhanced.spec.ts
```
- ✅ Vedi che il mock auth funziona
- ✅ Nessun problema di portal
- ✅ Test veloce e stabile

### Step 2: Confronta con Test Fallito (Real Login)
```bash
pnpm test:e2e:ui demo-user-login.spec.ts
```
- ❌ Vedi il portal apparire
- ❌ Click bloccato
- 🔍 Ispeziona DOM per vedere `<nextjs-portal>`

### Step 3: Test Manuale con Headed Mode
```bash
pnpm playwright test demo-user-login.spec.ts --headed --project=chromium
```
- 👀 Browser visibile (non headless)
- 🤔 Potrebbe funzionare meglio (il portal è un problema di headless)
- 📝 Se funziona in headed ma non headless → conferma che è bug di ambiente

### Step 4: Analisi Trace Viewer
```bash
# Genera trace
pnpm test:e2e demo-user-login.spec.ts

# Apri report
pnpm test:e2e:report
```

Nel trace viewer:
- Timeline delle azioni
- Network requests
- Console logs
- DOM snapshots ad ogni step

## 📸 Visual Regression Testing

Per verificare che il portal non appaia in produzione:

```typescript
// Nuovo file: e2e/visual-regression.spec.ts
import { test, expect } from '@playwright/test';

test('home page should not have nextjs-portal blocking clicks', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  // Screenshot baseline
  await expect(page).toHaveScreenshot('home-page.png');

  // Verifica che il portal non blocchi
  const portal = page.locator('nextjs-portal');
  const portalExists = await portal.count() > 0;

  if (portalExists) {
    // Verifica che non abbia z-index alto
    const zIndex = await portal.evaluate(el =>
      window.getComputedStyle(el).zIndex
    );
    expect(parseInt(zIndex)).toBeLessThan(100);
  }
});
```

## 🎯 Best Practices

### ✅ DO:
- Usa UI mode per debuggare test falliti
- Usa `--headed` per problemi di rendering
- Usa mock auth per test di funzionalità
- Genera trace per analisi post-mortem
- Usa screenshot comparison per visual regression

### ❌ DON'T:
- Non affidarti solo a UI mode in CI/CD (troppo lento)
- Non testare login UI in headless se hai portal issues
- Non usare `force: true` come fix permanente (nasconde il problema)
- Non ignorare differenze headed vs headless

## 🔬 Investigare Portal Issue - Checklist

Quando apri UI mode per `demo-user-login.spec.ts`:

- [ ] Vedi il browser aprire e navigare a `/`
- [ ] Vedi la home page caricarsi completamente
- [ ] Vedi il pulsante "Get Started" visibile
- [ ] Vedi il tentativo di click
- [ ] ❌ Vedi l'errore "nextjs-portal intercepts pointer events"
- [ ] Ispeziona DOM: trova `<nextjs-portal>` element
- [ ] Controlla CSS computed: z-index, position, pointer-events
- [ ] Verifica se esiste in DOM anche quando modal non è aperto
- [ ] Confronta con versione headed: `--headed --project=chromium`

## 📚 Comandi Utili

```bash
# UI mode base
pnpm test:e2e:ui

# UI mode per test specifico
pnpm test:e2e:ui demo-user-login.spec.ts

# Headed mode (browser visibile)
pnpm playwright test --headed demo-user-login.spec.ts

# Debug mode (con pause interattiva)
pnpm playwright test --debug demo-user-login.spec.ts

# Genera trace e apri viewer
pnpm test:e2e demo-user-login.spec.ts
pnpm test:e2e:report

# Screenshot di tutti i test
pnpm playwright test --screenshot=on

# Update screenshot baselines
pnpm playwright test --update-snapshots
```

## 🎓 Tutorial Interattivo

1. **Apri UI mode:**
   ```bash
   pnpm test:e2e:ui
   ```

2. **Seleziona test:** Click su `demo-user-login.spec.ts` → test "user@meepleai.dev can log in"

3. **Run test:** Click ▶️ (Play button)

4. **Osserva:**
   - Timeline si riempie di azioni
   - Browser esegue azioni
   - Stop su errore

5. **Ispeziona errore:**
   - Click sull'azione fallita
   - Vedi DOM snapshot
   - Vedi error message
   - Vedi screenshot

6. **Confronta con successo:**
   - Apri `comments-enhanced.spec.ts`
   - Run test
   - Vedi flusso completo senza errori

## 🌐 Link Utili

- [Playwright UI Mode Docs](https://playwright.dev/docs/test-ui-mode)
- [Playwright Inspector](https://playwright.dev/docs/debug#playwright-inspector)
- [Trace Viewer](https://playwright.dev/docs/trace-viewer)
- [Visual Comparisons](https://playwright.dev/docs/test-snapshots)

---

**TL;DR**: Usa `pnpm test:e2e:ui demo-user-login.spec.ts` per **vedere visualmente** il problema del portal. Poi usa `pnpm test:e2e:ui comments-enhanced.spec.ts` per vedere come il **mock auth bypassa** il problema. 🎯
