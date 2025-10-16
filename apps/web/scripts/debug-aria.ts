/**
 * Debug script to get detailed ARIA violation information
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

  // Find aria-prohibited-attr violations
  const ariaViolations = results.violations.filter(v => v.id === 'aria-prohibited-attr');

  if (ariaViolations.length > 0) {
    console.log('ARIA Prohibited Attribute Violations:\n');
    ariaViolations.forEach(violation => {
      console.log(`Rule: ${violation.id}`);
      console.log(`Help: ${violation.help}\n`);
      violation.nodes.forEach((node, idx) => {
        console.log(`Node ${idx + 1}:`);
        console.log(`  HTML: ${node.html}`);
        console.log(`  Target: ${node.target.join(' > ')}`);
        console.log(`  Failure Summary: ${node.failureSummary}`);
        console.log('');
      });
    });
  } else {
    console.log('No aria-prohibited-attr violations found!');
  }

  // Also check color-contrast
  const contrastViolations = results.violations.filter(v => v.id === 'color-contrast');
  if (contrastViolations.length > 0) {
    console.log('\n\nColor Contrast Violations:\n');
    contrastViolations.forEach(violation => {
      violation.nodes.forEach((node, idx) => {
        console.log(`Node ${idx + 1}:`);
        console.log(`  HTML: ${node.html}`);
        console.log(`  Target: ${node.target.join(' > ')}`);
        console.log('');
      });
    });
  }

  await browser.close();
}

main();
