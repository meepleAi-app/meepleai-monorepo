import { NextResponse } from 'next/server';
import { spawn } from 'node:child_process';
import path from 'node:path';

const ALLOWED = new Set(['game', 'plays', 'qa']);
const API_KEY = process.env.SCRAPER_API_KEY;

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
    if (!Number.isFinite(gameId))
      return NextResponse.json({ error: 'gameId required' }, { status: 400 });

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
    if (action === 'plays' && body.mindate) args.push('--mindate', String(body.mindate));
    if (action === 'qa' && body.rulebook) {
      const rb = String(body.rulebook);
      if (rb.includes('..'))
        return NextResponse.json({ error: 'Invalid rulebook path' }, { status: 400 });
      args.push('--rulebook', rb);
    }
    if (action === 'qa' && body.max) args.push('--max', String(Number(body.max)));

    const { stdout, stderr, code } = await runPnpm(args);
    if (code !== 0) {
      return NextResponse.json({ ok: false, code, stdout, stderr }, { status: 500 });
    }
    return NextResponse.json({ ok: true, stdout, stderr });
  } catch (err: any) {
    return NextResponse.json({ ok: false, error: err?.message ?? 'Server error' }, { status: 500 });
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
