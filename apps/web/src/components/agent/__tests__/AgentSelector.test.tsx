/**
 * AgentSelector Tests (Task #4)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AgentSelector } from '../AgentSelector';

describe('AgentSelector', () => {
  const mockOnChange = vi.fn();

  const defaultProps = {
    value: 'auto' as const,
    onChange: mockOnChange,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders agent selector dropdown', () => {
    render(<AgentSelector {...defaultProps} />);

    expect(screen.getByRole('combobox')).toBeInTheDocument();
    expect(screen.getByText(/Agent:/i)).toBeInTheDocument();
  });

  it('shows all agent options when opened', async () => {
    render(<AgentSelector {...defaultProps} />);

    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    await waitFor(() => {
      // Use getAllByText since elements appear in both trigger and options
      const autoElements = screen.getAllByText(/Auto \(Orchestrator\)/i);
      expect(autoElements.length).toBeGreaterThan(0);
    });
  });

  it('displays agent descriptions', async () => {
    render(<AgentSelector {...defaultProps} />);

    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    await waitFor(() => {
      // Use getAllByText for descriptions that may appear multiple times
      const autoDesc = screen.getAllByText(/Automatically routes to best agent/i);
      expect(autoDesc.length).toBeGreaterThan(0);
    });
  });

  it('shows status indicators for each agent', async () => {
    render(<AgentSelector {...defaultProps} />);

    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    await waitFor(() => {
      // Should show "Online" status for all agents in POC
      const statusLabels = screen.getAllByText('Online');
      expect(statusLabels.length).toBeGreaterThan(0);
    });
  });

  it('calls onChange when agent is selected', async () => {
    render(<AgentSelector {...defaultProps} />);

    const trigger = screen.getByRole('combobox');
    fireEvent.click(trigger);

    await waitFor(() => {
      const tutorOption = screen.getByText('Tutor');
      fireEvent.click(tutorOption);
    });

    expect(mockOnChange).toHaveBeenCalledWith('tutor');
  });

  it('shows badge for selected agent (non-auto)', () => {
    render(<AgentSelector {...defaultProps} value="tutor" />);

    // Badge should be visible (multiple Tutor text elements expected)
    const tutorElements = screen.getAllByText('Tutor');
    expect(tutorElements.length).toBeGreaterThan(0);
  });

  it('can be disabled', () => {
    render(<AgentSelector {...defaultProps} disabled />);

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });
});
