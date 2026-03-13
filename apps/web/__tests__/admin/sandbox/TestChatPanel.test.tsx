import { render, screen, within, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';
import { SandboxChat } from '@/components/admin/sandbox/SandboxChat';
import { RetrievedChunkCard } from '@/components/admin/sandbox/RetrievedChunkCard';
import { DebugSidePanel } from '@/components/admin/sandbox/DebugSidePanel';
import { AutoTestRunner } from '@/components/admin/sandbox/AutoTestRunner';
import { AutoTestSummary } from '@/components/admin/sandbox/AutoTestSummary';
import { TestChatPanel } from '@/components/admin/sandbox/TestChatPanel';
import { PipelineProvider } from '@/components/admin/sandbox/contexts/PipelineContext';
import { SandboxSessionProvider } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { SourceProvider } from '@/components/admin/sandbox/contexts/SourceContext';
import type {
  RetrievedChunk,
  PipelineTrace,
  AutoTestResult,
} from '@/components/admin/sandbox/types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function AllProviders({ children }: { children: ReactNode }) {
  return (
    <SourceProvider>
      <PipelineProvider>
        <SandboxSessionProvider>{children}</SandboxSessionProvider>
      </PipelineProvider>
    </SourceProvider>
  );
}

/**
 * Wraps children in PipelineProvider with a helper to set isAllReady.
 * By default pipeline is NOT ready (empty map → isAllReady = false).
 */
