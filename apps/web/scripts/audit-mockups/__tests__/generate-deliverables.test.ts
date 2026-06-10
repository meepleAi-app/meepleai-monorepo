import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, existsSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { generateDeliverables } from '../generate-deliverables.mjs';

describe('generateDeliverables', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'audit-deliverables-'));
  });

  afterEach(() => {
    rmSync(workDir, { recursive: true, force: true });
  });

  it('uses PENDING sentinel for obsolete_tracking_issue (Code-reviewer Finding 1)', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 1,
      byCluster: {
        'dev-fixtures': [],
        auth: [],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-x.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'R',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'T', body: 'B' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const fidelity = JSON.parse(
      readFileSync(join(workDir, 'design_files/sp4-x.fidelity.json'), 'utf-8')
    );
    expect(fidelity.acceptance.obsolete_tracking_issue).toBe('PENDING');
  });

  it('generates one fidelity.json per classification', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 2,
      byCluster: {
        'dev-fixtures': [
          {
            mockup_path: 'admin-mockups/design_files/00-hub.html',
            design_intent: 'current',
            confidence: 0.95,
            reasoning: 'No markers',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        auth: [],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-dashboard.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'Asse C #1898',
            sub_components: ['DashboardClient'],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'Tracked in #2114', body: 'Already tracked' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    expect(existsSync(join(workDir, 'design_files/00-hub.fidelity.json'))).toBe(true);
    expect(existsSync(join(workDir, 'design_files/sp4-dashboard.fidelity.json'))).toBe(true);

    const obsoleteFidelity = JSON.parse(
      readFileSync(join(workDir, 'design_files/sp4-dashboard.fidelity.json'), 'utf-8')
    );
    expect(obsoleteFidelity.acceptance.design_intent).toBe('forward-refactor-obsolete');
    expect(obsoleteFidelity.acceptance.obsolete_tracking_issue).toBe('PENDING');

    const currentFidelity = JSON.parse(
      readFileSync(join(workDir, 'design_files/00-hub.fidelity.json'), 'utf-8')
    );
    expect(currentFidelity.acceptance.obsolete_tracking_issue).toBe('');
  });

  it('emits designer queue with obsolete and pair_disagreement sections', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 2,
      byCluster: {
        'dev-fixtures': [],
        auth: [
          {
            mockup_path: 'admin-mockups/design_files/auth-flow.html',
            design_intent: 'current',
            confidence: 0.4,
            reasoning: 'Ambiguous',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-old.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'Replaced',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'Mark obsolete', body: 'Replaced by X' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/mockup-designer-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/Obsolete candidates/);
    expect(queue).toMatch(/sp4-old\.html/);
    expect(queue).toMatch(/Low confidence/);
    expect(queue).toMatch(/auth-flow\.html/);
  });

  it('emits tracking-issues-drafts.md with one section per obsolete', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 1,
      byCluster: {
        'dev-fixtures': [],
        auth: [],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-x.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'R',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'X obsolete', body: 'Body Y' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const drafts = readFileSync(join(workDir, 'audits-out/tracking-issues-drafts.md'), 'utf-8');
    expect(drafts).toMatch(/X obsolete/);
    expect(drafts).toMatch(/Body Y/);
  });

  it('emits summary markdown with cluster table', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 1,
      byCluster: {
        'dev-fixtures': [
          {
            mockup_path: 'admin-mockups/design_files/00-hub.html',
            design_intent: 'current',
            confidence: 0.95,
            reasoning: 'R',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        auth: [],
        sp3: [],
        'sp4-core': [],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const summary = readFileSync(
      join(workDir, 'audits-out/2026-06-10-mockup-design-intent-audit.md'),
      'utf-8'
    );
    expect(summary).toMatch(/Summary/);
    expect(summary).toMatch(/dev-fixtures.*1/);
  });
});
