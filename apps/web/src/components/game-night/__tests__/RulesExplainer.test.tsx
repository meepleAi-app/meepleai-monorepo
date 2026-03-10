import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

import { RulesExplainer } from '../RulesExplainer';

/**
 * Tests for RulesExplainer — progressive rules presentation.
 * Issue #5584: Rules Explainer component.
 */

function createMockAnalysis(overrides: Partial<RulebookAnalysisDto> = {}): RulebookAnalysisDto {
  return {
    id: 'a1a1a1a1-0000-0000-0000-000000000001',
    sharedGameId: 'g1g1g1g1-0000-0000-0000-000000000001',
    pdfDocumentId: 'p1p1p1p1-0000-0000-0000-000000000001',
    gameTitle: 'Test Game',
    summary: 'A strategy game about building settlements.',
    keyMechanics: ['Trading', 'Resource Management', 'Area Control'],
    victoryConditions: {
      primary: 'Reach 10 victory points',
      alternatives: ['Longest road', 'Largest army'],
      isPointBased: true,
      targetPoints: 10,
    },
    resources: [
      { name: 'Wood', type: 'Basic', usage: 'Building material', isLimited: true },
      { name: 'Brick', type: 'Basic', usage: 'Road construction', isLimited: false },
    ],
    gamePhases: [
      { name: 'Setup', description: 'Place initial settlements', order: 1, isOptional: false },
      { name: 'Trading Phase', description: 'Exchange resources', order: 2, isOptional: true },
      { name: 'Building', description: 'Construct settlements', order: 3, isOptional: false },
    ],
    commonQuestions: ['Can I trade on my first turn?'],
    confidenceScore: 0.92,
    version: 1,
    isActive: true,
    source: 's1s1s1s1-0000-0000-0000-000000000001',
    analyzedAt: '2026-03-01T00:00:00Z',
    createdBy: 'u1u1u1u1-0000-0000-0000-000000000001',
    keyConcepts: [
      {
        term: 'Trading',
        definition: 'Exchanging resources with other players',
        category: 'Actions',
      },
      { term: 'Settlement', definition: 'A small building worth 1 VP', category: 'Buildings' },
    ],
    generatedFaqs: [
      {
        question: 'Can you trade with any player?',
        answer: 'Yes, during your turn you may trade with any player.',
        sourceSection: 'Trading',
        confidence: 0.88,
        tags: ['trade', 'basics'],
      },
    ],
    gameStateSchemaJson: null,
    completionStatus: 'Complete',
    missingSections: null,
    ...overrides,
  };
}

