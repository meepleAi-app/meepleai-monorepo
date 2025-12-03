import fs from 'node:fs/promises';
import path from 'node:path';
import { NextResponse } from 'next/server';

const dataRoot = process.env.SCRAPER_OUTPUT_DIR ?? path.resolve(process.cwd(), '../../data');

export async function GET(_: Request, { params }: { params: { id: string } }) {
  const gameId = params.id;
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
