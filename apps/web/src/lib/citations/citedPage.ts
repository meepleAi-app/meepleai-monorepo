/**
 * #4819 — the page a citation names, or `null` when the server could not vouch for one.
 *
 * A document indexed without the page of each passage claims the same page for every passage, so the
 * server leaves the page out rather than send one that is wrong (#4782). Everything that writes a page
 * next to a citation goes through here: the contract is «no page», not «page 0».
 *
 * `null`, `undefined` and anything that is not a positive whole number all mean «no page». A server
 * that still sends 0 for it (some paths wrote `page ?? 0`) must not make a «p.0» appear either.
 */
export function citedPage(page: number | null | undefined): number | null {
  return typeof page === 'number' && Number.isInteger(page) && page > 0 ? page : null;
}

/** The download address of a citation's PDF, opened at the cited page when there is one. */
export function pdfDownloadHref(pdfId: string, page: number | null | undefined): string {
  const base = `/api/v1/pdfs/${pdfId}/download`;
  const cited = citedPage(page);
  return cited === null ? base : `${base}#page=${cited}`;
}
