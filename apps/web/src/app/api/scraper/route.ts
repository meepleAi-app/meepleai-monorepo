import { NextResponse } from 'next/server';
import { spawn } from 'node:child_process';
import path from 'node:path';

const ALLOWED = new Set(['game', 'plays', 'qa']);
const API_KEY = process.env.SCRAPER_API_KEY;

// Date format regex: YYYY-MM-DD
const DATE_REGEX = /^\d{4}-\d{2}-\d{2}$/;

// Allowed base directory for rulebooks (relative to project root)
const ALLOWED_RULEBOOK_DIR = 'rulebooks';

/**
 * Validates and sanitizes rulebook path to prevent path traversal.
 * Returns null if path is invalid.
 */
function sanitizeRulebookPath(input: string): string | null {
  // Reject empty or null inputs
  if (!input || typeof input !== 'string') return null;

  // Reject URL-encoded traversal attempts
  const decoded = decodeURIComponent(input);

  // Reject path traversal patterns (including encoded and backslash)
  if (decoded.includes('..') || decoded.includes('\\..') || decoded.includes('/..')) {
    return null;
  }

  // Reject absolute paths
  if (path.isAbsolute(decoded)) return null;

  // Normalize and resolve the path
  const normalized = path.normalize(decoded);

  // Double-check after normalization for traversal
  if (normalized.includes('..')) return null;

  // Ensure the path starts with allowed directory or is within it
  if (
    !normalized.startsWith(ALLOWED_RULEBOOK_DIR + path.sep) &&
    !normalized.startsWith(ALLOWED_RULEBOOK_DIR + '/') &&
    normalized !== ALLOWED_RULEBOOK_DIR
  ) {
    // Allow paths that don't have directory prefix if they're simple filenames
    // but prepend the allowed directory
    if (!normalized.includes(path.sep) && !normalized.includes('/')) {
      return path.join(ALLOWED_RULEBOOK_DIR, normalized);
    }
    return null;
  }

  return normalized;
}

/**
 * Validates date string is in YYYY-MM-DD format and is a valid date.
 */
function isValidDate(dateStr: string): boolean {
  if (!DATE_REGEX.test(dateStr)) return false;

  // Verify it's a valid calendar date
  const [year, month, day] = dateStr.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
}

export async function POST(req: Request) {
  try {
    if (API_KEY) {
      const provided = req.headers.get('x-api-key');
      if (provided !== API_KEY)
        return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const body = await req.json();
    const action = String(body.action ?? '');
    if (!ALLOWED.has(action))
      return NextResponse.json({ error: 'Invalid action' }, { status: 400 });
    const gameId = Number(body.gameId);
    if (!Number.isFinite(gameId) || gameId <= 0)
      return NextResponse.json({ error: 'Valid gameId required' }, { status: 400 });

    // Build safe args
    const args = [
      '--dir',
      '../../tools/game-scraper',
      'run',
      `scraper:${action}`,
      '--',
      '--game-id',
      String(gameId),
    ];

    // Validate and add mindate if provided
    if (action === 'plays' && body.mindate) {
      const dateStr = String(body.mindate);
      if (!isValidDate(dateStr)) {
        return NextResponse.json(
          { error: 'Invalid mindate format. Use YYYY-MM-DD' },
          { status: 400 }
        );
      }
      args.push('--mindate', dateStr);
    }

    // Validate and add rulebook path if provided
    if (action === 'qa' && body.rulebook) {
      const sanitizedPath = sanitizeRulebookPath(String(body.rulebook));
      if (!sanitizedPath) {
        return NextResponse.json({ error: 'Invalid rulebook path' }, { status: 400 });
      }
      args.push('--rulebook', sanitizedPath);
    }

    // Validate max parameter
    if (action === 'qa' && body.max) {
      const maxNum = Number(body.max);
      if (!Number.isFinite(maxNum) || maxNum < 1) {
        return NextResponse.json({ error: 'Invalid max value' }, { status: 400 });
      }
      args.push('--max', String(Math.floor(maxNum)));
    }

    const { stdout, stderr, code } = await runPnpm(args);
    if (code !== 0) {
      return NextResponse.json({ ok: false, code, stdout, stderr }, { status: 500 });
    }
    return NextResponse.json({ ok: true, stdout, stderr });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Server error';
    return NextResponse.json({ ok: false, error: message }, { status: 500 });
  }
}

function runPnpm(args: string[]): Promise<{ stdout: string; stderr: string; code: number | null }> {
  return new Promise((resolve, reject) => {
    const child = spawn('pnpm', args, { cwd: process.cwd(), shell: false });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', d => (stdout += d.toString()));
    child.stderr.on('data', d => (stderr += d.toString()));
    child.on('error', reject);
    child.on('close', code => resolve({ stdout, stderr, code }));
  });
}
