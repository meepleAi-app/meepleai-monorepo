/**
 * sp4-kb-detail — DS-17-13 #2220 sub-issue + #2311 FE-6 signoff.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-kb-detail.{html,jsx}`.
 * design_intent: forward-refactor (G4 v3 pivot deferred per MOCKUPS_INDEX).
 * Designer review tracking: CLOSED in #2223, superseded by #2311 iteration.
 *
 * Stories cover the 4-state FSM of the split-view orchestrator:
 *   - Default — document + chunks load, first chunk auto-selected
 *   - Loading — document fetch in flight (skeleton)
 *   - EmptyChunks — document landed but chunks list is empty
 *   - NotFound — document fetch returned 404
 */

import { http, HttpResponse } from 'msw';

import KnowledgeBaseDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const DOC_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CHUNK_ID = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

const DOC_FIXTURE = {
  id: DOC_ID,
  title: 'Azul · Manuale ufficiale',
  docType: 'rulebook',
  gameId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  gameName: 'Azul',
  uploaderName: 'Marco',
  uploadedAt: '2026-01-12T00:00:00Z',
  lastIngestedAt: '2026-03-08T00:00:00Z',
  processingStatus: 'ready',
  chunkCount: 3,
  pageCount: 24,
  language: 'it',
  tags: [],
  fileSize: 145_312,
  indexerVersion: 'v3',
};

const CHUNKS_FIXTURE = {
  items: [
    {
      id: CHUNK_ID,
      position: 0,
      headingPath: ['Introduzione'],
      snippet:
        'Benvenuto in Azul. Un gioco da tavolo per 2-4 giocatori in cui gli artigiani decorano le pareti del Palazzo Reale di Évora.',
      pageNumber: 1,
      vectorId: CHUNK_ID.replace(/-/g, ''),
      usedInChats: 8,
    },
    {
      id: '00000000-0000-4000-8000-000000000002',
      position: 1,
      headingPath: ['Setup', 'Preparazione del tavolo'],
      snippet:
        'Posiziona le 5, 7 o 9 factory display al centro del tavolo a seconda del numero di giocatori.',
      pageNumber: 2,
      vectorId: '00000000000040008000000000000002',
      usedInChats: 12,
    },
    {
      id: '00000000-0000-4000-8000-000000000003',
      position: 2,
      headingPath: ['Turno di gioco'],
      snippet:
        'Nel proprio turno il giocatore sceglie una factory display e prende tutte le tessere di un colore.',
      pageNumber: 3,
      vectorId: '00000000000040008000000000000003',
      usedInChats: 0,
    },
  ],
  nextCursor: null,
  totalCount: 3,
};

const CHUNK_DETAIL_FIXTURE = {
  id: CHUNK_ID,
  docId: DOC_ID,
  position: 0,
  headingPath: ['Introduzione'],
  content: `# Benvenuto in Azul

Azul è un gioco da tavolo per **2-4 giocatori** in cui gli artigiani decorano le pareti
del Palazzo Reale di Évora con tessere ispirate alle piastrelle *azulejos* portoghesi.

## Obiettivo

Accumulare il maggior numero di punti vittoria completando righe, colonne e set di
colore sul proprio tabellone personale entro la fine della partita.

## Durata

Una partita standard dura **30-45 minuti**. La fine è innescata quando un giocatore
completa una riga orizzontale del proprio muro.`,
  pageNumber: 1,
  prevChunkId: null,
  nextChunkId: '00000000-0000-4000-8000-000000000002',
  metadata: {},
};

function defaultHandlers() {
  return [
    http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, () => HttpResponse.json(DOC_FIXTURE)),
    http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks`, () =>
      HttpResponse.json(CHUNKS_FIXTURE)
    ),
    http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks/${CHUNK_ID}`, () =>
      HttpResponse.json(CHUNK_DETAIL_FIXTURE)
    ),
    http.post(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks/search`, () =>
      HttpResponse.json({ matches: [], totalCount: 0, skip: 0, take: 20 })
    ),
  ];
}

const meta: Meta<typeof KnowledgeBaseDetailPage> = {
  title: 'Authenticated / sp4-kb-detail',
  component: KnowledgeBaseDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/knowledge-base/${DOC_ID}`,
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2311 FE-6. KB document detail surface (split-view chunks list + Markdown preview). DEC-D1 keeps the canonical route /knowledge-base/[id]; /kb/[id] is a server redirect.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof KnowledgeBaseDetailPage>;

export const Default: Story = {
  parameters: {
    msw: { handlers: defaultHandlers() },
  },
};

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, () => new Promise(() => {}))],
    },
  },
};

export const EmptyChunks: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, () => HttpResponse.json(DOC_FIXTURE)),
        http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks`, () =>
          HttpResponse.json({ items: [], nextCursor: null, totalCount: 0 })
        ),
      ],
    },
  },
};

export const NotFound: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, () =>
          HttpResponse.json({ error: 'not found' }, { status: 404 })
        ),
      ],
    },
  },
};
