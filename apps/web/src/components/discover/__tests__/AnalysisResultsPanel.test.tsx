import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

import { AnalysisResultsPanel } from '../AnalysisResultsPanel';

/**
 * Tests for AnalysisResultsPanel.
 * Issue #5454: Analysis results UI.
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
      { name: 'Trading', description: 'Exchange resources', order: 2, isOptional: true },
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
      { term: 'Settlement', definition: 'A small building worth 1 VP', category: 'Buildings' },
      {
        term: 'Road',
        definition: 'A connection between intersections',
        category: 'Infrastructure',
      },
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
    gameStateSchemaJson: '{"type":"object","properties":{"score":{"type":"number"}}}',
    completionStatus: 'Complete',
    missingSections: null,
    ...overrides,
  };
}

describe('AnalysisResultsPanel', () => {
  it('renders nothing when analyses is empty', () => {
    const { container } = render(<AnalysisResultsPanel analyses={[]} />);
    expect(container.innerHTML).toBe('');
  });

  it('renders panel with heading and confidence badge', () => {
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    expect(screen.getByText('AI Analysis')).toBeInTheDocument();
    expect(screen.getByText('92%')).toBeInTheDocument();
  });

  it('renders summary text', () => {
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    expect(screen.getByText('A strategy game about building settlements.')).toBeInTheDocument();
  });

  it('shows mechanics tab by default with key mechanics', () => {
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    expect(screen.getByText('Trading')).toBeInTheDocument();
    expect(screen.getByText('Resource Management')).toBeInTheDocument();
    expect(screen.getByText('Area Control')).toBeInTheDocument();
  });

  it('shows victory conditions on mechanics tab', () => {
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    expect(screen.getByText('Reach 10 victory points')).toBeInTheDocument();
    expect(screen.getByText('Longest road')).toBeInTheDocument();
  });

  it('switches to phases tab', async () => {
    const user = userEvent.setup();
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    await user.click(screen.getByRole('tab', { name: /phases/i }));
    expect(screen.getByText('Setup')).toBeInTheDocument();
    expect(screen.getByText('Place initial settlements')).toBeInTheDocument();
    expect(screen.getByText('Exchange resources')).toBeInTheDocument();
  });

  it('switches to FAQ tab and shows generated FAQs with confidence', async () => {
    const user = userEvent.setup();
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    await user.click(screen.getByRole('tab', { name: /faq/i }));
    expect(screen.getByText('Can you trade with any player?')).toBeInTheDocument();
    expect(screen.getByText('88%')).toBeInTheDocument();
  });

  it('switches to glossary tab and shows grouped concepts', async () => {
    const user = userEvent.setup();
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    await user.click(screen.getByRole('tab', { name: /glossary/i }));
    expect(screen.getByText('Settlement')).toBeInTheDocument();
    expect(screen.getByText('A small building worth 1 VP')).toBeInTheDocument();
    expect(screen.getByText('Buildings')).toBeInTheDocument();
    expect(screen.getByText('Infrastructure')).toBeInTheDocument();
  });

  it('switches to state tab and shows completion status', async () => {
    const user = userEvent.setup();
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    await user.click(screen.getByRole('tab', { name: /state/i }));
    expect(screen.getByText('Complete')).toBeInTheDocument();
  });

  it('shows missing sections warning on state tab', async () => {
    const user = userEvent.setup();
    render(
      <AnalysisResultsPanel
        analyses={[
          createMockAnalysis({
            completionStatus: 'PartiallyComplete',
            missingSections: ['VictoryConditions', 'Resources'],
          }),
        ]}
      />
    );
    await user.click(screen.getByRole('tab', { name: /state/i }));
    expect(screen.getByText('Missing Sections')).toBeInTheDocument();
    expect(screen.getByText('VictoryConditions')).toBeInTheDocument();
    expect(screen.getByText('Resources')).toBeInTheDocument();
  });

  it('shows game state schema JSON on state tab', async () => {
    const user = userEvent.setup();
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    await user.click(screen.getByRole('tab', { name: /state/i }));
    expect(screen.getByText(/Game State Schema/)).toBeInTheDocument();
  });

  it('shows resources with limited badge', () => {
    render(<AnalysisResultsPanel analyses={[createMockAnalysis()]} />);
    expect(screen.getByText('Wood')).toBeInTheDocument();
    expect(screen.getByText('Limited')).toBeInTheDocument();
  });
});
