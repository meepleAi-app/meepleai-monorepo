import { describe, it, expect, beforeEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseDrafts, updateFidelityForIssue, runBatch } from '../create-tracking-issues.mjs';

describe('parseDrafts', () => {
  it('extracts mockup paths + titles + bodies from drafts markdown', () => {
    const drafts = `# Tracking Issues Drafts — DS-17 Phase B

NOT created until designer sign-off.

## Draft 1: admin-mockups/design_files/sp4-old.html

**Title**: \`Mark sp4-old obsolete\`

**Body**:

Replaced by sp4-new in Asse C #1898.

---

## Draft 2: admin-mockups/design_files/sp3-deprecated.html

**Title**: \`Track sp3-deprecated\`

**Body**:

Reason for obsolescence.

---
`;

    const result = parseDrafts(drafts);
    expect(result).toHaveLength(2);
    expect(result[0].mockup_path).toBe('admin-mockups/design_files/sp4-old.html');
    expect(result[0].title).toBe('Mark sp4-old obsolete');
    expect(result[0].body).toMatch(/Replaced by sp4-new/);
    expect(result[1].mockup_path).toBe('admin-mockups/design_files/sp3-deprecated.html');
  });

  it('returns empty array on no drafts', () => {
    const drafts = `# Tracking Issues Drafts — DS-17 Phase B\n\n_No obsolete classifications._`;
    expect(parseDrafts(drafts)).toEqual([]);
  });
});

describe('runBatch (rollback path — Code-reviewer Finding 4)', () => {
  it('rolls back already-created issues when one mid-batch creation fails', async () => {
    const drafts = [
      { mockup_path: 'a.html', title: 'A', body: 'b1' },
      { mockup_path: 'b.html', title: 'B', body: 'b2' },
      { mockup_path: 'c.html', title: 'C', body: 'b3' },
    ];
    const createCalls: string[] = [];
    const closeCalls: number[] = [];
    let nextIssue = 100;
    const createFn = (title: string, _body: string) => {
      createCalls.push(title);
      if (title === 'B') throw new Error('simulated API failure');
      return nextIssue++;
    };
    const closeFn = (issueNumber: number) => {
      closeCalls.push(issueNumber);
    };
    const updateFn = () => {
      // no-op for test
    };

    await expect(runBatch(drafts, { createFn, closeFn, updateFn })).rejects.toThrow(/simulated/);

    expect(createCalls).toEqual(['A', 'B']);
    expect(closeCalls).toEqual([100]);
  });

  it('completes successfully when all creates succeed', async () => {
    const drafts = [
      { mockup_path: 'a.html', title: 'A', body: 'b1' },
      { mockup_path: 'b.html', title: 'B', body: 'b2' },
    ];
    let nextIssue = 200;
    const createFn = () => nextIssue++;
    const closeFn = () => {
      throw new Error('should not be called when all succeed');
    };
    const updates: Array<[string, number]> = [];
    const updateFn = (mockup_path: string, issueNumber: number) => {
      updates.push([mockup_path, issueNumber]);
    };

    await runBatch(drafts, { createFn, closeFn, updateFn });

    expect(updates).toEqual([
      ['a.html', 200],
      ['b.html', 201],
    ]);
  });
});

describe('updateFidelityForIssue', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'fidelity-update-'));
  });

  it('updates obsolete_tracking_issue field from PENDING to #N', () => {
    const fidelity = {
      _comment: '',
      mockup: { source: 'admin-mockups/design_files/sp4-old.html', states: ['default'] },
      acceptance: {
        design_intent: 'forward-refactor-obsolete',
        obsolete_tracking_issue: 'PENDING',
        visual_diff_max_px: 5,
        color_delta_e_max: 3,
        tokens_used: 'canonical_only',
        legacy_token_names_forbidden: true,
        states_covered: ['default'],
        a11y_axe: 'AA',
        a11y_violations_max: 0,
        responsive_breakpoints: [375, 768, 1024, 1440],
        designer_approved_by: '',
        designer_approved_on: '',
        story_path: '',
        fixtures_path: '',
        viewports: ['desktop'],
      },
    };
    const path = join(workDir, 'sp4-old.fidelity.json');
    writeFileSync(path, JSON.stringify(fidelity, null, 2));

    updateFidelityForIssue(path, 9999);

    const after = JSON.parse(readFileSync(path, 'utf-8'));
    expect(after.acceptance.obsolete_tracking_issue).toBe('#9999');
  });
});
