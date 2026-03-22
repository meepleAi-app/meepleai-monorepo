/**
 * @vitest-environment jsdom
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { CardLinkChip } from '../sheet/CardLinkChip';

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('CardLinkChip', () => {
  it('renders the label', () => {
    render(<CardLinkChip icon="🎮" label="Scores" onClick={vi.fn()} />);
    expect(screen.getByText('Scores')).toBeInTheDocument();
  });

  it('renders the icon', () => {
    render(<CardLinkChip icon="🎮" label="Scores" onClick={vi.fn()} />);
    expect(screen.getByTestId('card-link-chip-icon')).toBeInTheDocument();
    expect(screen.getByTestId('card-link-chip-icon')).toHaveTextContent('🎮');
  });

  it('calls onClick when clicked', async () => {
    const onClick = vi.fn();
    render(<CardLinkChip icon="🎮" label="Scores" onClick={onClick} />);
    await userEvent.click(screen.getByTestId('card-link-chip'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders description when provided', () => {
    render(
      <CardLinkChip icon="🎮" label="Scores" description="Add player scores" onClick={vi.fn()} />
    );
    expect(screen.getByTestId('card-link-chip-description')).toBeInTheDocument();
    expect(screen.getByText('Add player scores')).toBeInTheDocument();
  });

  it('does not render description element when not provided', () => {
    render(<CardLinkChip icon="🎮" label="Scores" onClick={vi.fn()} />);
    expect(screen.queryByTestId('card-link-chip-description')).not.toBeInTheDocument();
  });

  it('renders the arrow indicator', () => {
    render(<CardLinkChip icon="🎮" label="Scores" onClick={vi.fn()} />);
    expect(screen.getByText('→')).toBeInTheDocument();
  });

  it('applies accentColor prop without throwing', () => {
    render(<CardLinkChip icon="🤖" label="AI Rules" accentColor="emerald" onClick={vi.fn()} />);
    expect(screen.getByTestId('card-link-chip')).toBeInTheDocument();
  });
});
