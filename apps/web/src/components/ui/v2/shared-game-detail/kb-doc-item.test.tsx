/**
 * Wave A.4 (Issue #603) — KbDocItem rendering tests.
 *
 * Verifies the indexed knowledge-base document row contract from spec §3.5:
 *  - Title + kind/language/totalChunks/indexedAt badges
 *  - kind defaults to 'pdf' when omitted; KIND_ICON map renders correct emoji
 *  - openHref present → enabled <a> with aria-label
 *  - openHref absent → disabled <span aria-disabled="true">
 *  - data-slot + data-kb-id + data-kb-kind attributes
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { KbDocItem, type KbDocKind } from './kb-doc-item';

const labels = {
  indexedPrefix: 'Indexed',
  chunksLabel: 'chunks',
  openLabel: 'Apri ↗',
  openAriaLabel: (title: string) => `Open KB ${title}`,
};

describe('KbDocItem (Wave A.4)', () => {
  it('renders title and badges (kind/language/chunks)', () => {
    render(
      <KbDocItem
        id="k1"
        title="Rulebook EN"
        kind="pdf"
        language="en"
        totalChunks={123}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { level: 3, name: 'Rulebook EN' })).toBeInTheDocument();
    expect(screen.getByText('PDF')).toBeInTheDocument();
    expect(screen.getByText('EN')).toBeInTheDocument();
    expect(screen.getByText('123 chunks')).toBeInTheDocument();
    expect(screen.getByText('Indexed', { exact: false })).toBeInTheDocument();
  });

  it('renders <time> element with dateTime attribute', () => {
    const { container } = render(
      <KbDocItem
        id="k1"
        title="Rulebook EN"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const time = container.querySelector('time');
    expect(time).toHaveAttribute('datetime', '2026-04-15T00:00:00Z');
  });

  it.each<[KbDocKind, string]>([
    ['pdf', '📄'],
    ['md', '📝'],
    ['url', '🔗'],
  ])('renders correct icon for kind=%s', (kind, expectedIcon) => {
    render(
      <KbDocItem
        id="k1"
        title="Doc"
        kind={kind}
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(screen.getByText(expectedIcon)).toBeInTheDocument();
  });

  it('defaults kind to "pdf" when omitted', () => {
    const { container } = render(
      <KbDocItem
        id="k1"
        title="Doc"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-kb-item"]');
    expect(root).toHaveAttribute('data-kb-kind', 'pdf');
    // PDF emoji rendered
    expect(screen.getByText('📄')).toBeInTheDocument();
  });

  it('renders enabled link when openHref is provided', () => {
    render(
      <KbDocItem
        id="k1"
        title="Rulebook"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        openHref="/kb/k1"
        labels={labels}
      />
    );
    const link = screen.getByRole('link', { name: 'Open KB Rulebook' });
    expect(link).toHaveAttribute('href', '/kb/k1');
  });

  it('renders disabled span fallback when openHref is omitted', () => {
    const { container } = render(
      <KbDocItem
        id="k1"
        title="Rulebook"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(container.querySelector('a')).toBeNull();
    const disabled = container.querySelector('[aria-disabled="true"]');
    expect(disabled?.textContent).toBe('Apri ↗');
  });

  it('uppercases language and kind in badges', () => {
    render(
      <KbDocItem
        id="k1"
        title="Rulebook"
        kind="md"
        language="it"
        totalChunks={5}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(screen.getByText('MD')).toBeInTheDocument();
    expect(screen.getByText('IT')).toBeInTheDocument();
  });

  it('exposes data-slot and data-kb-id attributes', () => {
    const { container } = render(
      <KbDocItem
        id="kb-xyz"
        title="Rulebook"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-kb-item"]');
    expect(root).toHaveAttribute('data-kb-id', 'kb-xyz');
  });

  it('passes through optional className', () => {
    const { container } = render(
      <KbDocItem
        id="k1"
        title="Rulebook"
        language="en"
        totalChunks={1}
        indexedAt="2026-04-15T00:00:00Z"
        labels={labels}
        className="custom-cls"
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-kb-item"]');
    expect(root?.className).toContain('custom-cls');
  });
});