describe('RulesExplainer', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    // scrollIntoView not available in jsdom
    Element.prototype.scrollIntoView = vi.fn();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // =========================================================================
  // Rendering
  // =========================================================================

  it('renders the rules explainer with game title', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByTestId('rules-explainer')).toBeInTheDocument();
    expect(screen.getByText(/Test Game — Rules/)).toBeInTheDocument();
  });

  it('uses custom gameTitle when provided', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} gameTitle="Custom Title" />);
    expect(screen.getByText(/Custom Title — Rules/)).toBeInTheDocument();
  });

  it('shows section count and estimated time', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByText(/6 sections/)).toBeInTheDocument();
  });

  it('shows empty state when no data is available', () => {
    const emptyAnalysis = createMockAnalysis({
      summary: '',
      keyMechanics: [],
      gamePhases: [],
      victoryConditions: null,
      resources: [],
      generatedFaqs: [],
      commonQuestions: [],
    });
    render(<RulesExplainer analysis={emptyAnalysis} />);
    expect(screen.getByTestId('rules-explainer-empty')).toBeInTheDocument();
    expect(screen.getByText(/No analysis data available/)).toBeInTheDocument();
  });

  // =========================================================================
  // Summary Section (default)
  // =========================================================================

  it('shows summary section by default', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByTestId('section-summary')).toBeInTheDocument();
    expect(screen.getByText('A strategy game about building settlements.')).toBeInTheDocument();
  });

  it('shows key mechanics as badges in summary', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByText('Trading')).toBeInTheDocument();
    expect(screen.getByText('Resource Management')).toBeInTheDocument();
    expect(screen.getByText('Area Control')).toBeInTheDocument();
  });

  // =========================================================================
  // Navigation
  // =========================================================================

  it('navigates to next section via Next button', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    await user.click(screen.getByRole('button', { name: /next section/i }));
    expect(screen.getByTestId('section-mechanics')).toBeInTheDocument();
  });

  it('navigates to previous section via Previous button', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Go to mechanics
    await user.click(screen.getByRole('button', { name: /next section/i }));
    expect(screen.getByTestId('section-mechanics')).toBeInTheDocument();

    // Go back to summary
    await user.click(screen.getByRole('button', { name: /previous/i }));
    expect(screen.getByTestId('section-summary')).toBeInTheDocument();
  });

  it('disables Previous button on first section', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
  });

  it('navigates via tab buttons', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Click on the Victory tab
    const tabs = screen.getAllByRole('tab');
    // Find the Victory tab — tabs are in order: Summary, Mechanics, Phases, Victory, Resources, FAQ
    const victoryTab = tabs.find(
      tab => tab.getAttribute('aria-selected') === 'false' && tab.textContent?.includes('Victory')
    );
    expect(victoryTab).toBeDefined();
    await user.click(victoryTab!);

    expect(screen.getByTestId('section-victory')).toBeInTheDocument();
    expect(screen.getByText('Reach 10 victory points')).toBeInTheDocument();
  });

  it('updates progress bar as user navigates', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Initially at section 1 of 6
    expect(screen.getByText('Section 1 of 6')).toBeInTheDocument();
    expect(screen.getByText('0%')).toBeInTheDocument();

    // Navigate to next
    await user.click(screen.getByRole('button', { name: /next section/i }));
    expect(screen.getByText('Section 2 of 6')).toBeInTheDocument();
    expect(screen.getByText('20%')).toBeInTheDocument();
  });

  // =========================================================================
  // Individual Sections
  // =========================================================================

  it('shows mechanics with definitions from key concepts', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    await user.click(screen.getByRole('button', { name: /next section/i }));
    // 'Trading' mechanic matches 'Trading' key concept
    expect(screen.getByText('Exchanging resources with other players')).toBeInTheDocument();
  });

  it('shows turn phases in order with optional badge', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Navigate to Phases (section index 2)
    await user.click(screen.getByRole('button', { name: /next section/i }));
    await user.click(screen.getByRole('button', { name: /next section/i }));

    expect(screen.getByTestId('section-phases')).toBeInTheDocument();
    expect(screen.getByText('Setup')).toBeInTheDocument();
    expect(screen.getByText('Trading Phase')).toBeInTheDocument();
    expect(screen.getByText('Building')).toBeInTheDocument();
    expect(screen.getByText('Optional')).toBeInTheDocument();
  });

  it('shows victory conditions with primary and alternatives', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Navigate to Victory (section index 3)
    for (let i = 0; i < 3; i++) {
      await user.click(screen.getByRole('button', { name: /next section/i }));
    }

    expect(screen.getByTestId('section-victory')).toBeInTheDocument();
    expect(screen.getByText('Reach 10 victory points')).toBeInTheDocument();
    expect(screen.getByText('Longest road')).toBeInTheDocument();
    expect(screen.getByText('Largest army')).toBeInTheDocument();
    expect(screen.getByText(/target: 10 points/)).toBeInTheDocument();
  });

  it('shows resources with limited badge', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Navigate to Resources (section index 4)
    for (let i = 0; i < 4; i++) {
      await user.click(screen.getByRole('button', { name: /next section/i }));
    }

    expect(screen.getByTestId('section-resources')).toBeInTheDocument();
    expect(screen.getByText('Wood')).toBeInTheDocument();
    expect(screen.getByText('Limited')).toBeInTheDocument();
    expect(screen.getByText('Building material')).toBeInTheDocument();
  });

  it('shows FAQ section with generated FAQs', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Navigate to FAQ (section index 5)
    for (let i = 0; i < 5; i++) {
      await user.click(screen.getByRole('button', { name: /next section/i }));
    }

    expect(screen.getByTestId('section-faq')).toBeInTheDocument();
    expect(screen.getByText('Can you trade with any player?')).toBeInTheDocument();
    expect(
      screen.getByText('Yes, during your turn you may trade with any player.')
    ).toBeInTheDocument();
  });

  it('disables Next button on last section', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Navigate to the last section (FAQ, index 5)
    for (let i = 0; i < 5; i++) {
      await user.click(screen.getByRole('button', { name: /next section/i }));
    }

    expect(screen.getByRole('button', { name: /next section/i })).toBeDisabled();
  });

  // =========================================================================
  // Empty sections are skipped
  // =========================================================================

  it('skips sections with no data', () => {
    const analysis = createMockAnalysis({
      gamePhases: [],
      victoryConditions: null,
    });
    render(<RulesExplainer analysis={analysis} />);

    // Should only show 4 sections: summary, mechanics, resources, faq
    expect(screen.getByText(/4 sections/)).toBeInTheDocument();
  });

  // =========================================================================
  // Timer
  // =========================================================================

  it('renders the timer by default', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} />);
    expect(screen.getByTestId('explanation-timer')).toBeInTheDocument();
    expect(screen.getByText('0:00')).toBeInTheDocument();
  });

  it('hides the timer when showTimer is false', () => {
    render(<RulesExplainer analysis={createMockAnalysis()} showTimer={false} />);
    expect(screen.queryByTestId('explanation-timer')).not.toBeInTheDocument();
  });

  it('starts and pauses the timer', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Start timer
    await user.click(screen.getByRole('button', { name: /start timer/i }));

    // Advance 3 seconds
    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.getByText('0:03')).toBeInTheDocument();

    // Pause timer
    await user.click(screen.getByRole('button', { name: /pause timer/i }));

    // Advance more time — should not change
    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(screen.getByText('0:03')).toBeInTheDocument();
  });

  it('resets the timer', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<RulesExplainer analysis={createMockAnalysis()} />);

    // Start timer
    await user.click(screen.getByRole('button', { name: /start timer/i }));

    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(screen.getByText('0:05')).toBeInTheDocument();

    // Reset timer
    await user.click(screen.getByRole('button', { name: /reset timer/i }));
    expect(screen.getByText('0:00')).toBeInTheDocument();
  });

  // =========================================================================
  // FAQ fallback to common questions
  // =========================================================================

  it('shows common questions when no generated FAQs', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const analysis = createMockAnalysis({
      generatedFaqs: [],
      commonQuestions: ['Can I trade on my first turn?', 'When does the game end?'],
    });
    render(<RulesExplainer analysis={analysis} />);

    // Navigate to FAQ (section index may differ since sections are filtered)
    const tabs = screen.getAllByRole('tab');
    const faqTab = tabs.find(tab => tab.textContent?.includes('FAQ'));
    expect(faqTab).toBeDefined();
    await user.click(faqTab!);

    expect(screen.getByText('Can I trade on my first turn?')).toBeInTheDocument();
    expect(screen.getByText('When does the game end?')).toBeInTheDocument();
  });
});
