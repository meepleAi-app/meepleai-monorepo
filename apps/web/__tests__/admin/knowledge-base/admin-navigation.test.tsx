/**
 * Admin Navigation Links Tests (Issue #4788)
 *
 * Validates that all navigation links in admin hub pages point to
 * existing routes by checking page.tsx files on the filesystem.
 */

import { existsSync } from 'fs';
import { resolve } from 'path';

import { describe, it, expect } from 'vitest';

const APP_ROOT = resolve(process.cwd(), 'src/app');

/**
 * Maps a URL path to the filesystem page.tsx location.
 * Accounts for Next.js route groups like (dashboard).
 */
function pageExists(urlPath: string): boolean {
  const segments = urlPath.replace(/^\//, '').split('/');

  // Try direct path: /admin/knowledge-base/documents → admin/knowledge-base/documents/page.tsx
  const directPath = resolve(APP_ROOT, ...segments, 'page.tsx');
  if (existsSync(directPath)) return true;

  // Try with (dashboard) route group under admin
  if (segments[0] === 'admin' && segments.length >= 2) {
    const dashboardPath = resolve(APP_ROOT, 'admin', '(dashboard)', ...segments.slice(1), 'page.tsx');
    if (existsSync(dashboardPath)) return true;
  }

  return false;
}

describe('Admin Navigation - Knowledge Base Hub Links', () => {
  const kbHubLinks = [
    { label: 'Documents Library', href: '/admin/knowledge-base/documents' },
    { label: 'Vector Collections', href: '/admin/knowledge-base/vectors' },
    { label: 'Processing Queue', href: '/admin/knowledge-base/queue' },
    { label: 'Upload Documents', href: '/admin/knowledge-base/upload' },
    { label: 'Pipeline Explorer', href: '/admin/knowledge-base/pipeline' },
    { label: 'Settings', href: '/admin/knowledge-base/settings' },
  ];

  for (const link of kbHubLinks) {
    it(`"${link.label}" → ${link.href} has a page.tsx`, () => {
      expect(pageExists(link.href), `No page.tsx found for ${link.href}`).toBe(true);
    });
  }
});

describe('Admin Navigation - Agents Hub Links', () => {
  const agentsHubLinks = [
    { label: 'Agent Builder', href: '/admin/agents/builder' },
    { label: 'Pipeline Explorer', href: '/admin/agents/pipeline' },
    { label: 'Debug Console', href: '/admin/agents/debug' },
  ];

  for (const link of agentsHubLinks) {
    it(`"${link.label}" → ${link.href} has a page.tsx`, () => {
      expect(pageExists(link.href), `No page.tsx found for ${link.href}`).toBe(true);
    });
  }
});

describe('Admin Navigation - All Agent Subpages', () => {
  const agentSubpages = [
    '/admin/agents/strategy',
    '/admin/agents/analytics',
    '/admin/agents/chat-history',
    '/admin/agents/models',
  ];

  for (const href of agentSubpages) {
    it(`${href} has a page.tsx`, () => {
      expect(pageExists(href), `No page.tsx found for ${href}`).toBe(true);
    });
  }
});

describe('Admin Navigation - Top Level Pages', () => {
  const topLevelPages = [
    '/admin/knowledge-base',
    '/admin/agents',
  ];

  for (const href of topLevelPages) {
    it(`${href} has a page.tsx`, () => {
      expect(pageExists(href), `No page.tsx found for ${href}`).toBe(true);
    });
  }
});
