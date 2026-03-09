/**
 * ChatExtraMeepleCard Story
 * Issue #5067 — Showcase stories: chat + kb entity cards
 *
 * Demonstrates the ChatExtraMeepleCard with configurable status,
 * message count, game context, and agent info.
 */

import { ChatExtraMeepleCard } from '@/components/ui/data-display/extra-meeple-card';
import type { ChatDetailData } from '@/components/ui/data-display/extra-meeple-card';

import type { ShowcaseStory } from '../types';

// ── Showcase prop shape (control-compatible scalars) ──────────────────────────

type ChatStoryProps = {
  chatStatus: string;
  messageCount: number;
  hasGame: boolean;
  hasAgent: boolean;
  loading: boolean;
};

// ── Sample messages for the preview ──────────────────────────────────────────

const SAMPLE_MESSAGES: ChatDetailData['messages'] = [
  {
    id: 'msg-1',
    role: 'user',
    content: 'Come funziona il sistema di commercio in Catan?',
    createdAt: new Date(Date.now() - 12 * 60_000).toISOString(),
  },
  {
    id: 'msg-2',
    role: 'assistant',
    content:
      'In Catan puoi commerciare con altri giocatori durante il tuo turno oppure con la banca al rapporto 4:1 (o migliore se controlli un porto). I porti con rapporto 2:1 accettano solo un tipo di risorsa specifico.',
    createdAt: new Date(Date.now() - 11 * 60_000).toISOString(),
  },
  {
    id: 'msg-3',
    role: 'user',
    content: 'Qual è la strategia migliore per vincere?',
    createdAt: new Date(Date.now() - 8 * 60_000).toISOString(),
  },
  {
    id: 'msg-4',
    role: 'assistant',
    content:
      'Dipende molto dal setup iniziale. In generale: occupare gli incroci ad alta produzione, diversificare le risorse e puntare sui porti è una strategia solida. Espandersi rapidamente verso città aiuta a scalare i punti.',
    createdAt: new Date(Date.now() - 7 * 60_000).toISOString(),
  },
];

// ── Story definition ──────────────────────────────────────────────────────────

export const extraMeepleCardChatStory: ShowcaseStory<ChatStoryProps> = {
  id: 'extra-meeple-card-chat',
  title: 'ChatExtraMeepleCard',
  category: 'Data Display',
  description:
    'Detail card for chat sessions — shows status, messages, and agent/game context across 3 tabs.',

  component: function ChatExtraMeepleCardStory({
    chatStatus,
    messageCount,
    hasGame,
    hasAgent,
    loading,
  }: ChatStoryProps) {
    const data: ChatDetailData = {
      id: 'thread-demo-1',
      status: chatStatus as ChatDetailData['status'],
      startedAt: new Date(Date.now() - 30 * 60_000).toISOString(),
      durationMinutes: 28,
      messageCount,
      messages: SAMPLE_MESSAGES.slice(0, Math.min(messageCount, SAMPLE_MESSAGES.length)),
      ...(hasAgent
        ? {
            agentId: 'agent-catan-expert',
            agentName: 'Catan Expert',
            agentModel: 'claude-sonnet-4-6',
          }
        : {}),
      ...(hasGame
        ? {
            gameId: 'game-catan-uuid',
            gameName: 'Catan',
            gameThumbnailUrl: undefined,
          }
        : {}),
      temperature: 0.7,
      maxTokens: 1024,
      systemPrompt:
        'Sei un esperto di Catan e rispondi solo a domande relative alle regole e strategie di questo gioco.',
    };

    return (
      <div className="flex items-start justify-center p-6">
        <ChatExtraMeepleCard data={data} loading={loading} />
      </div>
    );
  },

  defaultProps: {
    chatStatus: 'active',
    messageCount: 4,
    hasGame: true,
    hasAgent: true,
    loading: false,
  },

  controls: {
    chatStatus: {
      type: 'select',
      label: 'chatStatus',
      options: ['active', 'waiting', 'archived', 'closed'],
      default: 'active',
    },
    messageCount: {
      type: 'range',
      label: 'messageCount',
      min: 0,
      max: 4,
      step: 1,
      default: 4,
    },
    hasGame: {
      type: 'boolean',
      label: 'hasGame',
      default: true,
    },
    hasAgent: {
      type: 'boolean',
      label: 'hasAgent',
      default: true,
    },
    loading: {
      type: 'boolean',
      label: 'loading',
      default: false,
    },
  },

  presets: {
    withGame: {
      label: 'Chat attiva con gioco',
      props: {
        chatStatus: 'active',
        messageCount: 4,
        hasGame: true,
        hasAgent: true,
        loading: false,
      },
    },
    archived: {
      label: 'Chat archiviata senza gioco',
      props: {
        chatStatus: 'archived',
        messageCount: 2,
        hasGame: false,
        hasAgent: false,
        loading: false,
      },
    },
    loading: {
      label: 'Caricamento',
      props: { loading: true },
    },
  },
};
