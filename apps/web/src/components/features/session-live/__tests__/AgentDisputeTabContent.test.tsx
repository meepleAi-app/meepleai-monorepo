/**
 * AgentDisputeTabContent unit tests — #2588 A4
 *
 * Coverage:
 *   - renders data-slot="agent-dispute-tab-content"
 *   - renders the ArbitroModal trigger button ("⚖️ Arbitro")
 *   - clicking the button opens ArbitroModal (modal renders in DOM)
 *   - renders DisputeHistory (hidden when 0 disputes, visible when disputes exist)
 *   - DisputeHistory shows verdicts from useLiveSessionStore.disputes
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AgentDisputeTabContent } from '../AgentDisputeTabContent';

// ─── Mocks ────────────────────────────────────────────────────────────────────

// Mock the api (ArbitroModal calls api.liveSessions.submitDispute)
vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      submitDispute: vi.fn(),
    },
  },
}));

// Mock useLiveSessionStore — controllable disputes array
let mockDisputes: Array<{
  id: string;
  description: string;
  verdict: string;
  ruleReferences: string[];
  raisedByPlayerName: string;
  timestamp: string;
}> = [];

vi.mock('@/lib/stores/live-session-store', () => ({
  useLiveSessionStore: (
    selector: (s: { disputes: typeof mockDisputes; addDispute: () => void }) => unknown
  ) => selector({ disputes: mockDisputes, addDispute: vi.fn() }),
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna' },
];

function renderContent(sessionId = 'sess-001') {
  return render(<AgentDisputeTabContent sessionId={sessionId} players={PLAYERS} />);
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AgentDisputeTabContent — render shape', () => {
  beforeEach(() => {
    mockDisputes = [];
  });

  it('renders data-slot="agent-dispute-tab-content"', () => {
    renderContent();
    expect(document.querySelector('[data-slot="agent-dispute-tab-content"]')).toBeInTheDocument();
  });

  it('renders the ArbitroModal trigger button', () => {
    renderContent();
    // ArbitroModal renders a DialogTrigger Button with "⚖️ Arbitro"
    expect(screen.getByRole('button', { name: /Arbitro/i })).toBeInTheDocument();
  });

  it('DisputeHistory is not rendered when disputes list is empty', () => {
    mockDisputes = [];
    renderContent();
    // DisputeHistory returns null when disputes.length === 0
    expect(screen.queryByRole('button', { name: /Verdetti precedenti/i })).not.toBeInTheDocument();
  });
});

describe('AgentDisputeTabContent — ArbitroModal interaction', () => {
  beforeEach(() => {
    mockDisputes = [];
  });

  it('clicking the Arbitro button opens the dialog', async () => {
    const user = userEvent.setup();
    renderContent();

    const triggerBtn = screen.getByRole('button', { name: /Arbitro/i });
    await user.click(triggerBtn);

    // The dialog is now open — the form heading "Chiedi all'Arbitro" should be present
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Chiedi all.Arbitro/i })).toBeInTheDocument();
  });

  it('dialog contains the player select with correct options', async () => {
    const user = userEvent.setup();
    renderContent();

    await user.click(screen.getByRole('button', { name: /Arbitro/i }));

    const select = screen.getByRole('combobox', { name: /Sollevata da/i });
    expect(select).toBeInTheDocument();
    // Players are rendered as options
    expect(screen.getByRole('option', { name: 'Marco' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Anna' })).toBeInTheDocument();
  });
});

describe('AgentDisputeTabContent — DisputeHistory', () => {
  it('DisputeHistory renders toggle button when disputes exist', () => {
    mockDisputes = [
      {
        id: 'd1',
        description: 'Può giocare questa carta?',
        verdict: 'No, non può.',
        ruleReferences: ['Rule 5.3'],
        raisedByPlayerName: 'Marco',
        timestamp: '2026-06-30T10:00:00Z',
      },
    ];

    renderContent();

    expect(screen.getByRole('button', { name: /Verdetti precedenti \(1\)/i })).toBeInTheDocument();
  });

  it('expanding DisputeHistory shows the verdict for a dispute', async () => {
    mockDisputes = [
      {
        id: 'd1',
        description: 'Può giocare questa carta?',
        verdict: 'No, non può.',
        ruleReferences: ['Rule 5.3'],
        raisedByPlayerName: 'Marco',
        timestamp: '2026-06-30T10:00:00Z',
      },
    ];

    const user = userEvent.setup();
    renderContent();

    const toggleBtn = screen.getByRole('button', { name: /Verdetti precedenti/i });
    await user.click(toggleBtn);

    // After expanding, the dispute description should appear
    expect(screen.getByText(/Può giocare questa carta/i)).toBeInTheDocument();
  });
});
