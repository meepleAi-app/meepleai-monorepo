import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { generateClusterReviewQueue } from '../generate-cluster-review-queue.mjs';

describe('generateClusterReviewQueue', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'cluster-queue-'));
  });

  afterEach(() => {
    rmSync(workDir, { recursive: true, force: true });
  });

  it('emits queue with shipped stories table', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/auth-flow.html',
          design_intent: 'current',
          confidence: 0.9,
          reasoning: 'auth-flow shipped',
          sub_components: ['LoginScreen'],
          pair_disagreement: false,
          suggested_tracking_issue: null,
        },
      ],
      stories: [
        {
          mockup_stem: 'auth-flow',
          story_path: 'apps/web/src/app/(public)/(auth)/auth-flow.stories.tsx',
          fixtures_path: 'apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts',
          frame_count: 6,
        },
      ],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/auth-flow/);
    expect(queue).toMatch(/6 frames/);
    expect(queue).toMatch(/story_path.*auth-flow\.stories\.tsx/);
  });

  it('flags obsolete mockup with DEFERRED note', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/sp4-old.html',
          design_intent: 'forward-refactor-obsolete',
          confidence: 0.9,
          reasoning: 'Obsolete',
          sub_components: [],
          pair_disagreement: false,
          suggested_tracking_issue: { title: 'T', body: 'B' },
          obsolete_tracking_issue_ref: '#9999',
        },
      ],
      stories: [],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/DEFERRED post-Phase-B-tracking-#9999/);
    expect(queue).toMatch(/sp4-old\.html/);
  });

  it('throws on malformed input (missing clusterId, Code-reviewer Finding 6)', () => {
    const malformed = { byCluster: { auth: [] } };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(malformed));

    expect(() =>
      generateClusterReviewQueue({
        auditPath,
        outDir: join(workDir, 'docs-out'),
      })
    ).toThrow(/Invalid cluster audit JSON.*clusterId|classifications/);
  });

  it('calls out pair_disagreement explicitly', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          design_intent: 'current',
          confidence: 0.7,
          reasoning: 'HTML canonical, JSX twin differs',
          sub_components: [],
          pair_disagreement: true,
          suggested_tracking_issue: null,
        },
      ],
      stories: [
        {
          mockup_stem: 'foo',
          story_path: 'apps/web/foo.stories.tsx',
          fixtures_path: 'apps/web/fix.ts',
          frame_count: 1,
        },
      ],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/Pair disagreements/);
    expect(queue).toMatch(/foo\.html/);
  });
});
