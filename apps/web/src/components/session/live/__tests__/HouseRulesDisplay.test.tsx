/**
 * HouseRulesDisplay Tests
 *
 * AgentMemory — Task 25
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { HouseRulesDisplay, type HouseRule } from '../HouseRulesDisplay';

describe('HouseRulesDisplay', () => {
  const sampleRules: HouseRule[] = [
    { description: 'No kingmaking', addedAt: '2026-01-15T10:00:00Z', source: 'UserAdded' },
    {
      description: 'Timer override allowed',
      addedAt: '2026-01-16T12:00:00Z',
      source: 'DisputeOverride',
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders empty state when no rules', () => {
    render(<HouseRulesDisplay gameId="game-1" rules={[]} />);
    expect(screen.getByText('No house rules yet')).toBeInTheDocument();
  });

  it('renders the header with rule count', () => {
    render(<HouseRulesDisplay gameId="game-1" rules={sampleRules} />);
    expect(screen.getByText('House Rules')).toBeInTheDocument();
    expect(screen.getByText('(2)')).toBeInTheDocument();
  });

  it('renders rule descriptions', () => {
    render(<HouseRulesDisplay gameId="game-1" rules={sampleRules} />);
    expect(screen.getByText('No kingmaking')).toBeInTheDocument();
    expect(screen.getByText('Timer override allowed')).toBeInTheDocument();
  });

  it('renders source badges (amber for UserAdded, red for DisputeOverride)', () => {
    render(<HouseRulesDisplay gameId="game-1" rules={sampleRules} />);
    expect(screen.getByText('Added')).toBeInTheDocument();
    expect(screen.getByText('Dispute')).toBeInTheDocument();
  });

  it('does not show add input when onAddRule is not provided', () => {
    render(<HouseRulesDisplay gameId="game-1" rules={[]} />);
    expect(screen.queryByLabelText('New house rule')).not.toBeInTheDocument();
  });

  it('shows add input when onAddRule is provided', () => {
    const onAdd = vi.fn();
    render(<HouseRulesDisplay gameId="game-1" rules={[]} onAddRule={onAdd} />);
    expect(screen.getByLabelText('New house rule')).toBeInTheDocument();
    expect(screen.getByText('Add')).toBeInTheDocument();
  });

  it('calls onAddRule with trimmed value when Add is clicked', () => {
    const onAdd = vi.fn();
    render(<HouseRulesDisplay gameId="game-1" rules={[]} onAddRule={onAdd} />);

    const input = screen.getByLabelText('New house rule');
    fireEvent.change(input, { target: { value: '  New rule  ' } });
    fireEvent.click(screen.getByText('Add'));

    expect(onAdd).toHaveBeenCalledWith('New rule');
  });

  it('calls onAddRule on Enter key press', () => {
    const onAdd = vi.fn();
    render(<HouseRulesDisplay gameId="game-1" rules={[]} onAddRule={onAdd} />);

    const input = screen.getByLabelText('New house rule');
    fireEvent.change(input, { target: { value: 'Enter rule' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onAdd).toHaveBeenCalledWith('Enter rule');
  });

  it('clears input after adding a rule', () => {
    const onAdd = vi.fn();
    render(<HouseRulesDisplay gameId="game-1" rules={[]} onAddRule={onAdd} />);

    const input = screen.getByLabelText('New house rule') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'My rule' } });
    fireEvent.click(screen.getByText('Add'));

    expect(input.value).toBe('');
  });

  it('disables Add button when input is empty', () => {
    const onAdd = vi.fn();
    render(<HouseRulesDisplay gameId="game-1" rules={[]} onAddRule={onAdd} />);

    const addButton = screen.getByText('Add').closest('button');
    expect(addButton).toBeDisabled();
  });
});
