/**
 * knowledge-base — axe AA regression guard for the #2311 FE-3 components.
 *
 * Scans the 5 new domain components in their default states to catch
 * a11y regressions before they ship. The orchestrator page itself
 * (`/knowledge-base/[id]/page.tsx`) is covered by the Storybook
 * `Default` story + the project-wide axe CI sweep.
 */

import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import type { KbChunkDetail, KbChunkSummary, KbDocument } from '@/lib/api/kb-detail-api';

import {
  ChunkSearchBox,
  KbChunkListPanel,
  KbChunkPreview,
  KbHeader,
  MarkdownRenderBlock,
} from '../index';

expect.extend(toHaveNoViolations);

const DOC: KbDocument = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  title: 'Azul rulebook',
  docType: 'rulebook',
  gameId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  gameName: 'Azul',
  uploaderName: 'Marco',
  uploadedAt: '2026-01-12T00:00:00Z',
  lastIngestedAt: '2026-03-08T00:00:00Z',
  processingStatus: 'ready',
  chunkCount: 42,
  pageCount: 24,
  language: 'it',
  tags: [],
  fileSize: 12_345,
  indexerVersion: 'v3',
};

const CHUNK: KbChunkSummary = {
  id: '00000000-0000-4000-8000-000000000001',
  position: 0,
  headingPath: ['Setup'],
  snippet: 'Posiziona le factory display al centro del tavolo.',
  pageNumber: 2,
  vectorId: 'vec-0',
  usedInChats: 4,
};

const CHUNK_DETAIL: KbChunkDetail = {
  id: CHUNK.id,
  docId: DOC.id,
  position: 0,
  headingPath: ['Intro', 'Benvenuto'],
  content: '# Hello\n\nbody text',
  pageNumber: 1,
  prevChunkId: null,
  nextChunkId: null,
  metadata: {},
};

describe('knowledge-base — axe AA gate (#2311 FE-7)', () => {
  it('KbHeader — no violations', async () => {
    const { container } = render(<KbHeader document={DOC} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('MarkdownRenderBlock — no violations on a fixture chunk body', async () => {
    const { container } = render(
      <MarkdownRenderBlock content="# Hello\n\n- one\n- two\n\n| A | B |\n|---|---|\n| 1 | 2 |" />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('ChunkSearchBox — no violations (label association via sr-only)', async () => {
    const { container } = render(<ChunkSearchBox onCommit={() => undefined} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('KbChunkListPanel — no violations with one active and one inactive row', async () => {
    const { container } = render(
      <KbChunkListPanel
        chunks={[CHUNK, { ...CHUNK, id: '00000000-0000-4000-8000-000000000002', position: 1 }]}
        activeChunkId={CHUNK.id}
        onSelect={() => undefined}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('KbChunkPreview — no violations in the ready state with sub-tabs', async () => {
    const { container } = render(<KbChunkPreview state={{ kind: 'ready', chunk: CHUNK_DETAIL }} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
