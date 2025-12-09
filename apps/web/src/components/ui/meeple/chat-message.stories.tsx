import type { Meta, StoryObj } from '@storybook/react';
import { ChatMessage } from './chat-message';

// Fixed timestamps for consistent Chromatic snapshots
const FIXED_TIMESTAMP = new Date('2024-01-15T10:00:00Z');
const FIXED_TIMESTAMPS = {
  twoMinutesAgo: new Date('2024-01-15T09:58:00Z'),
  oneMinuteAgo: new Date('2024-01-15T09:59:00Z'),
  thirtySecondsAgo: new Date('2024-01-15T09:59:30Z'),
  now: new Date('2024-01-15T10:00:00Z'),
};

/**
 * ChatMessage displays user and AI messages with role-based layout,
 * confidence badges, citations, and typing indicators.
 *
 * ## Design Reference
 * - Wireframes: `docs/04-frontend/wireframes-playful-boardroom.md` (Page 4 - Chat AI)
 * - Issue: #1831 (UI-004)
 *
 * ## Features
 * - **Role-based layout**: AI messages on left, user messages on right
 * - **Confidence badges**: Color-coded (green ≥85%, yellow 70-84%, red <70%)
 * - **Citations**: Clickable orange badges with document references
 * - **Typing indicator**: Animated 3-dot loading state
 * - **Avatars**: MeepleAvatar for AI (5 states), standard Avatar for users
 *
 * ## Accessibility
 * - ✅ ARIA labels for all interactive elements
 * - ✅ Keyboard navigation for citations
 * - ✅ Screen reader friendly message structure
 * - ✅ Semantic HTML with role attributes
 */
