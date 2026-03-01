/**
 * KbExtraMeepleCard Story
 * Issue #5067 — Showcase stories: chat + kb entity cards
 *
 * Demonstrates the KbExtraMeepleCard with configurable indexing status,
 * file metadata, and optional game context across 3 tabs.
 */

import { KbExtraMeepleCard } from '@/components/ui/data-display/extra-meeple-card';
import type { KbDetailData } from '@/components/ui/data-display/extra-meeple-card';
import type { ShowcaseStory } from '../types';

// ── Showcase prop shape (control-compatible scalars) ──────────────────────────

type KbStoryProps = {
  documentStatus: string;
  fileSizeKb: number;
  hasGame: boolean;
  loading: boolean;
};

// ── Story definition ──────────────────────────────────────────────────────────

export const extraMeepleCardKbStory: ShowcaseStory<KbStoryProps> = {
  id: 'extra-meeple-card-kb',
  title: 'KbExtraMeepleCard',
  category: 'Data Display',
  description:
    'Detail card for KB documents — shows indexing status, extracted content, and processing timeline across 3 tabs.',

  component: function KbExtraMeepleCardStory({
    documentStatus,
    fileSizeKb,
    hasGame,
    loading,
  }: KbStoryProps) {
    const status = documentStatus as KbDetailData['status'];

    const data: KbDetailData = {
      id: 'doc-demo-1',
      fileName: 'Catan_Regolamento_IT.pdf',
      status,
      fileSize: fileSizeKb * 1024,
      pageCount: 24,
      characterCount: 18_400,
      uploadedAt: new Date(Date.now() - 2 * 24 * 60 * 60_000).toISOString(),
      processedAt:
        status === 'indexed'
          ? new Date(Date.now() - 1 * 24 * 60 * 60_000).toISOString()
          : undefined,
      errorMessage:
        status === 'failed'
          ? 'OCR fallito: il PDF contiene solo immagini scansionate senza testo estraibile.'
          : undefined,
      extractedContent:
        status === 'indexed'
          ? `Catan è un gioco da tavolo per 3-4 giocatori (espandibile a 5-6) ideato da Klaus Teuber.\n\nObiettivo: essere il primo a raggiungere 10 punti vittoria costruendo insediamenti, città e strade sull'isola di Catan.\n\nSetup: posiziona le tessere esagonali in modo casuale o usa la configurazione suggerita per principianti. Distribuisci le risorse iniziali agli insediamenti di partenza.\n\nTurno: lancia i dadi → incassa risorse → commercia → costruisci. Il numero 7 attiva il brigante.`
          : undefined,
      hasMoreContent: status === 'indexed',
      ...(hasGame
        ? {
            gameId: 'game-catan-uuid',
            gameName: 'Catan',
            gameThumbnailUrl: undefined,
          }
        : {}),
    };

    return (
      <div className="flex items-start justify-center p-6">
        <KbExtraMeepleCard
          data={data}
          loading={loading}
          onDelete={() => { /* showcase: no-op */ }}
          onRetryIndexing={status === 'failed' ? () => { /* showcase: no-op */ } : undefined}
        />
      </div>
    );
  },

  defaultProps: {
    documentStatus: 'indexed',
    fileSizeKb: 384,
    hasGame: true,
    loading: false,
  },

  controls: {
    documentStatus: {
      type: 'select',
      label: 'documentStatus',
      options: ['indexed', 'processing', 'failed', 'none'],
      default: 'indexed',
    },
    fileSizeKb: {
      type: 'range',
      label: 'fileSizeKb',
      min: 50,
      max: 5000,
      step: 50,
      default: 384,
    },
    hasGame: {
      type: 'boolean',
      label: 'hasGame',
      default: true,
    },
    loading: {
      type: 'boolean',
      label: 'loading',
      default: false,
    },
  },

  presets: {
    indexed: {
      label: 'Documento indicizzato',
      props: {
        documentStatus: 'indexed',
        fileSizeKb: 384,
        hasGame: true,
        loading: false,
      },
    },
    processing: {
      label: 'Documento in elaborazione',
      props: {
        documentStatus: 'processing',
        fileSizeKb: 1200,
        hasGame: true,
        loading: false,
      },
    },
    failed: {
      label: 'Indicizzazione fallita',
      props: {
        documentStatus: 'failed',
        fileSizeKb: 250,
        hasGame: false,
        loading: false,
      },
    },
    loading: {
      label: 'Caricamento',
      props: { loading: true },
    },
  },
};
