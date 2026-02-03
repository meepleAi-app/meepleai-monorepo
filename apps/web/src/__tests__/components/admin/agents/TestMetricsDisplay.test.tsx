/**
 * TestMetricsDisplay Component Tests
 * Issue #3378
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TestMetricsDisplay } from '@/components/admin/agents/TestMetricsDisplay';

const mockResult = {
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
};

describe('TestMetricsDisplay', () => {
  describe('Metrics Grid', () => {
    it('renders latency correctly', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('Latency')).toBeInTheDocument();
      expect(screen.getByText('1.25s')).toBeInTheDocument();
    });

    it('renders tokens used correctly', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('Tokens Used')).toBeInTheDocument();
      expect(screen.getByText('350')).toBeInTheDocument();
    });

    it('renders cost correctly', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('Cost')).toBeInTheDocument();
      expect(screen.getByText('$0.0035')).toBeInTheDocument();
    });

    it('renders citations count correctly', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      // There are two "Citations" labels - one in metrics grid, one in citations list
      const citationsLabels = screen.getAllByText('Citations');
      expect(citationsLabels.length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('2')).toBeInTheDocument();
    });
  });

  describe('Confidence Score', () => {
    it('renders high confidence correctly (green)', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('Confidence Score')).toBeInTheDocument();
      expect(screen.getByText('92%')).toBeInTheDocument();
      expect(
        screen.getByText('High confidence - reliable response')
      ).toBeInTheDocument();
    });

    it('renders moderate confidence correctly (yellow)', () => {
      const moderateResult = { ...mockResult, confidenceScore: 0.75 };
      render(<TestMetricsDisplay result={moderateResult} />);

      expect(screen.getByText('75%')).toBeInTheDocument();
      expect(
        screen.getByText('Moderate confidence - review recommended')
      ).toBeInTheDocument();
    });

    it('renders low confidence correctly (red)', () => {
      const lowResult = { ...mockResult, confidenceScore: 0.55 };
      render(<TestMetricsDisplay result={lowResult} />);

      expect(screen.getByText('55%')).toBeInTheDocument();
      expect(
        screen.getByText('Low confidence - response may be inaccurate')
      ).toBeInTheDocument();
    });

    it('applies correct color class for high confidence', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      const percentElement = screen.getByText('92%');
      expect(percentElement).toHaveClass('text-green-500');
    });

    it('applies correct color class for moderate confidence', () => {
      const moderateResult = { ...mockResult, confidenceScore: 0.75 };
      render(<TestMetricsDisplay result={moderateResult} />);

      const percentElement = screen.getByText('75%');
      expect(percentElement).toHaveClass('text-yellow-500');
    });

    it('applies correct color class for low confidence', () => {
      const lowResult = { ...mockResult, confidenceScore: 0.55 };
      render(<TestMetricsDisplay result={lowResult} />);

      const percentElement = screen.getByText('55%');
      expect(percentElement).toHaveClass('text-red-500');
    });
  });

  describe('Citations List', () => {
    it('renders citations when present', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('p.12')).toBeInTheDocument();
      expect(screen.getByText('Section 4.2: Building')).toBeInTheDocument();
      expect(screen.getByText('p.15')).toBeInTheDocument();
      expect(screen.getByText('Section 5.1: Resources')).toBeInTheDocument();
    });

    it('does not render citations section when empty', () => {
      const noCitationsResult = { ...mockResult, citations: [] };
      render(<TestMetricsDisplay result={noCitationsResult} />);

      // Should not have the citations section label (there are two "Citations" - one in grid, one in list)
      const citationsLabels = screen.getAllByText('Citations');
      expect(citationsLabels).toHaveLength(1); // Only the one in the metrics grid
    });
  });

  describe('Save Button', () => {
    it('renders save button when onSave provided', () => {
      const onSave = vi.fn();
      render(<TestMetricsDisplay result={mockResult} onSave={onSave} />);

      expect(
        screen.getByRole('button', { name: /save to history/i })
      ).toBeInTheDocument();
    });

    it('does not render save button when onSave not provided', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(
        screen.queryByRole('button', { name: /save to history/i })
      ).not.toBeInTheDocument();
    });

    it('calls onSave when clicked', () => {
      const onSave = vi.fn();
      render(<TestMetricsDisplay result={mockResult} onSave={onSave} />);

      fireEvent.click(
        screen.getByRole('button', { name: /save to history/i })
      );

      expect(onSave).toHaveBeenCalledTimes(1);
    });
  });

  describe('Header', () => {
    it('renders title correctly', () => {
      render(<TestMetricsDisplay result={mockResult} />);

      expect(screen.getByText('Latest Result Metrics')).toBeInTheDocument();
    });
  });
});