function PipelineWrapper({ children }: { children: ReactNode }) {
  return <PipelineProvider>{children}</PipelineProvider>;
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const mockChunk: RetrievedChunk = {
  id: 'chunk-1',
  score: 0.85,
  text: 'Il gioco si prepara distribuendo le carte iniziali a ciascun giocatore e posizionando il tabellone al centro del tavolo. Ogni giocatore riceve le proprie risorse iniziali.',
  page: 3,
  chunkIndex: 0,
  pdfName: 'regolamento.pdf',
  used: true,
};

const mockChunkLow: RetrievedChunk = {
  id: 'chunk-2',
  score: 0.35,
  text: 'Le carte bonus vengono mescolate e poste coperte.',
  page: 5,
  chunkIndex: 2,
  pdfName: 'regolamento.pdf',
  used: false,
};

const mockTrace: PipelineTrace = {
  steps: [
    { name: 'Query Analysis', durationMs: 12, details: { language: 'it', intent: 'setup_rules' } },
    { name: 'Dense Search', durationMs: 45, details: { collection: 'game_docs', candidates: 50 } },
    { name: 'Sparse Search', durationMs: 28, details: { bm25Matches: 12 } },
    { name: 'Hybrid Merge', durationMs: 5, details: { uniqueResults: 15 } },
    { name: 'Reranking', durationMs: 120, details: { model: 'cross-encoder/ms-marco' } },
    { name: 'LLM Generation', durationMs: 890, details: { model: 'gpt-4', tokensIn: 1200 } },
  ],
  totalDurationMs: 1100,
};

const mockMessageData = {
  chunks: [mockChunk, mockChunkLow],
  trace: mockTrace,
  latencyMs: 1100,
  avgConfidence: 0.6,
};

const mockAutoTestResults: AutoTestResult[] = [
  {
    question: 'Come si prepara il gioco?',
    answer: 'Si distribuiscono le carte.',
    confidence: 0.8,
    passed: true,
    latencyMs: 500,
  },
  {
    question: 'Quanti giocatori possono giocare?',
    answer: 'Da 2 a 5.',
    confidence: 0.7,
    passed: true,
    latencyMs: 450,
  },
  {
    question: 'Come si vince?',
    answer: 'Accumulando punti.',
    confidence: 0.6,
    passed: true,
    latencyMs: 600,
  },
  {
    question: 'Quali sono le regole base?',
    answer: 'Tre fasi per turno.',
    confidence: 0.55,
    passed: true,
    latencyMs: 520,
  },
  {
    question: 'Come funziona il turno?',
    answer: 'Pesca, azione, mantenimento.',
    confidence: 0.4,
    passed: false,
    latencyMs: 480,
  },
  {
    question: 'Ci sono espansioni?',
    answer: 'Due espansioni disponibili.',
    confidence: 0.65,
    passed: true,
    latencyMs: 550,
  },
  {
    question: 'Quanto dura una partita?',
    answer: '45-60 minuti.',
    confidence: 0.75,
    passed: true,
    latencyMs: 430,
  },
  {
    question: 'Quali componenti?',
    answer: 'Carte, segnalini, dadi.',
    confidence: 0.3,
    passed: false,
    latencyMs: 510,
  },
];

// ---------------------------------------------------------------------------
// SandboxChat
// ---------------------------------------------------------------------------

describe('SandboxChat', () => {
  it('renders welcome message when no messages exist', () => {
    render(
      <PipelineWrapper>
        <SandboxChat selectedMessageId={null} onSelectMessage={vi.fn()} />
      </PipelineWrapper>
    );

    expect(screen.getByTestId('welcome-message')).toHaveTextContent(
      'Invia un messaggio per testare il RAG agent'
    );
  });

  it('disables input when pipeline is not ready', () => {
    // PipelineProvider with empty map → isAllReady = false
    render(
      <PipelineWrapper>
        <SandboxChat selectedMessageId={null} onSelectMessage={vi.fn()} />
      </PipelineWrapper>
    );

    const input = screen.getByTestId('chat-input');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Pipeline non pronta');
  });

  it('renders send button as disabled when input is empty', () => {
    render(
      <PipelineWrapper>
        <SandboxChat selectedMessageId={null} onSelectMessage={vi.fn()} />
      </PipelineWrapper>
    );

    expect(screen.getByTestId('send-button')).toBeDisabled();
  });
});

// ---------------------------------------------------------------------------
// RetrievedChunkCard
// ---------------------------------------------------------------------------

describe('RetrievedChunkCard', () => {
  it('renders score bar and text preview', () => {
    render(<RetrievedChunkCard chunk={mockChunk} />);

    expect(screen.getByTestId('score-bar')).toBeInTheDocument();
    expect(screen.getByTestId('score-value')).toHaveTextContent('0.85');
    expect(screen.getByTestId('chunk-text')).toBeInTheDocument();
  });

  it('shows "Usato" badge when chunk is used', () => {
    render(<RetrievedChunkCard chunk={mockChunk} />);

    expect(screen.getByTestId('used-badge')).toHaveTextContent('Usato');
  });

  it('does not show "Usato" badge when chunk is not used', () => {
    render(<RetrievedChunkCard chunk={mockChunkLow} />);

    expect(screen.queryByTestId('used-badge')).not.toBeInTheDocument();
  });

  it('shows source info (PDF name, page, chunk index)', () => {
    render(<RetrievedChunkCard chunk={mockChunk} />);

    expect(screen.getByText('regolamento.pdf')).toBeInTheDocument();
    expect(screen.getByText('p.3')).toBeInTheDocument();
  });

  it('expands text when expanded prop is true', () => {
    const { rerender } = render(
      <RetrievedChunkCard chunk={mockChunk} expanded={false} onToggleExpand={vi.fn()} />
    );

    const textEl = screen.getByTestId('chunk-text');
    expect(textEl.className).toContain('line-clamp-2');

    rerender(<RetrievedChunkCard chunk={mockChunk} expanded={true} onToggleExpand={vi.fn()} />);

    expect(screen.getByTestId('chunk-text').className).not.toContain('line-clamp-2');
  });

  it('calls onToggleExpand when expand toggle is clicked', async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();

    render(<RetrievedChunkCard chunk={mockChunk} expanded={false} onToggleExpand={onToggle} />);

    await user.click(screen.getByTestId('chunk-text-area'));
    expect(onToggle).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// DebugSidePanel
// ---------------------------------------------------------------------------

describe('DebugSidePanel', () => {
  it('shows empty state when no message selected', () => {
    render(
      <SandboxSessionProvider>
        <DebugSidePanel />
      </SandboxSessionProvider>
    );

    expect(screen.getByTestId('debug-panel-empty')).toBeInTheDocument();
    expect(screen.getByText('Seleziona un messaggio per vedere i dettagli')).toBeInTheDocument();
  });

  it('renders 3 accordion sections when message data is provided', () => {
    render(
      <SandboxSessionProvider>
        <DebugSidePanel messageData={mockMessageData} />
      </SandboxSessionProvider>
    );

    expect(screen.getByTestId('accordion-chunks')).toBeInTheDocument();
    expect(screen.getByTestId('accordion-trace')).toBeInTheDocument();
    expect(screen.getByTestId('accordion-waterfall')).toBeInTheDocument();
  });

  it('renders chunk cards in chunks section', () => {
    render(
      <SandboxSessionProvider>
        <DebugSidePanel messageData={mockMessageData} />
      </SandboxSessionProvider>
    );

    // Chunks section is default open
    const chunkCards = screen.getAllByTestId('chunk-card');
    expect(chunkCards).toHaveLength(2);
  });

  it('renders footer summary with strategy and latency', () => {
    render(
      <SandboxSessionProvider>
        <DebugSidePanel messageData={mockMessageData} />
      </SandboxSessionProvider>
    );

    const footer = screen.getByTestId('debug-footer');
    expect(footer).toHaveTextContent('hybrid-v2');
    expect(footer).toHaveTextContent('1100ms');
    expect(footer).toHaveTextContent('60%');
  });
});

// ---------------------------------------------------------------------------
// AutoTestRunner
// ---------------------------------------------------------------------------

describe('AutoTestRunner', () => {
  it('renders the run button', () => {
    render(<AutoTestRunner onComplete={vi.fn()} disabled={false} />);

    expect(screen.getByTestId('run-auto-test-button')).toBeInTheDocument();
    expect(screen.getByTestId('run-auto-test-button')).toHaveTextContent('Esegui Auto-Test');
  });

  it('disables button when disabled prop is true', () => {
    render(<AutoTestRunner onComplete={vi.fn()} disabled={true} />);

    expect(screen.getByTestId('run-auto-test-button')).toBeDisabled();
  });

  it('shows progress bar when running', async () => {
    const user = userEvent.setup();

    render(<AutoTestRunner onComplete={vi.fn()} disabled={false} />);

    await user.click(screen.getByTestId('run-auto-test-button'));

    expect(screen.getByTestId('auto-test-progress')).toBeInTheDocument();
    expect(screen.getByTestId('progress-label')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// AutoTestSummary
// ---------------------------------------------------------------------------

describe('AutoTestSummary', () => {
  it('renders overall score badge', () => {
    render(<AutoTestSummary results={mockAutoTestResults} />);

    expect(screen.getByTestId('overall-score')).toHaveTextContent('6/8');
  });

  it('renders per-question results', () => {
    render(<AutoTestSummary results={mockAutoTestResults} />);

    const list = screen.getByTestId('question-results');
    expect(list.children).toHaveLength(8);
  });

  it('renders stats (avg confidence, avg latency, total time)', () => {
    render(<AutoTestSummary results={mockAutoTestResults} />);

    expect(screen.getByTestId('avg-confidence')).toBeInTheDocument();
    expect(screen.getByTestId('avg-latency')).toBeInTheDocument();
    expect(screen.getByTestId('total-time')).toBeInTheDocument();
  });

  it('shows recommendation for failed questions', () => {
    render(<AutoTestSummary results={mockAutoTestResults} />);

    expect(screen.getByTestId('recommendation')).toHaveTextContent(
      'Verifica i chunk recuperati per le domande fallite'
    );
  });

  it('hides recommendation when all pass', () => {
    const allPassed = mockAutoTestResults.map(r => ({ ...r, passed: true, confidence: 0.8 }));
    render(<AutoTestSummary results={allPassed} />);

    expect(screen.queryByTestId('recommendation')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// TestChatPanel (integration)
// ---------------------------------------------------------------------------

describe('TestChatPanel', () => {
  it('renders split view with chat and debug areas', () => {
    render(
      <AllProviders>
        <TestChatPanel />
      </AllProviders>
    );

    expect(screen.getByTestId('test-chat-panel')).toBeInTheDocument();
    expect(screen.getByTestId('chat-area')).toBeInTheDocument();
    expect(screen.getByTestId('debug-area')).toBeInTheDocument();
  });

  it('renders panel title with Test Chat text', () => {
    render(
      <AllProviders>
        <TestChatPanel />
      </AllProviders>
    );

    expect(screen.getByText('Test Chat')).toBeInTheDocument();
  });

  it('renders clear button', () => {
    render(
      <AllProviders>
        <TestChatPanel />
      </AllProviders>
    );

    expect(screen.getByTestId('clear-chat-button')).toBeInTheDocument();
    expect(screen.getByTestId('clear-chat-button')).toHaveTextContent('Pulisci');
  });

  it('renders auto-test button', () => {
    render(
      <AllProviders>
        <TestChatPanel />
      </AllProviders>
    );

    expect(screen.getByTestId('run-auto-test-button')).toBeInTheDocument();
  });

  it('shows debug empty state when no message is selected', () => {
    render(
      <AllProviders>
        <TestChatPanel />
      </AllProviders>
    );

    expect(screen.getByTestId('debug-panel-empty')).toBeInTheDocument();
  });
});
