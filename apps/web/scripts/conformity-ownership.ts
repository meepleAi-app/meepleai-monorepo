/**
 * WS-C Phase 1 — Mockup ownership loader.
 *
 * Loads `mockup-ownership.bootstrap.json` (or any schema-conformant file),
 * validates against the v1 contract, applies framework defaults, and returns a
 * fully-resolved `RouteOwnership[]`.
 *
 * Consumed by:
 *   - Phase 2 Playwright specs (conformity-desktop/mobile projects)
 *   - Phase 3 workflows (path-trigger derivation, bootstrap script)
 *   - WS-F future auto-discovery tooling
 *
 * Refs: #1069 (WS-C), #1066 (umbrella).
 */
import { readFileSync } from 'node:fs';

const FRAMEWORK_DEFAULTS = {
  threshold: 0.1,
  conformityRatio: 0.05,
  viewports: [
    { name: 'desktop', width: 1280, height: 720 },
    { name: 'mobile', width: 375, height: 740 },
  ],
} as const;

export interface Viewport {
  name: string;
  width: number;
  height: number;
}

export interface RouteEntry {
  id: string;
  livePath: string;
  liveFixture?: Record<string, string>;
  mockup: string;
  triggerPaths: string[];
  threshold?: number;
  conformityRatio?: number;
  viewports?: Viewport[];
  notes?: string;
}

export interface OwnershipDefaults {
  threshold?: number;
  conformityRatio?: number;
  viewports?: Viewport[];
}

export interface OwnershipFile {
  version: 1;
  defaults?: OwnershipDefaults;
  routes: RouteEntry[];
}

export interface RouteOwnership {
  id: string;
  livePath: string;
  liveFixture: Record<string, string>;
  mockup: string;
  triggerPaths: string[];
  threshold: number;
  conformityRatio: number;
  viewports: Viewport[];
  notes?: string;
}

export interface ResolvedOwnership {
  version: 1;
  routes: RouteOwnership[];
}

const TOP_LEVEL_KEYS = new Set(['$schema', 'version', 'defaults', 'routes']);
const ROUTE_KEYS = new Set([
  'id',
  'livePath',
  'liveFixture',
  'mockup',
  'triggerPaths',
  'threshold',
  'conformityRatio',
  'viewports',
  'notes',
]);
const ID_PATTERN = /^[a-z][a-z0-9-]*$/;
const PLACEHOLDER_PATTERN = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g;

export function loadOwnership(path: string): ResolvedOwnership {
  const raw = readFileSync(path, 'utf8');
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (err) {
    throw new Error(`Failed to parse ${path}: ${(err as Error).message}`);
  }
  return validateAndResolve(parsed, path);
}

function validateAndResolve(input: unknown, source: string): ResolvedOwnership {
  if (!isPlainObject(input)) {
    throw new Error(`${source}: root must be an object`);
  }

  for (const key of Object.keys(input)) {
    if (!TOP_LEVEL_KEYS.has(key)) {
      throw new Error(`${source}: unknown top-level key "${key}"`);
    }
  }

  if (input.version !== 1) {
    throw new Error(`${source}: unsupported version ${String(input.version)} (expected 1)`);
  }

  const defaults = mergeDefaults(input.defaults, source);

  if (!Array.isArray(input.routes) || input.routes.length === 0) {
    throw new Error(`${source}: routes must be a non-empty array`);
  }

  const seenIds = new Set<string>();
  const routes: RouteOwnership[] = input.routes.map((raw, idx) =>
    resolveRoute(raw, defaults, source, idx, seenIds)
  );

  return { version: 1, routes };
}

function mergeDefaults(value: unknown, source: string): Required<OwnershipDefaults> {
  if (value === undefined) {
    return { ...FRAMEWORK_DEFAULTS, viewports: [...FRAMEWORK_DEFAULTS.viewports] };
  }
  if (!isPlainObject(value)) {
    throw new Error(`${source}: defaults must be an object`);
  }
  const threshold =
    pickNumber(value.threshold, 'defaults.threshold', source) ?? FRAMEWORK_DEFAULTS.threshold;
  const conformityRatio =
    pickNumber(value.conformityRatio, 'defaults.conformityRatio', source) ??
    FRAMEWORK_DEFAULTS.conformityRatio;
  const viewports = pickViewports(value.viewports, 'defaults.viewports', source) ?? [
    ...FRAMEWORK_DEFAULTS.viewports,
  ];
  return { threshold, conformityRatio, viewports };
}

