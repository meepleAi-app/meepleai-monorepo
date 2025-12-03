import { NextResponse } from 'next/server';
import { exec as rawExec } from 'node:child_process';
import { promisify } from 'node:util';

const exec = promisify(rawExec);
const ALLOWED = new Set(['game', 'plays', 'qa']);

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const action = String(body.action ?? '');
    if (!ALLOWED.has(action))
      return NextResponse.json({ error: 'Invalid action' }, { status: 400 });
    const gameId = Number(body.gameId);
    if (!Number.isFinite(gameId))
      return NextResponse.json({ error: 'gameId required' }, { status: 400 });

    const args = [
      `pnpm --dir ../../tools/game-scraper run scraper:${action} -- --game-id ${gameId}`,
    ];
    if (body.mindate && action === 'plays') args.push(`--mindate ${body.mindate}`);
    if (body.rulebook && action === 'qa') args.push(`--rulebook ${body.rulebook}`);
    if (body.max && action === 'qa') args.push(`--max ${Number(body.max)}`);

    const command = args.join(' ');
    const { stdout, stderr } = await exec(command, { cwd: process.cwd(), timeout: 5 * 60 * 1000 });
    return NextResponse.json({ ok: true, command, stdout, stderr });
  } catch (err: any) {
    return NextResponse.json({ ok: false, error: err?.message ?? 'Server error' }, { status: 500 });
  }
}
