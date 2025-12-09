/**
 * ErrorDisplay Storybook Stories
 *
 * Visual regression tests for Chromatic.
 * Covers all variants, states, and accessibility scenarios.
 *
 * @issue BGAI-068
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { ErrorDisplay } from './ErrorDisplay';
import { NetworkError, ServerError, RateLimitError, ApiError } from '@/lib/api/core/errors';

const meta = {
  title: 'Components/Error/ErrorDisplay',
  component: ErrorDisplay,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Error display component with retry functionality. Supports different error types with appropriate icons, messaging, and countdown timers for rate limits.',
      },
    },
    // Chromatic settings
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['generic', 'network', 'server', 'rateLimit'],
      description: 'Error variant determining icon and styling',
    },
    title: {
      control: 'text',
      description: 'Main error title',
    },
    description: {
      control: 'text',
      description: 'Optional description text',
    },
    retryLabel: {
      control: 'text',
      description: 'Retry button label',
    },
    retryAfterSeconds: {
      control: 'number',
      description: 'Countdown seconds for rate limit',
    },
    showRetry: {
      control: 'boolean',
      description: 'Show retry button',
    },
  },
  args: {
    onRetry: fn(),
    showRetry: true,
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-4">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ErrorDisplay>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic Variants
// =============================================================================

/**
 * Generic error - default variant for unexpected errors
 */
export const Generic: Story = {
  args: {
    title: 'Errore imprevisto',
    description: 'Si è verificato un errore. Riprova più tardi.',
    variant: 'generic',
  },
};

/**
 * Network error - connection issues
 */
export const Network: Story = {
  args: {
    title: 'Errore di connessione',
    description: 'Impossibile raggiungere il server. Verifica la tua connessione internet.',
    variant: 'network',
  },
};

/**
 * Server error - backend issues (500, 502, 503)
 */
export const Server: Story = {
  args: {
    title: 'Errore del server',
    description: 'Il server sta riscontrando problemi. Riprova tra qualche minuto.',
    variant: 'server',
  },
};

/**
 * Rate limit error - too many requests (429)
 */
export const RateLimit: Story = {
  args: {
    title: 'Troppe richieste',
    description: 'Hai effettuato troppe richieste. Attendi prima di riprovare.',
    variant: 'rateLimit',
    retryAfterSeconds: 30,
  },
};

// =============================================================================
// Error Object Integration
// =============================================================================

/**
 * With NetworkError object - auto-detects variant
 */
export const WithNetworkError: Story = {
  args: {
    title: 'Connessione persa',
    error: new NetworkError({
      message: 'Failed to fetch: network error',
      endpoint: '/api/v1/chat',
    }),
  },
};

/**
 * With ServerError object - auto-detects variant
 */
export const WithServerError: Story = {
  args: {
    title: 'Errore interno',
    error: new ServerError({
      message: 'Internal server error',
      endpoint: '/api/v1/chat',
      statusCode: 500,
      correlationId: 'abc-123-def-456',
    }),
  },
};

/**
 * With RateLimitError object - shows countdown automatically
 */
export const WithRateLimitError: Story = {
  args: {
    title: 'Limite raggiunto',
    error: new RateLimitError({
      message: 'Rate limit exceeded',
      endpoint: '/api/v1/chat',
      retryAfter: 60,
    }),
  },
};

/**
 * With generic ApiError - shows correlation ID
 */
export const WithApiError: Story = {
  args: {
    title: 'Errore API',
    error: new ApiError({
      message: 'Bad request: invalid game ID',
      statusCode: 400,
      endpoint: '/api/v1/chat',
      correlationId: 'corr-789-xyz-012',
    }),
  },
};

// =============================================================================
// UI States
// =============================================================================

/**
 * Without retry button
 */
export const NoRetryButton: Story = {
  args: {
    title: 'Errore permanente',
    description: 'Questo errore non può essere risolto automaticamente.',
    variant: 'generic',
    showRetry: false,
  },
};

/**
 * Custom retry label
 */
export const CustomRetryLabel: Story = {
  args: {
    title: 'Richiesta fallita',
    description: 'La richiesta non è andata a buon fine.',
    variant: 'server',
    retryLabel: 'Riprova ora',
  },
};

/**
 * Countdown active - button disabled
 */
export const CountdownActive: Story = {
  args: {
    title: 'Attendi...',
    description: 'Il countdown deve terminare prima di poter riprovare.',
    variant: 'rateLimit',
    retryAfterSeconds: 45,
  },
  parameters: {
    chromatic: {
      // Pause animations for snapshot
      pauseAnimationAtEnd: true,
    },
  },
};

/**
 * Countdown finished - button enabled
 */
export const CountdownFinished: Story = {
  args: {
    title: 'Puoi riprovare',
    description: 'Il countdown è terminato.',
    variant: 'rateLimit',
    retryAfterSeconds: 0,
  },
};

// =============================================================================
// Italian Q&A Context
// =============================================================================

/**
 * Chat streaming error
 */
export const ChatStreamingError: Story = {
  args: {
    title: 'Errore nella risposta',
    description: "Non è stato possibile completare la risposta. L'AI potrebbe essere occupata.",
    variant: 'server',
  },
};

/**
 * Game not found error
 */
export const GameNotFoundError: Story = {
  args: {
    title: 'Gioco non trovato',
    description: 'Il gioco selezionato non è più disponibile nel database.',
    variant: 'generic',
    showRetry: false,
  },
};

/**
 * RAG search error
 */
export const RAGSearchError: Story = {
  args: {
    title: 'Ricerca fallita',
    description: 'Impossibile cercare nel database delle regole. Riprova tra poco.',
    variant: 'server',
  },
};

// =============================================================================
// Responsive & Accessibility
// =============================================================================

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {
    title: 'Errore di connessione',
    description: 'Verifica la tua connessione e riprova.',
    variant: 'network',
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Long text content
 */
export const LongContent: Story = {
  args: {
    title: 'Errore durante il caricamento delle informazioni del gioco selezionato',
    description:
      'Si è verificato un errore imprevisto durante il tentativo di recuperare le informazioni dal server. ' +
      'Questo potrebbe essere dovuto a problemi di connessione, manutenzione del server, o un errore temporaneo. ' +
      'Ti consigliamo di attendere qualche minuto e riprovare.',
    variant: 'server',
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  args: {
    title: 'Errore del server',
    description: 'Il server non risponde. Riprova più tardi.',
    variant: 'server',
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-4 bg-slate-900 rounded-lg">
        <Story />
      </div>
    ),
  ],
};

// =============================================================================
// All Variants Grid (for visual comparison)
// =============================================================================

/**
 * All variants side by side for visual comparison
 */
export const AllVariants: Story = {
  render: () => (
    <div className="grid gap-6 w-full max-w-4xl">
      <ErrorDisplay
        title="Errore generico"
        description="Si è verificato un errore imprevisto."
        variant="generic"
        onRetry={() => {}}
      />
      <ErrorDisplay
        title="Errore di rete"
        description="Impossibile connettersi al server."
        variant="network"
        onRetry={() => {}}
      />
      <ErrorDisplay
        title="Errore server"
        description="Il server sta riscontrando problemi."
        variant="server"
        onRetry={() => {}}
      />
      <ErrorDisplay
        title="Troppe richieste"
        description="Attendi prima di riprovare."
        variant="rateLimit"
        retryAfterSeconds={15}
        onRetry={() => {}}
      />
    </div>
  ),
  parameters: {
    chromatic: {
      viewports: [1024],
    },
  },
};
