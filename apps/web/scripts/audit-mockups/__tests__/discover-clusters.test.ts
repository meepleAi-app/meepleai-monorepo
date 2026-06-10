import { describe, it, expect } from 'vitest';
import { classifyFile, groupByPair, type ClusterId } from '../discover-clusters.mjs';

describe('classifyFile', () => {
  const cases: Array<[string, ClusterId]> = [
    // dev-fixtures
    ['00-hub.html', 'dev-fixtures'],
    ['04-design-system.html', 'dev-fixtures'],
    ['tokens.css', 'dev-fixtures'],
    ['data.js', 'dev-fixtures'],
    ['mobile-app.jsx', 'dev-fixtures'],
    ['state-matrix.html', 'dev-fixtures'],
    ['sp4-play-records-data.js', 'dev-fixtures'],

    // auth + onboarding
    ['auth-flow.html', 'auth'],
    ['onboarding.html', 'auth'],
    ['notifications.html', 'auth'],
    ['public.html', 'auth'],
    ['settings.html', 'auth'],
    ['verify-email.html', 'auth'],
    ['reset-password.html', 'auth'],

    // sp3
    ['sp3-join.html', 'sp3'],
    ['sp3-join.jsx', 'sp3'],
    ['hub-public.html', 'sp3'],
    ['library-public.html', 'sp3'],

    // sp4-core
    ['sp4-dashboard.html', 'sp4-core'],
    ['sp4-player-detail.html', 'sp4-core'],
    ['sp4-game-night.html', 'sp4-core'],
    ['sp4-library-desktop.html', 'sp4-core'],
    ['sp4-game-detail.html', 'sp4-core'],
    ['sp4-sessions-index.html', 'sp4-core'],
    ['sp4-session-catan-summary.html', 'sp4-core'],
    ['sp4-session-codenames-summary.html', 'sp4-core'],

    // sp4-sessions
    ['sp4-session-catan-live.html', 'sp4-sessions'],
    ['sp4-session-wingspan-live.jsx', 'sp4-sessions'],
    ['sp4-toolkit-detail.html', 'sp4-sessions'],
    ['sp4-toolkit-history.html', 'sp4-sessions'],
    ['sp4-scores-live.html', 'sp4-sessions'],
    ['sp4-recap.html', 'sp4-sessions'],
    ['sp4-gamebook-upload.html', 'sp4-sessions'],

    // sp6-7-nano
    ['sp6-admin-dashboard.html', 'sp6-7-nano'],
    ['sp7-rag-config.html', 'sp6-7-nano'],
    ['admin-users.html', 'sp6-7-nano'],
    ['nano-generator.html', 'sp6-7-nano'],
    ['rag-observability.html', 'sp6-7-nano'],
    ['observability-dashboard.html', 'sp6-7-nano'],
    ['generator-config.html', 'sp6-7-nano'],

    // Code-reviewer Finding 3: explicit prefix coverage
    ['librogame-game-night-storyboard.html', 'sp4-sessions'],
    ['librogame-runthrough-1.html', 'sp4-sessions'],
    ['nanolith-nav-bottom-mobile.html', 'dev-fixtures'],
    ['nanolith-nav-topbar.html', 'dev-fixtures'],
    ['chat-fullscreen.html', 'sp4-sessions'],
    ['sp5-profile-settings.html', 'auth'],
    ['pr-form-core.jsx', 'sp4-core'],
    ['index.html', 'dev-fixtures'],
  ];

  for (const [filename, expectedCluster] of cases) {
    it(`classifies ${filename} as ${expectedCluster}`, () => {
      expect(classifyFile(filename)).toBe(expectedCluster);
    });
  }

  it('falls back to sp6-7-nano with warning for unknown', () => {
    const warnings: string[] = [];
    const cluster = classifyFile('unknown-mystery.html', msg => warnings.push(msg));
    expect(cluster).toBe('sp6-7-nano');
    expect(warnings).toHaveLength(1);
    expect(warnings[0]).toMatch(/unknown-mystery/);
  });
});

describe('groupByPair', () => {
  it('pairs HTML and JSX twins', () => {
    const files = [
      { path: 'admin-mockups/design_files/sp3-join.html', type: 'html' as const },
      { path: 'admin-mockups/design_files/sp3-join.jsx', type: 'jsx' as const },
      { path: 'admin-mockups/design_files/standalone.html', type: 'html' as const },
    ];

    const grouped = groupByPair(files);
    const sp3Join = grouped.find(f => f.path.endsWith('sp3-join.html'));
    expect(sp3Join?.pairKey).toBe('sp3-join');
    const sp3JoinJsx = grouped.find(f => f.path.endsWith('sp3-join.jsx'));
    expect(sp3JoinJsx?.pairKey).toBe('sp3-join');
    const standalone = grouped.find(f => f.path.endsWith('standalone.html'));
    expect(standalone?.pairKey).toBeUndefined();
  });
});
