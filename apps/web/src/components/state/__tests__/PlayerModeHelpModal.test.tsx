/**
 * Unit tests for PlayerModeHelpModal - Issue #2475
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { PlayerModeHelpModal } from '../PlayerModeHelpModal';

describe('PlayerModeHelpModal', () => {
  it('should render trigger button', () => {
    render(<PlayerModeHelpModal />);

    const trigger = screen.getByRole('button');
    expect(trigger).toBeInTheDocument();
  });

  it('should render with custom variant and size', () => {
    render(<PlayerModeHelpModal variant="default" size="lg" />);

    const trigger = screen.getByRole('button');
    expect(trigger).toBeInTheDocument();
    expect(trigger).toHaveTextContent('Aiuto');
  });

  it('should render with icon only when size is icon', () => {
    render(<PlayerModeHelpModal size="icon" />);

    const trigger = screen.getByRole('button');
    expect(trigger).toBeInTheDocument();
    expect(trigger).not.toHaveTextContent('Aiuto');
  });

  it('should render custom trigger element', () => {
    render(
      <PlayerModeHelpModal>
        <button>Custom Trigger</button>
      </PlayerModeHelpModal>
    );

    expect(screen.getByText('Custom Trigger')).toBeInTheDocument();
  });
});
