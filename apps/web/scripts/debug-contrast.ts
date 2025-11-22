/**
 * Debug script to get detailed color contrast information
 */

import { chromium } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.goto('http://localhost:3000/chess');
  await page.waitForTimeout(2000);

  const axeBuilder = new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']);

  const results = await axeBuilder.analyze();

  // Find color-contrast violations
  const contrastViolations = results.violations.filter(v => v.id === 'color-contrast');

  if (contrastViolations.length > 0) {
    console.log('Color Contrast Violations:\n');
    for (const violation of contrastViolations) {
      console.log(`Rule: ${violation.id}`);
      console.log(`Help: ${violation.help}\n`);

      for (const [idx, node] of violation.nodes.entries()) {
        console.log(`Node ${idx + 1}:`);
        console.log(`  HTML: ${node.html}`);
        console.log(`  Target: ${node.target.join(' > ')}`);
        console.log(`  Failure Summary:\n${node.failureSummary}`);

        // Get computed colors if available
        if (node.any && node.any.length > 0) {
          console.log(`  Details:`);
          node.any.forEach((check: any) => {
            if (check.data) {
              console.log(`    FG Color: ${check.data.fgColor}`);
              console.log(`    BG Color: ${check.data.bgColor}`);
              console.log(`    Contrast Ratio: ${check.data.contrastRatio}`);
              console.log(`    Expected: ${check.data.expectedContrastRatio}`);
            }
          });
        }
        console.log('');
      }
    }
  } else {
    console.log('✅ No color-contrast violations found!');
  }

  // Also check the actual computed styles of the link
  console.log('\n--- Direct Element Inspection ---');
  const linkExists = await page.locator('a[href="/"]').first().isVisible();

  if (linkExists) {
    const computedStyles = await page.locator('a[href="/"]').first().evaluate((el) => {
      const styles = window.getComputedStyle(el);
      const parent = el.parentElement;
      const parentStyles = parent ? window.getComputedStyle(parent) : null;

      return {
        link: {
          color: styles.color,
          backgroundColor: styles.backgroundColor,
          display: styles.display,
        },
        parent: parentStyles ? {
          backgroundColor: parentStyles.backgroundColor,
        } : null
      };
    });

    console.log('Link computed styles:', JSON.stringify(computedStyles, null, 2));
  } else {
    console.log('Link not found on page');
  }

  await browser.close();
}

main();