function resolveRoute(
  raw: unknown,
  defaults: Required<OwnershipDefaults>,
  source: string,
  idx: number,
  seenIds: Set<string>
): RouteOwnership {
  const ctx = `${source} routes[${idx}]`;
  if (!isPlainObject(raw)) {
    throw new Error(`${ctx}: must be an object`);
  }

  for (const key of Object.keys(raw)) {
    if (!ROUTE_KEYS.has(key)) {
      throw new Error(`${ctx}: unknown key "${key}"`);
    }
  }

  const id = pickString(raw.id, `${ctx}.id`, source);
  if (!ID_PATTERN.test(id)) {
    throw new Error(`${ctx}.id "${id}" must match ${ID_PATTERN}`);
  }
  if (seenIds.has(id)) {
    throw new Error(`${ctx}.id duplicate "${id}"`);
  }
  seenIds.add(id);

  const livePath = pickString(raw.livePath, `${ctx}.livePath`, source);
  if (!livePath.startsWith('/')) {
    throw new Error(`${ctx}.livePath "${livePath}" must start with /`);
  }

  const mockup = pickString(raw.mockup, `${ctx}.mockup`, source);
  if (!mockup.endsWith('.html')) {
    throw new Error(`${ctx}.mockup "${mockup}" must end with .html`);
  }

  const triggerPaths = pickStringArray(raw.triggerPaths, `${ctx}.triggerPaths`, source);
  if (triggerPaths.length === 0) {
    throw new Error(`${ctx}.triggerPaths must be a non-empty array`);
  }

  const placeholders = collectPlaceholders(livePath);
  const liveFixture = pickRecord(raw.liveFixture, `${ctx}.liveFixture`, source) ?? {};
  for (const placeholder of placeholders) {
    if (!(placeholder in liveFixture)) {
      throw new Error(`${ctx}.liveFixture missing key "${placeholder}" required by livePath`);
    }
  }

  const threshold = pickNumber(raw.threshold, `${ctx}.threshold`, source) ?? defaults.threshold;
  const conformityRatio =
    pickNumber(raw.conformityRatio, `${ctx}.conformityRatio`, source) ?? defaults.conformityRatio;
  const viewports = pickViewports(raw.viewports, `${ctx}.viewports`, source) ?? defaults.viewports;
  const notes = raw.notes === undefined ? undefined : pickString(raw.notes, `${ctx}.notes`, source);

  return {
    id,
    livePath,
    liveFixture,
    mockup,
    triggerPaths,
    threshold,
    conformityRatio,
    viewports: viewports.map(v => ({ ...v })),
    notes,
  };
}

export function resolveLivePath(livePath: string, fixture: Record<string, string>): string {
  return livePath.replace(PLACEHOLDER_PATTERN, (_, key: string) => {
    if (!(key in fixture)) {
      throw new Error(`resolveLivePath: missing fixture key "${key}" for "${livePath}"`);
    }
    return encodeURIComponent(fixture[key]);
  });
}

function collectPlaceholders(livePath: string): string[] {
  const out: string[] = [];
  for (const match of livePath.matchAll(PLACEHOLDER_PATTERN)) {
    out.push(match[1]);
  }
  return out;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function pickString(value: unknown, field: string, source: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`${source}: ${field} must be a non-empty string`);
  }
  return value;
}

function pickNumber(value: unknown, field: string, source: string): number | undefined {
  if (value === undefined) return undefined;
  if (typeof value !== 'number' || Number.isNaN(value)) {
    throw new Error(`${source}: ${field} must be a number`);
  }
  if (value < 0 || value > 1) {
    throw new Error(`${source}: ${field} ${value} out of range [0, 1]`);
  }
  return value;
}

function pickStringArray(value: unknown, field: string, source: string): string[] {
  if (!Array.isArray(value)) {
    throw new Error(`${source}: ${field} must be an array`);
  }
  return value.map((entry, i) => pickString(entry, `${field}[${i}]`, source));
}

function pickRecord(
  value: unknown,
  field: string,
  source: string
): Record<string, string> | undefined {
  if (value === undefined) return undefined;
  if (!isPlainObject(value)) {
    throw new Error(`${source}: ${field} must be an object`);
  }
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(value)) {
    if (typeof v !== 'string') {
      throw new Error(`${source}: ${field}.${k} must be a string`);
    }
    out[k] = v;
  }
  return out;
}

function pickViewports(value: unknown, field: string, source: string): Viewport[] | undefined {
  if (value === undefined) return undefined;
  if (!Array.isArray(value) || value.length === 0) {
    throw new Error(`${source}: ${field} must be a non-empty array`);
  }
  return value.map((entry, i) => {
    const ctx = `${field}[${i}]`;
    if (!isPlainObject(entry)) {
      throw new Error(`${source}: ${ctx} must be an object`);
    }
    const name = pickString(entry.name, `${ctx}.name`, source);
    if (!ID_PATTERN.test(name)) {
      throw new Error(`${source}: ${ctx}.name "${name}" must match ${ID_PATTERN}`);
    }
    const width = pickInt(entry.width, `${ctx}.width`, source, 320, 3840);
    const height = pickInt(entry.height, `${ctx}.height`, source, 240, 2160);
    return { name, width, height };
  });
}

function pickInt(value: unknown, field: string, source: string, min: number, max: number): number {
  if (typeof value !== 'number' || !Number.isInteger(value)) {
    throw new Error(`${source}: ${field} must be an integer`);
  }
  if (value < min || value > max) {
    throw new Error(`${source}: ${field} ${value} out of range [${min}, ${max}]`);
  }
  return value;
}
