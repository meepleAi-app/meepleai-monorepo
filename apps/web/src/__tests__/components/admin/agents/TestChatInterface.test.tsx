/**
 * TestChatInterface Component Tests
 * Issue #3378
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TestChatInterface } from '@/components/admin/agents/TestChatInterface';

const mockResults = [
  {
    query: 'How do I build a settlement?',
    response: 'To build a settlement in Catan...',
    latency: 1.25,
    tokensUsed: 350,
    costEstimate: 0.0035,
    confidenceScore: 0.92,
    citations: [
      { page: 12, text: 'Section 4.2: Building' },
      { page: 15, text: 'Section 5.1: Resources' },
    ],
    timestamp: new Date('2026-02-03T10:00:00'),
  },
  {
    query: 'What resources are needed?',
    response: 'You need brick, lumber, wheat, and wool.',
    latency: 0.85,
    tokensUsed: 200,
    costEstimate: 0.002,
    confidenceScore: 0.78,
    citations: [{ page: 8, text: 'Section 3.1: Resources' }],
    timestamp: new Date('2026-02-03T09:55:00'),
  },
];

describe('TestChatInterface', () => {
  describe('Empty State', () => {
    it('renders empty state when no results', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={[]} onSave={onSave} />);

      expect(screen.getByText('Test History')).toBeInTheDocument();
      expect(
        screen.getByText('Your test queries and responses will appear here')
      ).toBeInTheDocument();
      expect(
        screen.getByText('No tests yet. Send a query to get started.')
      ).toBeInTheDocument();
    });
  });

  describe('With Results', () => {
    it('renders test count correctly', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      expect(screen.getByText('2 tests in this session')).toBeInTheDocument();
    });

    it('renders singular test count', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={[mockResults[0]]} onSave={onSave} />);

      expect(screen.getByText('1 test in this session')).toBeInTheDocument();
    });

    it('displays user queries', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      expect(
        screen.getByText('How do I build a settlement?')
      ).toBeInTheDocument();
      expect(screen.getByText('What resources are needed?')).toBeInTheDocument();
    });

    it('displays agent responses', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      expect(
        screen.getByText('To build a settlement in Catan...')
      ).toBeInTheDocument();
      expect(
        screen.getByText('You need brick, lumber, wheat, and wool.')
      ).toBeInTheDocument();
    });

    it('displays metrics badges', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      // Latency
      expect(screen.getByText('1.25s')).toBeInTheDocument();
      expect(screen.getByText('0.85s')).toBeInTheDocument();

      // Tokens
      expect(screen.getByText('350 tokens')).toBeInTheDocument();
      expect(screen.getByText('200 tokens')).toBeInTheDocument();

      // Cost
      expect(screen.getByText('$0.0035')).toBeInTheDocument();
      expect(screen.getByText('$0.0020')).toBeInTheDocument();

      // Confidence
      expect(screen.getByText('92%')).toBeInTheDocument();
      expect(screen.getByText('78%')).toBeInTheDocument();
    });

    it('displays citations', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      expect(screen.getByText('p.12')).toBeInTheDocument();
      expect(screen.getByText('p.15')).toBeInTheDocument();
      expect(screen.getByText('p.8')).toBeInTheDocument();
    });

    it('calls onSave when save button clicked', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      const saveButtons = screen.getAllByRole('button', { name: /save/i });
      fireEvent.click(saveButtons[0]);

      expect(onSave).toHaveBeenCalledWith(mockResults[0]);
    });

    it('displays timestamps', () => {
      const onSave = vi.fn();
      render(<TestChatInterface results={mockResults} onSave={onSave} />);

      // Timestamps are formatted by toLocaleTimeString, so exact format depends on locale
      // Just check that the time display is present
      expect(screen.getAllByText(/\d{1,2}:\d{2}/)).toHaveLength(2);
    });
  });
});
