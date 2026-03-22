/**
 * Page Catalog Configuration
 *
 * Types, constants, and configuration for the automated page catalog
 * screenshot system. Used by capture.ts and generate.ts.
 */

import * as path from 'path';

// ============================================================================
// Types
// ============================================================================

export interface PageEntry {
  id: string;
  route: string;
  routePattern: string;
  group: string;
  title: string;
  description: string;
  screenshot: string;
  requiresAuth: boolean;
  requiresAdmin: boolean;
  params: Record<string, string> | null;
  status: 'pending' | 'ok' | 'error' | 'timeout' | 'skipped';
  capturedAt: string | null;
  error: string | null;
}

export interface Manifest {
  generatedAt: string;
  baseUrl: string;
  stats: {
    total: number;
    captured: number;
    errors: number;
    timeouts: number;
    skipped: number;
    groups: number;
  };
  pages: PageEntry[];
}

// ============================================================================
// Configuration
// ============================================================================

const ROOT = path.resolve(__dirname, '../..');

export const config = {
  baseUrl: process.env.PAGE_CATALOG_URL || 'https://meepleai.app',
  secretsPath: path.join(ROOT, 'infra/secrets/staging/admin.secret'),
  pagesDir: path.join(ROOT, 'apps/web/src/app'),
  outputDir: path.join(__dirname, 'screenshots'),
  distDir: path.join(__dirname, 'dist'),
  manifestPath: path.join(__dirname, 'manifest.json'),
  viewport: { width: 1440, height: 900 },
  pageTimeout: 15000,
  delayAfterLoad: 1000,
  concurrency: 3,
} as const;

// ============================================================================
// Route Group Config
// ============================================================================

export const GROUP_CONFIG: Record<string, {
  requiresAuth: boolean;
  requiresAdmin: boolean;
  label: string;
}> = {
  '(public)':        { requiresAuth: false, requiresAdmin: false, label: 'public' },
  '(auth)':          { requiresAuth: false, requiresAdmin: false, label: 'auth' },
  '(authenticated)': { requiresAuth: true,  requiresAdmin: false, label: 'authenticated' },
  'admin':           { requiresAuth: true,  requiresAdmin: true,  label: 'admin' },
  '(chat)':          { requiresAuth: true,  requiresAdmin: false, label: 'chat' },
  '(dev)':           { requiresAuth: false, requiresAdmin: false, label: 'dev' },
  '(docs)':          { requiresAuth: false, requiresAdmin: false, label: 'docs' },
  'join':            { requiresAuth: false, requiresAdmin: false, label: 'join' },
  'offline':         { requiresAuth: false, requiresAdmin: false, label: 'offline' },
};

// ============================================================================
// Parameter Resolution Config
// ============================================================================

export const PARAM_RESOLVERS: Record<string, {
  endpoint: string;
  extract: (data: any) => string | null;
}> = {
  'shared-games': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'games': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'giochi': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'agents': {
    endpoint: '/api/v1/agents?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'players': {
    endpoint: '/api/v1/players?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'sessions': {
    endpoint: '/api/v1/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'game-nights': {
    endpoint: '/api/v1/game-nights?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'chat': {
    endpoint: '/api/v1/chat-threads?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'playlists': {
    endpoint: '/api/v1/playlists?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'knowledge-base': {
    endpoint: '/api/v1/knowledge-base?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'play-records': {
    endpoint: '/api/v1/play-records?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'users': {
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'definitions': {
    endpoint: '/api/v1/admin/agents/definitions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'library': {
    endpoint: '/api/v1/library?pageSize=1',
    extract: (data) => data?.items?.[0]?.gameId || data?.[0]?.gameId || null,
  },
  'private': {
    endpoint: '/api/v1/library/private?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'ab-testing': {
    endpoint: '/api/v1/admin/agents/ab-testing?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'ui-library': {
    endpoint: '/api/v1/admin/ui-library?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'compositions': {
    endpoint: '/api/v1/admin/ui-library/compositions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'toolkit': {
    endpoint: '/api/v1/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'live': {
    endpoint: '/api/v1/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'agent-proposals': {
    endpoint: '/api/v1/editor/agent-proposals?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
};

// Hardcoded param values for routes that can't be resolved via API
export const HARDCODED_PARAMS: Record<string, string> = {
  'showcase/[component]': 'button',
  'join/[inviteToken]': '__skip__',
  'shared/[token]': '__skip__',
  'accept-invite': '__skip__',
};
