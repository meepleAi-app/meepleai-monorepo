/**
 * Automated Accessibility Audit Script (UI-05)
 *
 * This script uses Playwright + axe-core to scan all pages
 * and generate a comprehensive accessibility audit report.
 *
 * Usage: npx ts-node scripts/run-accessibility-audit.ts
 */

import { chromium, Browser, Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import * as fs from 'fs';
import * as path from 'path';

interface PageConfig {
  name: string;
  url: string;
  requiresAuth: boolean;
  description: string;
}

interface ViolationSummary {
  id: string;
  impact: string;
  description: string;
  help: string;
  helpUrl: string;
  nodes: number;
}

interface PageAuditResult {
  page: string;
  url: string;
  timestamp: string;
  violations: ViolationSummary[];
  passes: number;
  incomplete: number;
  inapplicable: number;
  critical: number;
  serious: number;
  moderate: number;
  minor: number;
}

const PAGES_TO_AUDIT: PageConfig[] = [
  {
    name: 'Landing Page',
    url: '/',
    requiresAuth: false,
    description: 'Home page with hero, features, and auth modal'
  },
  {
    name: 'Chat',
    url: '/chat',
    requiresAuth: true,
    description: 'Chat interface with Timeline component (UI-04)'
  },
  {
    name: 'Upload',
    url: '/upload',
    requiresAuth: true,
    description: 'PDF upload wizard'
  },
  {
    name: 'Editor',
    url: '/editor',
    requiresAuth: true,
    description: 'RuleSpec editor'
  },
  {
    name: 'Versions',
    url: '/versions',
    requiresAuth: true,
    description: 'Version comparison and diff viewer'
  },
  {
    name: 'Admin',
    url: '/admin',
    requiresAuth: true,
    description: 'Admin dashboard with charts'
  },
  {
    name: 'N8N',
    url: '/n8n',
    requiresAuth: true,
    description: 'n8n workflow configuration'
  },
  {
    name: 'Logs',
    url: '/logs',
    requiresAuth: true,
    description: 'AI logs viewer'
  },
  {
    name: 'Setup',
    url: '/setup',
    requiresAuth: true,
    description: 'Setup wizard (AI-03)'
  },
  {
    name: 'Chess',
    url: '/chess',
    requiresAuth: false,
    description: 'Chess game UI'
  }
];

const BASE_URL = process.env.BASE_URL || 'http://localhost:3000';

async function login(page: Page): Promise<void> {
  console.log('  🔐 Logging in...');

  await page.goto(`${BASE_URL}/`);

  // Click "Get Started" button to open auth modal
  await page.click('text=Get Started');

  // Wait for modal to be visible
  await page.waitForSelector('input[type="email"]', { timeout: 5000 });

  // Fill login form (using demo account)
  await page.fill('input[type="email"]', 'user@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');

  // Submit login
  await page.click('button[type="submit"]');

  // Wait for navigation to chat page (confirms login success)
  await page.waitForURL('**/chat', { timeout: 10000 });

  console.log('  ✓ Login successful');
}

async function auditPage(
  page: Page,
  config: PageConfig,
  isAuthenticated: boolean
): Promise<PageAuditResult> {
  console.log(`\n📄 Auditing: ${config.name}`);
  console.log(`   URL: ${config.url}`);

  // Navigate to page
  await page.goto(`${BASE_URL}${config.url}`, { waitUntil: 'networkidle' });

  // Wait a bit for dynamic content to load
  await page.waitForTimeout(1000);

  console.log('  ⚡ Running axe-core scan...');

  // Run axe accessibility scan
  const axeBuilder = new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .exclude('#webpack-dev-server-client-overlay'); // Exclude dev overlay

  const results = await axeBuilder.analyze();

  // Categorize violations by impact
  const critical = results.violations.filter(v => v.impact === 'critical').length;
  const serious = results.violations.filter(v => v.impact === 'serious').length;
  const moderate = results.violations.filter(v => v.impact === 'moderate').length;
  const minor = results.violations.filter(v => v.impact === 'minor').length;

  console.log(`  ✓ Scan complete`);
  console.log(`    Violations: ${results.violations.length} (Critical: ${critical}, Serious: ${serious}, Moderate: ${moderate}, Minor: ${minor})`);
  console.log(`    Passes: ${results.passes.length}`);

  // Transform violations for report
  const violations: ViolationSummary[] = results.violations.map(v => ({
    id: v.id,
    impact: v.impact || 'unknown',
    description: v.description,
    help: v.help,
    helpUrl: v.helpUrl,
    nodes: v.nodes.length
  }));

  return {
    page: config.name,
    url: config.url,
    timestamp: new Date().toISOString(),
    violations,
    passes: results.passes.length,
    incomplete: results.incomplete.length,
    inapplicable: results.inapplicable.length,
    critical,
    serious,
    moderate,
    minor
  };
}

function generateMarkdownReport(results: PageAuditResult[]): string {
  const timestamp = new Date().toLocaleString('it-IT');

  let markdown = `# Accessibility Audit Report (UI-05)

**Generated:** ${timestamp}
**Tool:** axe-core (via @axe-core/playwright)
**Standard:** WCAG 2.1 AA
**Pages Audited:** ${results.length}

---

## Executive Summary

`;

  // Calculate totals
  const totalViolations = results.reduce((sum, r) => sum + r.violations.length, 0);
  const totalCritical = results.reduce((sum, r) => sum + r.critical, 0);
  const totalSerious = results.reduce((sum, r) => sum + r.serious, 0);
  const totalModerate = results.reduce((sum, r) => sum + r.moderate, 0);
  const totalMinor = results.reduce((sum, r) => sum + r.minor, 0);

  markdown += `| Severity | Count |\n`;
  markdown += `|----------|-------|\n`;
  markdown += `| 🔴 Critical | ${totalCritical} |\n`;
  markdown += `| 🟠 Serious | ${totalSerious} |\n`;
  markdown += `| 🟡 Moderate | ${totalModerate} |\n`;
  markdown += `| 🔵 Minor | ${totalMinor} |\n`;
  markdown += `| **Total** | **${totalViolations}** |\n\n`;

  // Status
  if (totalCritical === 0 && totalSerious === 0) {
    markdown += `✅ **Status:** PASS - No blocking accessibility errors\n\n`;
  } else {
    markdown += `❌ **Status:** FAIL - ${totalCritical + totalSerious} blocking errors found\n\n`;
  }

  markdown += `---

## Page-by-Page Results

`;

  // Results for each page
  results.forEach(result => {
    markdown += `### ${result.page}\n\n`;
    markdown += `**URL:** \`${result.url}\`  \n`;
    markdown += `**Violations:** ${result.violations.length} (Critical: ${result.critical}, Serious: ${result.serious}, Moderate: ${result.moderate}, Minor: ${result.minor})  \n`;
    markdown += `**Passes:** ${result.passes}  \n\n`;

    if (result.violations.length > 0) {
      markdown += `#### Violations\n\n`;

      // Group by severity
      const critical = result.violations.filter(v => v.impact === 'critical');
      const serious = result.violations.filter(v => v.impact === 'serious');
      const moderate = result.violations.filter(v => v.impact === 'moderate');
      const minor = result.violations.filter(v => v.impact === 'minor');

      if (critical.length > 0) {
        markdown += `##### 🔴 Critical\n\n`;
        critical.forEach(v => {
          markdown += `- **${v.id}** (${v.nodes} instance${v.nodes > 1 ? 's' : ''})\n`;
          markdown += `  - ${v.help}\n`;
          markdown += `  - [Learn more](${v.helpUrl})\n`;
        });
        markdown += `\n`;
      }

      if (serious.length > 0) {
        markdown += `##### 🟠 Serious\n\n`;
        serious.forEach(v => {
          markdown += `- **${v.id}** (${v.nodes} instance${v.nodes > 1 ? 's' : ''})\n`;
          markdown += `  - ${v.help}\n`;
          markdown += `  - [Learn more](${v.helpUrl})\n`;
        });
        markdown += `\n`;
      }

      if (moderate.length > 0) {
        markdown += `##### 🟡 Moderate\n\n`;
        moderate.forEach(v => {
          markdown += `- **${v.id}** (${v.nodes} instance${v.nodes > 1 ? 's' : ''})\n`;
          markdown += `  - ${v.help}\n`;
          markdown += `  - [Learn more](${v.helpUrl})\n`;
        });
        markdown += `\n`;
      }

      if (minor.length > 0) {
        markdown += `<details>\n<summary>🔵 Minor Issues (${minor.length})</summary>\n\n`;
        minor.forEach(v => {
          markdown += `- **${v.id}** (${v.nodes} instance${v.nodes > 1 ? 's' : ''})\n`;
          markdown += `  - ${v.help}\n`;
          markdown += `  - [Learn more](${v.helpUrl})\n`;
        });
        markdown += `\n</details>\n\n`;
      }
    } else {
      markdown += `✅ No accessibility violations found!\n\n`;
    }

    markdown += `---\n\n`;
  });

  // Recommendations
  markdown += `## Recommendations

Based on the audit findings, the following actions are recommended:

### Priority 1: Critical & Serious Issues
`;

  if (totalCritical + totalSerious > 0) {
    markdown += `These **MUST** be fixed before considering the application WCAG 2.1 AA compliant.

`;

    // List unique critical/serious violation types
    const blockingViolations = new Set<string>();
    results.forEach(r => {
      r.violations.forEach(v => {
        if (v.impact === 'critical' || v.impact === 'serious') {
          blockingViolations.add(v.id);
        }
      });
    });

    blockingViolations.forEach(id => {
      markdown += `- [ ] Fix \`${id}\` across all affected pages\n`;
    });
  } else {
    markdown += `✅ No critical or serious issues found!\n`;
  }

  markdown += `
### Priority 2: Moderate Issues

These **SHOULD** be addressed to improve accessibility.

`;

  if (totalModerate > 0) {
    const moderateViolations = new Set<string>();
    results.forEach(r => {
      r.violations.forEach(v => {
        if (v.impact === 'moderate') {
          moderateViolations.add(v.id);
        }
      });
    });

    moderateViolations.forEach(id => {
      markdown += `- [ ] Review \`${id}\` instances\n`;
    });
  } else {
    markdown += `✅ No moderate issues found!\n`;
  }

  markdown += `
### Priority 3: Minor Issues

These can be addressed in future iterations.

---

## Next Steps

1. **Review this report** with the development team
2. **Create fix tasks** for critical and serious issues (Fase 5 of UI-05)
3. **Implement accessible components** using Magic MCP (Fase 4 of UI-05)
4. **Add automated tests** to prevent regressions (Fase 3 of UI-05)
5. **Perform manual testing** with keyboard and screen reader (Fase 6 of UI-05)
6. **Re-run audit** after fixes are implemented

---

## Appendix: Testing Methodology

### Tools Used
- **axe-core:** @axe-core/playwright (latest)
- **Playwright:** @playwright/test (latest)
- **Browser:** Chromium (latest)

### WCAG Tags Applied
- \`wcag2a\` - WCAG 2.0 Level A
- \`wcag2aa\` - WCAG 2.0 Level AA
- \`wcag21a\` - WCAG 2.1 Level A
- \`wcag21aa\` - WCAG 2.1 Level AA

### Pages Audited

`;

  PAGES_TO_AUDIT.forEach(page => {
    markdown += `- **${page.name}** (\`${page.url}\`) - ${page.description}\n`;
  });

  markdown += `
---

**Report generated by:** UI-05 Accessibility Audit Script
**Issue:** #306 (UI-05 - Audit accessibilità baseline)
`;

  return markdown;
}

async function main() {
  console.log('🚀 Starting Accessibility Audit (UI-05)\n');
  console.log(`Base URL: ${BASE_URL}\n`);

  // Launch browser
  const browser: Browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page: Page = await context.newPage();

  const results: PageAuditResult[] = [];
  let isAuthenticated = false;

  try {
    for (const config of PAGES_TO_AUDIT) {
      // Authenticate if needed and not yet authenticated
      if (config.requiresAuth && !isAuthenticated) {
        await login(page);
        isAuthenticated = true;
      }

      // Audit the page
      const result = await auditPage(page, config, isAuthenticated);
      results.push(result);
    }

    // Generate reports
    console.log('\n📊 Generating reports...');

    // Markdown report
    const markdown = generateMarkdownReport(results);
    const reportDir = path.join(__dirname, '..', '..', '..', 'docs', 'issue');
    const reportPath = path.join(reportDir, 'ui-05-accessibility-audit.md');

    // Ensure directory exists
    if (!fs.existsSync(reportDir)) {
      fs.mkdirSync(reportDir, { recursive: true });
    }

    fs.writeFileSync(reportPath, markdown, 'utf-8');
    console.log(`✓ Markdown report saved: ${reportPath}`);

    // JSON report (for CI/CD)
    const jsonReport = {
      timestamp: new Date().toISOString(),
      baseUrl: BASE_URL,
      summary: {
        totalPages: results.length,
        totalViolations: results.reduce((sum, r) => sum + r.violations.length, 0),
        critical: results.reduce((sum, r) => sum + r.critical, 0),
        serious: results.reduce((sum, r) => sum + r.serious, 0),
        moderate: results.reduce((sum, r) => sum + r.moderate, 0),
        minor: results.reduce((sum, r) => sum + r.minor, 0)
      },
      results
    };

    const jsonPath = path.join(reportDir, 'ui-05-accessibility-audit.json');
    fs.writeFileSync(jsonPath, JSON.stringify(jsonReport, null, 2), 'utf-8');
    console.log(`✓ JSON report saved: ${jsonPath}`);

    // Print summary
    console.log('\n📈 Audit Summary:');
    console.log(`   Total Violations: ${jsonReport.summary.totalViolations}`);
    console.log(`   🔴 Critical: ${jsonReport.summary.critical}`);
    console.log(`   🟠 Serious: ${jsonReport.summary.serious}`);
    console.log(`   🟡 Moderate: ${jsonReport.summary.moderate}`);
    console.log(`   🔵 Minor: ${jsonReport.summary.minor}`);

    // Exit code based on blocking errors
    const blockingErrors = jsonReport.summary.critical + jsonReport.summary.serious;
    if (blockingErrors > 0) {
      console.log(`\n❌ FAIL: ${blockingErrors} blocking accessibility errors found`);
      process.exit(1);
    } else {
      console.log('\n✅ PASS: No blocking accessibility errors');
      process.exit(0);
    }

  } catch (error) {
    console.error('\n❌ Error during audit:', error);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

main();
