/**
 * RuleSourceCard Story
 * Issue #5528: Showcase story for RuleSourceCard component.
 */

import { RuleSourceCard, type RuleSourceCardProps } from '@/components/chat-unified/RuleSourceCard';
import type { Citation } from '@/types';

import type { ShowcaseStory } from '../types';

// ─── Sample Data ─────────────────────────────────────────────────────────────

const singleCitation: Citation[] = [
  {
    documentId: 'doc-catan-001',
    pageNumber: 12,
    snippet:
      'Quando un giocatore costruisce un insediamento su un incrocio adiacente a un terreno con il numero uscito, riceve la risorsa corrispondente.',
    relevanceScore: 0.92,
  },
];

const multiCitations: Citation[] = [
  {
    documentId: 'doc-catan-001',
    pageNumber: 12,
    snippet:
      'Quando un giocatore costruisce un insediamento su un incrocio adiacente a un terreno con il numero uscito, riceve la risorsa corrispondente.',
    relevanceScore: 0.92,
  },
  {
    documentId: 'doc-catan-001',
    pageNumber: 23,
    snippet:
      'Il ladro viene spostato quando un giocatore tira un 7. Il giocatore sceglie un terreno e ruba una risorsa.',
    relevanceScore: 0.67,
  },
  {
    documentId: 'doc-catan-001',
    pageNumber: 45,
    snippet:
      'Per vincere la partita, un giocatore deve raggiungere 10 punti vittoria durante il proprio turno.',
    relevanceScore: 0.41,
  },
];

const highRelevanceCitations: Citation[] = [
  {
    documentId: 'doc-catan-001',
    pageNumber: 5,
    snippet: "All'inizio del gioco, ogni giocatore piazza due insediamenti e due strade.",
    relevanceScore: 0.98,
  },
  {
    documentId: 'doc-catan-001',
    pageNumber: 8,
    snippet: 'Le risorse vengono distribuite a ogni lancio di dado, eccetto quando esce il 7.',
    relevanceScore: 0.88,
  },
];

// ─── Story Type ──────────────────────────────────────────────────────────────

type RuleSourceCardShowcaseProps = {
  mode: string;
  gameTitle: string;
  publisherUrl: string;
  citationSet: string;
};

// ─── Story ───────────────────────────────────────────────────────────────────

export const ruleSourceCardStory: ShowcaseStory<RuleSourceCardShowcaseProps> = {
  id: 'rule-source-card',
  title: 'RuleSourceCard',
  category: 'Feedback',
  description: 'Citation card per risposte RAG con fonti dal regolamento.',

  component: function RuleSourceCardStory({
    mode,
    gameTitle,
    publisherUrl,
    citationSet,
  }: RuleSourceCardShowcaseProps) {
    const citations =
      citationSet === 'single'
        ? singleCitation
        : citationSet === 'high-relevance'
          ? highRelevanceCitations
          : multiCitations;

    return (
      <div className="max-w-lg space-y-4">
        <RuleSourceCard
          citations={citations}
          gameTitle={gameTitle}
          publisherUrl={publisherUrl || undefined}
          mode={mode as RuleSourceCardProps['mode']}
        />
        <p className="text-xs text-muted-foreground">
          Mode: <strong>{mode}</strong> — Citations: <strong>{citations.length}</strong>
        </p>
      </div>
    );
  },

  defaultProps: {
    mode: 'casual',
    gameTitle: 'Catan',
    publisherUrl: 'https://www.catan.com/understand-catan/game-rules',
    citationSet: 'multiple',
  },

  controls: {
    mode: { type: 'select', options: ['casual', 'power'], default: 'casual', label: 'Mode' },
    gameTitle: { type: 'text', default: 'Catan', label: 'Game Title' },
    publisherUrl: {
      type: 'text',
      default: 'https://www.catan.com/understand-catan/game-rules',
      label: 'Publisher URL',
    },
    citationSet: {
      type: 'select',
      options: ['single', 'multiple', 'high-relevance'],
      default: 'multiple',
      label: 'Citation Set',
    },
  },

  presets: {
    singleCitation: {
      label: 'Single citation',
      props: { citationSet: 'single' },
    },
    multipleCitations: {
      label: 'Multiple citations',
      props: { citationSet: 'multiple' },
    },
    noPublisherUrl: {
      label: 'No publisher URL',
      props: { publisherUrl: '' },
    },
    highRelevance: {
      label: 'High relevance (power)',
      props: { citationSet: 'high-relevance', mode: 'power' },
    },
  },
};
