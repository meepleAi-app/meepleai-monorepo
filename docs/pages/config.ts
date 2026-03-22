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
  'discover': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'agents': {
    endpoint: '/api/v1/agents?pageSize=1',
    extract: (data) => data?.agents?.[0]?.id || data?.items?.[0]?.id || null,
  },
  'players': {
    // No list endpoint — uses admin users as fallback
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || null,
  },
  'sessions': {
    endpoint: '/api/v1/admin/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || data?.[0]?.sessionId || null,
  },
  'game-nights': {
    endpoint: '/api/v1/game-nights?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'chat': {
    // chat-threads requires gameId — use knowledge-base/my-chats as fallback
    endpoint: '/api/v1/knowledge-base/my-chats?skip=0&take=1',
    extract: (data) => data?.threads?.[0]?.id || null,
  },
  'playlists': {
    endpoint: '/api/v1/playlists?page=1&pageSize=1',
    extract: (data) => data?.playlists?.[0]?.id || data?.items?.[0]?.id || null,
  },
  'knowledge-base': {
    // No generic list — use shared-games as fallback (KB is per-game)
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || null,
  },
  'play-records': {
    endpoint: '/api/v1/play-records/history?pageSize=1',
    extract: (data) => data?.records?.[0]?.id || data?.items?.[0]?.id || null,
  },
  'users': {
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || null,
  },
  'definitions': {
    endpoint: '/api/v1/admin/agent-definitions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'library': {
    endpoint: '/api/v1/library?pageSize=1',
    extract: (data) => data?.items?.[0]?.gameId || data?.[0]?.gameId || null,
  },
  'private': {
    endpoint: '/api/v1/library?pageSize=1&stateFilter=private',
    extract: (data) => data?.items?.[0]?.gameId || null,
  },
  'ab-testing': {
    endpoint: '/api/v1/admin/ab-tests?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'ui-library': {
    // No endpoint exists — skip
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: () => null,
  },
  'compositions': {
    // No endpoint exists — skip
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: () => null,
  },
  'toolkit': {
    endpoint: '/api/v1/admin/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || data?.[0]?.sessionId || null,
  },
  'live': {
    endpoint: '/api/v1/admin/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || data?.[0]?.sessionId || null,
  },
  'agent-proposals': {
    endpoint: '/api/v1/migrations/pending?pageSize=1',
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
