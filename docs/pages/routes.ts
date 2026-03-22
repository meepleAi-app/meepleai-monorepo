/**
 * Route Discovery
 *
 * Scans apps/web/src/app for all page.tsx files and builds
 * a sorted list of PageEntry objects for the manifest.
 */

import * as fs from 'fs';
import * as path from 'path';

import { config, GROUP_CONFIG, HARDCODED_PARAMS, type PageEntry } from './config';

// ============================================================================
// Public API
// ============================================================================

export function discoverRoutes(): PageEntry[] {
  const pages: PageEntry[] = [];
  scanDirectory(config.pagesDir, '', pages);

  // Sort by group order, then alphabetically by route
  const groupOrder = Object.values(GROUP_CONFIG).map(g => g.label);
  return pages.sort((a, b) => {
    const aIdx = groupOrder.indexOf(a.group);
    const bIdx = groupOrder.indexOf(b.group);
    if (aIdx !== bIdx) return (aIdx === -1 ? 999 : aIdx) - (bIdx === -1 ? 999 : bIdx);
    return a.routePattern.localeCompare(b.routePattern);
  });
}

// ============================================================================
// Filesystem Scanning
// ============================================================================

function scanDirectory(dir: string, relativePath: string, pages: PageEntry[]): void {
  if (!fs.existsSync(dir)) return;

  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    if (entry.isDirectory()) {
      // Skip hidden dirs and node_modules
      if (entry.name.startsWith('.') || entry.name === 'node_modules') continue;
      scanDirectory(
        path.join(dir, entry.name),
        relativePath ? `${relativePath}/${entry.name}` : entry.name,
        pages
      );
    } else if (entry.name === 'page.tsx') {
      const page = buildPageEntry(relativePath);
      if (page) pages.push(page);
    }
  }
}

// ============================================================================
// Page Entry Builder
// ============================================================================

function buildPageEntry(relativePath: string): PageEntry | null {
  const segments = relativePath.split('/').filter(Boolean);
  if (segments.length === 0) return null;

  const firstSegment = segments[0];
  const groupConfig = GROUP_CONFIG[firstSegment];

  const group = groupConfig?.label || 'other';
  const requiresAuth = groupConfig?.requiresAuth || false;
  const requiresAdmin = groupConfig?.requiresAdmin || false;

  // Build route segments (remove route group wrappers)
  const routeSegments = segments
    .filter(s => {
      // Remove Next.js route group parenthetical wrappers
      if (s.startsWith('(') && s.endsWith(')')) return false;
      return true;
    });

  const routePattern = '/' + routeSegments.join('/');

  // Check for hardcoded skip patterns
  for (const [pattern, value] of Object.entries(HARDCODED_PARAMS)) {
    if (routePattern.includes(pattern) && value === '__skip__') {
      return null;
    }
  }

  // Build ID
  const idSegments = routeSegments.map(s => s.replace(/\[.*?\]/g, 'param'));
  const id = `${group}-${idSegments.join('-') || 'index'}`;

  // Derive title and description
  const title = deriveTitle(routeSegments);
  const description = `${capitalize(group)} — ${title}`;

  // Screenshot path
  const screenshotName = idSegments.join('-') || 'index';
  const screenshot = `screenshots/${group}/${screenshotName}.png`;

  // Detect dynamic params
  const dynamicSegments = routeSegments.filter(s => s.startsWith('[') && s.endsWith(']'));
  const hasParams = dynamicSegments.length > 0;
  const params = hasParams
    ? Object.fromEntries(dynamicSegments.map(s => [s.replace(/[\[\]]/g, ''), 'unresolved']))
    : null;

  return {
    id,
    route: routePattern,
    routePattern,
    group,
    title,
    description,
    screenshot,
    requiresAuth,
    requiresAdmin,
    params,
    status: 'pending',
    capturedAt: null,
    error: null,
  };
}

// ============================================================================
// Title Derivation
// ============================================================================

function deriveTitle(segments: string[]): string {
  if (segments.length === 0) return 'Home';

  const last = segments[segments.length - 1];

  // Dynamic param → use parent + "Detail"
  if (last.startsWith('[')) {
    if (segments.length > 1) {
      const parent = segments[segments.length - 2];
      // Don't add "Detail" if parent is also dynamic
      if (parent.startsWith('[')) return 'Detail';
      return capitalize(parent.replace(/-/g, ' ')) + ' Detail';
    }
    return 'Detail';
  }

  return capitalize(last.replace(/-/g, ' '));
}

function capitalize(str: string): string {
  return str.replace(/\b\w/g, c => c.toUpperCase());
}
