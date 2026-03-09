/**
 * RulesSummary Tests — Issue #5583
 *
 * Test Coverage:
 * - Renders summary, mechanics, victory conditions
 * - Shows FAQ section
 * - Shows warning when no analysis available
 * - Confidence indicator
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { RulebookAnalysisDto } from '@/lib/api';

import { RulesSummary } from '../RulesSummary';

const mockAnalysis: RulebookAnalysisDto = {
  id: 'analysis-1',
  sharedGameId: 'game-1',
  pdfDocumentId: 'pdf-1',
  gameTitle: 'Catan',
  summary: 'A game about settling an island.',
  keyMechanics: ['Dice Rolling', 'Trading'],
  victoryConditions: {
    primary: 'First to 10 victory points',
    alternatives: ['Longest Road'],
    isPointBased: true,
    targetPoints: 10,
  },
  resources: [],
  gamePhases: [],
  commonQuestions: ['Can I trade on first turn?'],
  confidenceScore: 0.85,
  version: 1,
  isActive: true,
  source: 'pdf-1',
  analyzedAt: '2025-01-01T00:00:00Z',
  createdBy: 'user-1',
  keyConcepts: [],
  generatedFaqs: [
    {
      question: 'How many roads per turn?',
      answer: 'As many as you can afford.',
      sourceSection: 'Building',
      confidence: 0.9,
      tags: ['building'],
    },
  ],
  gameStateSchemaJson: null,
  completionStatus: 'Complete',
  missingSections: null,
};

describe('RulesSummary', () => {
  it('should render game summary', () => {
    render(<RulesSummary analysis={mockAnalysis} />);

    expect(screen.getByText('Sommario')).toBeInTheDocument();
    expect(screen.getByText('A game about settling an island.')).toBeInTheDocument();
  });

  it('should render key mechanics as badges', () => {
    render(<RulesSummary analysis={mockAnalysis} />);

    expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
  });

  it('should render victory conditions', () => {
    render(<RulesSummary analysis={mockAnalysis} />);

    expect(screen.getByText('Condizioni di Vittoria')).toBeInTheDocument();
    expect(screen.getByText('First to 10 victory points')).toBeInTheDocument();
    expect(screen.getByText('Longest Road')).toBeInTheDocument();
    expect(screen.getByText('A Punti')).toBeInTheDocument();
    expect(screen.getByText('Obiettivo: 10 punti')).toBeInTheDocument();
  });

  it('should render FAQs', () => {
    render(<RulesSummary analysis={mockAnalysis} />);

    expect(screen.getByText('How many roads per turn?')).toBeInTheDocument();
  });

  it('should show confidence indicator', () => {
    render(<RulesSummary analysis={mockAnalysis} />);

    expect(screen.getByText(/85%/)).toBeInTheDocument();
  });

  it('should show warning when analysis is null', () => {
    render(<RulesSummary analysis={null} gameTitle="Catan" />);

    expect(screen.getByText('Nessuna analisi disponibile')).toBeInTheDocument();
    expect(screen.getByText(/Catan.*non ha un regolamento analizzato/)).toBeInTheDocument();
  });

  it('should show generic warning when analysis is null and no title', () => {
    render(<RulesSummary analysis={null} />);

    expect(screen.getByText('Nessuna analisi disponibile')).toBeInTheDocument();
    expect(screen.getByText(/non ha un regolamento analizzato/)).toBeInTheDocument();
  });

  it('should show common questions when no generated FAQs', () => {
    const analysisNoFaqs = {
      ...mockAnalysis,
      generatedFaqs: [],
    };
    render(<RulesSummary analysis={analysisNoFaqs} />);

    expect(screen.getByText('Domande Comuni')).toBeInTheDocument();
    expect(screen.getByText('Can I trade on first turn?')).toBeInTheDocument();
  });
});
