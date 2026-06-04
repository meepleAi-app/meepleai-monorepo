import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EditCoverOverlay } from '../EditCoverOverlay';

describe('EditCoverOverlay', () => {
  it('renders edit icon with accessible label', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={false} />);
    const btn = screen.getByRole('button', { name: /modifica copertina/i });
    expect(btn).toBeInTheDocument();
  });

  it('calls onEditClick when clicked', () => {
    const onEditClick = vi.fn();
    render(<EditCoverOverlay onEditClick={onEditClick} hasCustomCover={false} />);
    fireEvent.click(screen.getByRole('button', { name: /modifica copertina/i }));
    expect(onEditClick).toHaveBeenCalledOnce();
  });

  it('shows different label when custom cover already exists', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={true} />);
    const btn = screen.getByRole('button', { name: /cambia copertina/i });
    expect(btn).toBeInTheDocument();
  });

  it('has tabIndex 0 for keyboard accessibility', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={false} />);
    const btn = screen.getByRole('button', { name: /modifica copertina/i });
    expect(btn.tabIndex).toBe(0);
  });
});
