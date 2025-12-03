import { NextResponse } from 'next/server';

const ITEM_REGEX = /<item[^>]*id="(\d+)"[^>]*>([\s\S]*?)<\/item>/g;
const NAME_REGEX = /<name[^>]*value="([^"]+)"/i;
const YEAR_REGEX = /<yearpublished[^>]*value="(\d{4})"/i;

const HTML_CARD_REGEX = /data-objectid="(\d+)"[\s\S]*?primary"[^>]*>([^<]+)<\/a>[\s\S]*?(\d{4})?/g;

export async function GET(req: Request) {
  const { searchParams } = new URL(req.url);
  const query = searchParams.get('q')?.trim();
  if (!query) return NextResponse.json({ error: 'Missing q' }, { status: 400 });
  const token = process.env.BGG_TOKEN;
  const url = `https://boardgamegeek.com/xmlapi2/search?query=${encodeURIComponent(query)}&type=boardgame`;
  let xml: string | undefined;
  let usedFallback = false;

  try {
    const res = await fetch(url, {
      cache: 'no-store',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (res.ok) {
      xml = await res.text();
    } else if (res.status === 401 || res.status === 403) {
      usedFallback = true;
    } else {
      return NextResponse.json({ error: 'BGG error' }, { status: 502 });
    }
  } catch {
    usedFallback = true;
  }

  if (!usedFallback && xml) {
    const items: { id: number; name: string; year?: number }[] = [];
    let match: RegExpExecArray | null;
    while ((match = ITEM_REGEX.exec(xml)) !== null) {
      const [, idStr, inner] = match;
      const id = Number(idStr);
      const nameMatch = NAME_REGEX.exec(inner);
      const yearMatch = YEAR_REGEX.exec(inner);
      items.push({
        id,
        name: nameMatch?.[1] ?? '',
        year: yearMatch ? Number(yearMatch[1]) : undefined,
      });
      if (items.length >= 20) break;
    }
    return NextResponse.json({ count: items.length, items, source: 'xml' });
  }

  // Fallback: scrape HTML search page (unauthenticated)
  const htmlUrl = `https://boardgamegeek.com/search/boardgame?q=${encodeURIComponent(query)}`;
  const resHtml = await fetch(htmlUrl, { cache: 'no-store' });
  if (!resHtml.ok) return NextResponse.json({ error: 'BGG search unavailable' }, { status: 502 });
  const html = await resHtml.text();
  const items: { id: number; name: string; year?: number }[] = [];
  let match: RegExpExecArray | null;
  while ((match = HTML_CARD_REGEX.exec(html)) !== null) {
    const [, idStr, nameRaw, yearRaw] = match;
    const id = Number(idStr);
    const name = nameRaw?.trim() ?? '';
    const year = yearRaw && /^\d{4}$/.test(yearRaw) ? Number(yearRaw) : undefined;
    items.push({ id, name, year });
    if (items.length >= 20) break;
  }
  return NextResponse.json({ count: items.length, items, source: 'html' });
}
