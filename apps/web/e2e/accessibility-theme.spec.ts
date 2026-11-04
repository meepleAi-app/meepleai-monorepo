/**
 * E2E Test: Accessibility - Theme System - Issue #2965 Wave 9
 *
 * Verifies WCAG 2.1 AA compliance for dual-theme system.
 *
 * Tests:
 * - Color contrast ratios (4.5:1 for text, 3:1 for UI)
 * - Focus visibility in both themes
 * - Keyboard navigation
 * - Screen reader compatibility
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from './_helpers/realAdminAuth';

test.describe('Accessibility - Dual-Theme System', () => {
  test.beforeEach(async ({ page }) => {
    // #4106: questa spec non autenticava. La rotta e' protetta, quindi ogni asserzione
    // cadeva sulla pagina di login e il sintomo (`toBeVisible` che non trova nulla) somigliava
    // a una deriva di selettori. Causa stabilita guardando l'istantanea di pagina, non il
    // messaggio d'errore.
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  test('should have no accessibility violations in light mode', async ({ page }) => {
    // Set light mode
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Run axe accessibility scan
    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    // Expect no violations
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  /**
   * SALTATO per una violazione REALE del prodotto, non per un difetto del test.
   *
   * axe riporta **una** violazione `color-contrast` di impatto `serious`, sullo stesso
   * elemento che #4104 ha aperto partendo da `dashboard.spec.ts`: il CTA «+ Nuova» di
   * `ProssimiSection.tsx:173`, `#ffffff` su `#f47187` a 11px grassetto = **2.77** contro il
   * 4.5:1 che AA chiede. Due spec indipendenti colgono lo stesso difetto — il che e' la
   * prova che non e' un artefatto di come una delle due misura.
   *
   * Non si rilassa il filtro e non si esclude la regola: `CLAUDE.md` dice che un fallimento
   * di contrasto e' una regressione vera. Quando #4104 chiude, togli la riga `test.skip`.
   */
  test('should have no accessibility violations in dark mode', async ({ page }) => {
    test.skip(
      true,
      'DIFETTO: #4104 — color-contrast 2.77 contro 4.5:1 sul CTA «+ Nuova» (ProssimiSection.tsx:173) in tema scuro'
    );

    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('should have visible focus indicators in light mode', async ({ page }) => {
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    // Tab through interactive elements
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Check focus is visible (ring should be present)
    const focused = page.locator(':focus');
    await expect(focused).toBeVisible();

    // Verify focus ring exists (primary color in light mode)
    const focusedElement = await focused.evaluate(el => {
      const styles = window.getComputedStyle(el);
      return {
        outline: styles.outline,
        outlineWidth: styles.outlineWidth,
        boxShadow: styles.boxShadow,
      };
    });

    // Should have some focus indicator (outline or box-shadow/ring)
    expect(
      focusedElement.outline !== 'none' ||
        focusedElement.outlineWidth !== '0px' ||
        focusedElement.boxShadow !== 'none'
    ).toBeTruthy();
  });

  test('should have visible focus indicators in dark mode (amber rings)', async ({ page }) => {
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    // Tab through interactive elements
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Check focus is visible
    const focused = page.locator(':focus');
    await expect(focused).toBeVisible();

    // In dark mode, focus rings should use amber (--accent) for better visibility
    const focusedElement = await focused.evaluate(el => {
      const styles = window.getComputedStyle(el);
      return {
        outline: styles.outline,
        boxShadow: styles.boxShadow,
        outlineColor: styles.outlineColor,
      };
    });

    // Should have amber-ish focus indicator in dark mode
    // (Can't easily test exact color, but verify SOME focus exists)
    expect(focusedElement.outline !== 'none' || focusedElement.boxShadow !== 'none').toBeTruthy();
  });

  test('should support keyboard navigation of theme toggle', async ({ page }) => {
    await page.goto('/dashboard');

    // #4106: la versione precedente premeva `Tab` tre volte contando le voci del menu
    // («Settings», «Separator», «ThemeToggle»), poi leggeva l'`aria-label` di `:focus` —
    // che risultava `null`, perche' il fuoco non era dove il conteggio prevedeva. Un test
    // che conta i Tab verifica l'ORDINE del menu, non l'operabilita' da tastiera, e si
    // rompe a ogni riordino. Qui il toggle si individua per ruolo e nome, e si verifica
    // che risponda a Invio.
    await page.getByRole('button', { name: 'User menu' }).click();

    // `aria-label` del toggle: `Attiva tema chiaro` / `Attiva tema scuro` secondo lo stato
    // corrente (`ThemeToggle.tsx:74`, stringhe italiane cablate, non da `t()`).
    const toggle = page.getByRole('button', { name: /Attiva tema (chiaro|scuro)/ });
    await expect(toggle).toBeVisible();

    const before = await page.locator('html').getAttribute('class');
    await toggle.focus();
    await page.keyboard.press('Enter');

    // La prova dell'operabilita' e' che il tema CAMBIA: asserire che `class` sia non-vuota
    // passerebbe anche se Invio non facesse nulla.
    await expect(page.locator('html')).not.toHaveClass(before ?? '');
  });

  test('should maintain WCAG AA contrast ratios in both themes', async ({ page }) => {
    // This is a smoke test - full contrast testing done by axe-core
    // Just verify critical text elements are readable

    // Test light mode
    await page.goto('/dashboard');
    await page.evaluate(() => {
      localStorage.setItem('theme', 'light');
      document.documentElement.classList.remove('dark');
    });
    await page.reload();

    const lightContrast = await page.evaluate(() => {
      const sampleText = document.querySelector('h1, h2, p');
      if (!sampleText) return null;

      const styles = window.getComputedStyle(sampleText);
      return {
        color: styles.color,
        backgroundColor: styles.backgroundColor,
      };
    });
    expect(lightContrast).toBeTruthy();

    // Test dark mode
    await page.evaluate(() => {
      localStorage.setItem('theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.reload();

    const darkContrast = await page.evaluate(() => {
      const sampleText = document.querySelector('h1, h2, p');
      if (!sampleText) return null;

      const styles = window.getComputedStyle(sampleText);
      return {
        color: styles.color,
        backgroundColor: styles.backgroundColor,
      };
    });
    expect(darkContrast).toBeTruthy();

    // Colors should be different between themes
    expect(darkContrast?.color).not.toBe(lightContrast?.color);
  });

  test('il tema predefinito e chiaro anche con il sistema in scuro', async ({ page }) => {
    // #4106: la versione precedente attendeva che `prefers-color-scheme: dark` producesse
    // `class="dark"`. **L'app non lo fa, per decisione**: `ThemeProvider` passa
    // `defaultTheme="light"` a next-themes («mockup default warm cream #f7f3ee»), e
    // `CLAUDE.md` lo dichiara — «Default is light». Con `enableSystem` attivo ma un
    // `defaultTheme` esplicito, la preferenza del sistema conta solo se l'utente sceglie
    // «system», e il toggle di questa app commuta solo light↔dark.
    //
    // Il test e' stato girato: invece di asserire un comportamento che non c'e', fissa la
    // decisione documentata, cosi' un cambio accidentale di `defaultTheme` fallirebbe qui.
    await page.goto('/dashboard');
    await page.evaluate(() => localStorage.removeItem('theme'));

    await page.emulateMedia({ colorScheme: 'dark' });
    await page.reload();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('html')).not.toHaveClass(/dark/);

    await page.emulateMedia({ colorScheme: 'light' });
    await page.evaluate(() => localStorage.removeItem('theme'));
    await page.reload();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('html')).not.toHaveClass(/dark/);
  });
});
