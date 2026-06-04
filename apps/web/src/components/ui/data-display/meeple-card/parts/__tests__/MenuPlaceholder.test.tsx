import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { MenuPlaceholder } from '../MenuPlaceholder';

describe('MenuPlaceholder', () => {
  it('renders a button with aria-label="Azioni"', () => {
    render(<MenuPlaceholder />);
    expect(screen.getByRole('button', { name: 'Azioni' })).toBeInTheDocument();
  });

  it('renders the ⋯ glyph', () => {
    render(<MenuPlaceholder />);
    expect(screen.getByRole('button', { name: 'Azioni' }).textContent).toContain('⋯');
  });

  it('starts hidden (opacity-0) and becomes visible on parent group-hover', () => {
    render(<MenuPlaceholder />);
    const btn = screen.getByRole('button', { name: 'Azioni' });
    expect(btn.className).toMatch(/\bopacity-0\b/);
    expect(btn.className).toMatch(/group-hover:opacity-100/);
    expect(btn.className).toMatch(/transition-opacity/);
  });

  it('positions absolute top-2 right-2 with glass style', () => {
    render(<MenuPlaceholder />);
    const btn = screen.getByRole('button', { name: 'Azioni' });
    expect(btn.className).toMatch(/\babsolute\b/);
    expect(btn.className).toMatch(/\btop-2\b/);
    expect(btn.className).toMatch(/\bright-2\b/);
    expect(btn.className).toMatch(/bg-white\/85/);
    expect(btn.className).toMatch(/backdrop-blur-md/);
  });

  it('stops click event propagation (prevents triggering parent card onClick)', () => {
    const parentClick = vi.fn();
    render(
      <div onClick={parentClick}>
        <MenuPlaceholder />
      </div>
    );
    fireEvent.click(screen.getByRole('button', { name: 'Azioni' }));
    expect(parentClick).not.toHaveBeenCalled();
  });
});
