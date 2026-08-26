/**
 * Classifica gli esiti della passata del crawler e aggiorna il tracker.
 *
 * Un error boundary React risponde HTTP 200: senza il controllo sui marker
 * testuali una pagina completamente rotta risulterebbe sana. Per lo stesso
 * motivo gli errori di console e le richieste secondarie fallite non sono
 * ignorati, ma nemmeno equiparati a un guasto: diventano 'sospetto', da
 * triagare a mano.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

export type CrawlEntry = {
  id: string;
  route: string;
  url?: string;
  role: string;
  status: number;
  consoleErrors: string[];
  failedRequests: string[];
  bodyMarkers: string[];
  screenshot?: string;
};

export type Verdict = 'ok' | 'sospetto' | 'rotto';

export function classify(e: CrawlEntry): Verdict {
  if (e.status >= 400 || e.status === 0 || e.bodyMarkers.length > 0) return 'rotto';
  if (e.consoleErrors.length > 0 || e.failedRequests.length > 0) return 'sospetto';
  return 'ok';
}

const STATUS_BY_VERDICT: Record<Verdict, string> = {
  ok: '✅ verificato',
  sospetto: '⚠️ finding da triagare',
  rotto: '⚠️ finding da aprire',
};

/** Riscrive la colonna `stato` delle sole righe che il crawl ha toccato. */
export function applyStatuses(csv: string, entries: CrawlEntry[]): string {
  const verdicts = new Map(entries.map(e => [e.id, classify(e)]));
  const [header, ...lines] = csv.trim().split('\n');

  const updated = lines.map(line => {
    const cells = line.split(',');
    const verdict = verdicts.get(cells[0]);
    if (!verdict) return line;
    cells[7] = STATUS_BY_VERDICT[verdict];
    return cells.join(',');
  });

  return [header, ...updated].join('\n') + '\n';
}

export function renderMarkdown(entries: CrawlEntry[]): string {
  const byVerdict: Record<Verdict, number> = { ok: 0, sospetto: 0, rotto: 0 };
  const problems: string[] = [];

  for (const e of entries) {
    const verdict = classify(e);
    byVerdict[verdict] += 1;
    if (verdict === 'ok') continue;

    // Il segnale va su una riga di tabella: i messaggi di console sono spesso
    // multi-riga e lunghi centinaia di caratteri, e sfondano il markdown.
    const signal =
      [...e.bodyMarkers, ...e.failedRequests, ...e.consoleErrors]
        .slice(0, 2)
        .map(s => s.replace(/\s+/g, ' ').slice(0, 110))
        .join(' · ') || '—';
    problems.push(`| \`${e.route}\` | ${e.role} | ${verdict} | ${e.status} | ${signal} |`);
  }

  return [
    '# Passata del crawler',
    '',
    `Rotte visitate: ${entries.length} — ok: ${byVerdict.ok} · sospetto: ${byVerdict.sospetto} · rotto: ${byVerdict.rotto}`,
    '',
    '| Rotta | Ruolo | Esito | HTTP | Segnale |',
    '|---|---|---|---|---|',
    ...problems,
    '',
  ].join('\n');
}
