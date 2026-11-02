/**
 * CitationBlock — hook stabile per contare le citazioni (#4120).
 *
 * Il componente non aveva alcun test. Il bottone di ogni chip porta `citation-chip-${i}`, che
 * indirizza UNA chip e non serve a contarle; `e2e/document-source-filtering.spec.ts` cercava
 * `[data-testid="citation"]` da sempre senza trovarlo, e si reggeva su un `.or()` di fallback sul
 * testo — fragile, perché Playwright vede l'inglese e non il locale predefinito.
 *
 * Questi test fissano il contratto che la spec consuma: un nodo `citation` per citazione VISIBILE.
 */
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CitationBlock } from '../CitationBlock';

type Snippet = Parameters<typeof CitationBlock>[0]['snippets'][number];

function snippet(page: number, overrides: Partial<Snippet> = {}): Snippet {
  return {
    text: `Testo di pagina ${page}`,
    source: `PDF:doc-${page}`,
    page,
    line: 1,
    score: 0.9,
    ...overrides,
  };
}

describe('CitationBlock — data-testid="citation" (#4120)', () => {
  it('rende un nodo `citation` per ogni citazione', () => {
    const { container } = render(
      <CitationBlock snippets={[snippet(10), snippet(25)]} excludeIndices={new Set()} />
    );
    expect(container.querySelectorAll('[data-testid="citation"]')).toHaveLength(2);
  });

  it('non conta le citazioni escluse', () => {
    // `excludeIndices` e` il meccanismo che la spec di filtraggio per documento esercita: il
    // conteggio dei nodi `citation` deve seguirlo, altrimenti il test del filtro non misura nulla.
    const { container } = render(
      <CitationBlock
        snippets={[snippet(10), snippet(25), snippet(40)]}
        excludeIndices={new Set([1])}
      />
    );
    expect(container.querySelectorAll('[data-testid="citation"]')).toHaveLength(2);
  });

  it('non rende nulla quando tutte le citazioni sono escluse', () => {
    const { container } = render(
      <CitationBlock snippets={[snippet(10)]} excludeIndices={new Set([0])} />
    );
    expect(container.querySelector('[data-testid="citation"]')).toBeNull();
    expect(container.querySelector('[data-testid="citation-block"]')).toBeNull();
  });

  it('il nodo `citation` contiene la chip con il numero di pagina', () => {
    // Nota per chi riscrivera` la spec: il testo reale e` «Pagina N», non «Pag. N» —
    // `e2e/chat-citations.spec.ts` asseriva la seconda forma, che non esiste.
    const { container } = render(
      <CitationBlock snippets={[snippet(10)]} excludeIndices={new Set()} />
    );
    const citation = container.querySelector('[data-testid="citation"]');
    expect(citation?.textContent).toContain('Pagina 10');
  });

  it('non rimuove il testid indicizzato della chip, che indirizza una citazione specifica', () => {
    const { container } = render(
      <CitationBlock snippets={[snippet(10), snippet(25)]} excludeIndices={new Set()} />
    );
    expect(container.querySelector('[data-testid="citation-chip-0"]')).toBeTruthy();
    expect(container.querySelector('[data-testid="citation-chip-1"]')).toBeTruthy();
  });

  it('un nodo `citation` per citazione, non uno per blocco', () => {
    // L'invariante che conta per Playwright: `toHaveCount(n)` sulle citazioni deve dare n, non 1.
    const { container } = render(
      <CitationBlock
        snippets={[snippet(1), snippet(2), snippet(3), snippet(4)]}
        excludeIndices={new Set()}
      />
    );
    expect(container.querySelectorAll('[data-testid="citation-block"]')).toHaveLength(1);
    expect(container.querySelectorAll('[data-testid="citation"]')).toHaveLength(4);
  });
});

// ─── #4819: citazione senza pagina ────────────────────────────────────────────

describe('CitationBlock — citazione senza pagina (#4819)', () => {
  it.each([
    ['null', null],
    ['absent', undefined],
    ['zero', 0],
  ])('writes no page and links to the whole PDF when the page is %s', (_label, page) => {
    const { getByTestId } = render(
      <CitationBlock snippets={[snippet(1, { page })]} excludeIndices={new Set()} />
    );

    expect(getByTestId('citation-chip-0').textContent).toBe('Fonte');
    const link = getByTestId('citation-pdf-link-0');
    expect(link.getAttribute('href')).toBe('/api/v1/pdfs/doc-1/download');
    expect(link.getAttribute('title')).toBe('Apri PDF');
  });

  it('still writes the page and opens the PDF there when there is one', () => {
    const { getByTestId } = render(
      <CitationBlock snippets={[snippet(25)]} excludeIndices={new Set()} />
    );

    expect(getByTestId('citation-chip-0').textContent).toBe('Pagina 25');
    expect(getByTestId('citation-pdf-link-0').getAttribute('href')).toBe(
      '/api/v1/pdfs/doc-25/download#page=25'
    );
  });
});