const meta = {
  title: 'UI/ChatMessage',
  component: ChatMessage,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Message component for chat interfaces with role-based layout, confidence indicators, and citation support.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    role: {
      control: 'select',
      options: ['user', 'assistant'],
      description: 'Message sender role',
      table: {
        type: { summary: '"user" | "assistant"' },
      },
    },
    content: {
      control: 'text',
      description: 'Message text content',
    },
    confidence: {
      control: { type: 'number', min: 0, max: 100, step: 1 },
      description: 'AI confidence score (0-100, AI messages only)',
    },
    isTyping: {
      control: 'boolean',
      description: 'Show typing indicator instead of content',
    },
  },
  decorators: [
    Story => (
      <div className="w-full max-w-2xl">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ChatMessage>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * AI message with high confidence (≥85%).
 * Displays green confidence badge and MeepleAvatar in confident state.
 */
export const AIHighConfidence: Story = {
  args: {
    role: 'assistant',
    content:
      'Le risorse si piazzano sui territori adiacenti agli insediamenti. Ogni giocatore riceve le risorse prodotte dai territori con numero corrispondente al dado.',
    confidence: 95,
    citations: [
      { id: '1', label: 'Regolamento', page: 5 },
      { id: '2', label: 'FAQ', page: 12 },
    ],
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * AI message with medium confidence (70-84%).
 * Displays yellow confidence badge and MeepleAvatar in searching state.
 */
export const AIMediumConfidence: Story = {
  args: {
    role: 'assistant',
    content:
      "Questa regola potrebbe variare in base all'espansione utilizzata. Controlla il manuale specifico per conferma.",
    confidence: 78,
    citations: [{ id: '3', label: 'Espansioni', page: 23 }],
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * AI message with low confidence (<70%).
 * Displays red confidence badge and MeepleAvatar in uncertain state.
 */
export const AILowConfidence: Story = {
  args: {
    role: 'assistant',
    content:
      'Non sono sicuro di questa regola. Ti consiglio di verificare nel manuale ufficiale per evitare errori.',
    confidence: 62,
    citations: [{ id: '4', label: 'Manuale Base', page: 8 }],
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * AI typing indicator state.
 * Displays animated 3-dot loader and MeepleAvatar in thinking state.
 */
export const AITyping: Story = {
  args: {
    role: 'assistant',
    content: '',
    isTyping: true,
  },
};

/**
 * User message with standard avatar.
 * Right-aligned layout with user avatar fallback.
 */
export const UserMessage: Story = {
  args: {
    role: 'user',
    content: 'Come si piazzano le risorse in Catan?',
    avatar: { fallback: 'U' },
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * User message with custom avatar image.
 * Demonstrates avatar with image source.
 */
export const UserMessageWithAvatar: Story = {
  args: {
    role: 'user',
    content: 'Grazie per la spiegazione!',
    avatar: {
      src: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Felix',
      fallback: 'F',
    },
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * AI message without citations.
 * Shows confidence badge but no citation links.
 */
export const AINoCitations: Story = {
  args: {
    role: 'assistant',
    content: 'Catan è un gioco strategico per 3-4 giocatori che dura circa 90 minuti.',
    confidence: 88,
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * AI message with multiple citations.
 * Demonstrates citation link wrapping for many references.
 */
export const AIMultipleCitations: Story = {
  args: {
    role: 'assistant',
    content:
      'Questa meccanica è spiegata in dettaglio in diverse sezioni del manuale e nelle FAQ ufficiali.',
    confidence: 92,
    citations: [
      { id: '1', label: 'Regolamento', page: 5 },
      { id: '2', label: 'FAQ', page: 12 },
      { id: '3', label: 'Glossario', page: 45 },
      { id: '4', label: 'Esempi', page: 28 },
      { id: '5', label: 'Chiarimenti', page: 33 },
    ],
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * Conversation flow showcase.
 * Displays alternating user and AI messages to demonstrate layout.
 */
export const ConversationFlow: Story = {
  render: () => (
    <div className="space-y-4 w-full">
      <ChatMessage
        role="user"
        content="Come funziona il ladrone in Catan?"
        avatar={{ fallback: 'U' }}
        timestamp={FIXED_TIMESTAMPS.twoMinutesAgo}
      />
      <ChatMessage
        role="assistant"
        content="Il ladrone si attiva quando si tira un 7. Il giocatore può spostarlo su un territorio per bloccare la produzione e rubare una carta casuale a un avversario."
        confidence={94}
        citations={[
          { id: '1', label: 'Regolamento', page: 7 },
          { id: '2', label: 'FAQ', page: 15 },
        ]}
        timestamp={FIXED_TIMESTAMPS.oneMinuteAgo}
      />
      <ChatMessage
        role="user"
        content="E se nessuno ha carte?"
        avatar={{ fallback: 'U' }}
        timestamp={FIXED_TIMESTAMPS.thirtySecondsAgo}
      />
      <ChatMessage
        role="assistant"
        content="In quel caso, il ladrone viene comunque spostato ma non si ruba alcuna carta. Il blocco del territorio rimane attivo."
        confidence={97}
        citations={[{ id: '3', label: 'FAQ', page: 16 }]}
        timestamp={FIXED_TIMESTAMPS.now}
      />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Example conversation showing alternating user and AI messages.',
      },
    },
  },
};

/**
 * Long message content test.
 * Verifies message bubble handles long text properly.
 */
export const LongContent: Story = {
  args: {
    role: 'assistant',
    content:
      "Catan è un gioco di strategia e negoziazione dove i giocatori competono per costruire il miglior insediamento sull'isola di Catan. Durante il gioco, raccoglierai risorse (legno, argilla, grano, lana, pietra), costruirai strade, insediamenti e città, e scambierai con altri giocatori. Il primo a raggiungere 10 punti vittoria vince la partita. Le risorse vengono ottenute tramite il lancio di due dadi: se il numero corrisponde a un territorio dove hai un insediamento, ricevi quella risorsa. Ogni giocatore inizia con 2 insediamenti e 2 strade, e può espandersi costruendo ulteriori strutture. È fondamentale pianificare la posizione iniziale considerando la probabilità dei numeri e la varietà delle risorse disponibili.",
    confidence: 89,
    citations: [
      { id: '1', label: 'Regolamento', page: 1 },
      { id: '2', label: 'Guida Strategica', page: 3 },
    ],
    timestamp: FIXED_TIMESTAMP,
  },
};

/**
 * Dark theme variant.
 * Shows message appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    role: 'assistant',
    content: 'Il commercio è una parte fondamentale di Catan.',
    confidence: 91,
    citations: [{ id: '1', label: 'Regolamento', page: 10 }],
    timestamp: FIXED_TIMESTAMP,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Interactive citation click test.
 * Logs citation clicks to Storybook actions panel.
 */
export const CitationInteraction: Story = {
  args: {
    role: 'assistant',
    content: 'Clicca sui badge delle citazioni per testarli.',
    confidence: 90,
    citations: [
      { id: 'cite-1', label: 'Doc A', page: 5 },
      { id: 'cite-2', label: 'Doc B', page: 12 },
    ],
    onCitationClick: (documentId: string, pageNumber: number) => {
      console.log('Citation clicked:', documentId, 'page:', pageNumber);
      alert(`Citation clicked: ${documentId} (page ${pageNumber})`);
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Click on citation badges to test the onCitationClick handler.',
      },
    },
  },
};

/**
 * Multiple citations to same PDF (Issue #1940 fix verification).
 * Visual regression test for citation page preservation.
 *
 * Tests that each citation badge correctly passes its specific page number,
 * even when multiple citations reference the same document.
 *
 * @see https://github.com/meepleai/monorepo/issues/1940
 */
export const MultipleCitationsSameDocument: Story = {
  args: {
    role: 'assistant',
    content:
      'Le regole del setup sono a pagina 3. Le condizioni di vittoria sono a pagina 7. Il turno di gioco è spiegato a pagina 12. Tutti questi riferimenti sono allo stesso manuale.',
    confidence: 92,
    citations: [
      { id: 'rulebook-pdf', label: 'Manuale', page: 3 },
      { id: 'rulebook-pdf', label: 'Manuale', page: 7 },
      { id: 'rulebook-pdf', label: 'Manuale', page: 12 },
    ],
    timestamp: FIXED_TIMESTAMP,
    onCitationClick: (documentId: string, pageNumber: number) => {
      console.log(`Citation clicked: ${documentId} → page ${pageNumber}`);
      alert(`Opening ${documentId} at page ${pageNumber}`);
    },
  },
  parameters: {
    chromatic: {
      // Disable Chromatic snapshot for interactive test
      disableSnapshot: false,
    },
    docs: {
      description: {
        story:
          'Test for Issue #1940: Verifies that clicking different citations to the same PDF opens the correct page. Each citation badge should pass its specific page number (3, 7, 12) despite having the same document ID.',
      },
    },
  },
};

/**
 * All confidence levels comparison.
 * Visual comparison of badge colors at different confidence thresholds.
 */
export const ConfidenceLevels: Story = {
  render: () => (
    <div className="space-y-4 w-full">
      <div className="mb-4 text-sm font-medium text-muted-foreground">
        Confidence Badge Color Mapping:
      </div>
      <ChatMessage
        role="assistant"
        content="High confidence (≥85%) - Green badge"
        confidence={95}
        citations={[{ id: '1', label: 'Source', page: 1 }]}
      />
      <ChatMessage
        role="assistant"
        content="Medium confidence (70-84%) - Yellow badge"
        confidence={75}
        citations={[{ id: '2', label: 'Source', page: 2 }]}
      />
      <ChatMessage
        role="assistant"
        content="Low confidence (<70%) - Red badge"
        confidence={55}
        citations={[{ id: '3', label: 'Source', page: 3 }]}
      />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of confidence badge colors at different thresholds.',
      },
    },
  },
};
