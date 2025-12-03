import fs from 'node:fs/promises';
import path from 'node:path';
import { type NextRequest, NextResponse } from 'next/server';

const dataRoot = process.env.SCRAPER_OUTPUT_DIR ?? path.resolve(process.cwd(), '../../data');

type ParamShape = { id: string } | Promise<{ id: string }>;

export async function GET(_: NextRequest, { params }: { params: ParamShape }) {
  const resolved = params instanceof Promise ? await params : params;
  const gameId = resolved.id;
  if (!dataRoot) {
    return NextResponse.json({ error: 'SCRAPER_OUTPUT_DIR not configured' }, { status: 500 });
  }
  const qaPath = path.join(dataRoot, 'rulebooks', 'qa', `${gameId}.jsonl`);
  try {
    const content = await fs.readFile(qaPath, 'utf8');
    const items = content
      .split(/\r?\n/)
      .filter(Boolean)
      .map(line => JSON.parse(line));
    return NextResponse.json({ game_id: Number(gameId), count: items.length, items });
  } catch (err: any) {
    if (err.code === 'ENOENT') {
      return NextResponse.json({ error: 'Not found' }, { status: 404 });
    }
    console.error(err);
    return NextResponse.json({ error: 'Server error' }, { status: 500 });
  }
}
