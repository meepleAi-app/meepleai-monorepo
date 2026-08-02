/**
 * @vitest-environment jsdom
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CoverFocalPointPicker } from '../CoverFocalPointPicker';

function renderPicker(overrides: Partial<React.ComponentProps<typeof CoverFocalPointPicker>> = {}) {
  const onChange = vi.fn();
  const props = {
    imageUrl: 'https://r2.example/preview.webp',
    alt: 'Anteprima candidato',
    x: 0.5,
    y: 0.5,
    onChange,
    label: 'Punto focale copertina',
    ...overrides,
  };
  const utils = render(<CoverFocalPointPicker {...props} />);
  return { onChange, ...utils };
}

describe('CoverFocalPointPicker', () => {
  it('renders the candidate preview image with its alt text', () => {
    renderPicker();
    expect(screen.getByRole('img', { name: 'Anteprima candidato' })).toHaveAttribute(
      'src',
      'https://r2.example/preview.webp'
    );
  });

  it('positions the handle at the current focal value', () => {
    renderPicker({ x: 0.25, y: 0.75 });
    const handle = screen.getByRole('button', { name: /punto focale/i });
    expect(handle.style.left).toBe('25%');
    expect(handle.style.top).toBe('75%');
  });

  it('exposes the handle as a focusable button with an accessible name', () => {
    renderPicker();
    const handle = screen.getByRole('button', { name: /punto focale/i });
    expect(handle.tabIndex).toBe(0);
  });

  it('moves the focal point right by one step on ArrowRight', () => {
    const { onChange } = renderPicker({ x: 0.5, y: 0.5 });
    fireEvent.keyDown(screen.getByRole('button', { name: /punto focale/i }), { key: 'ArrowRight' });
    expect(onChange).toHaveBeenCalledWith({ x: 0.55, y: 0.5 });
  });

  it('moves the focal point down by one step on ArrowDown and up on ArrowUp', () => {
    const { onChange } = renderPicker({ x: 0.5, y: 0.5 });
    const handle = screen.getByRole('button', { name: /punto focale/i });
    fireEvent.keyDown(handle, { key: 'ArrowDown' });
    expect(onChange).toHaveBeenCalledWith({ x: 0.5, y: 0.55 });
    onChange.mockClear();
    fireEvent.keyDown(handle, { key: 'ArrowUp' });
    expect(onChange).toHaveBeenCalledWith({ x: 0.5, y: 0.45 });
  });

  it('clamps at the boundary and does not emit when already at the edge', () => {
    const { onChange } = renderPicker({ x: 0, y: 0 });
    fireEvent.keyDown(screen.getByRole('button', { name: /punto focale/i }), { key: 'ArrowLeft' });
    expect(onChange).not.toHaveBeenCalled();
  });

  it('sets the focal point from a pointer click, normalized by the area rect', () => {
    const { onChange } = renderPicker();
    const area = screen.getByTestId('focal-area');
    vi.spyOn(area, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      top: 0,
      width: 200,
      height: 300,
      right: 200,
      bottom: 300,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    } as DOMRect);

    fireEvent.pointerDown(area, { clientX: 150, clientY: 75 });

    expect(onChange).toHaveBeenCalledWith({ x: 0.75, y: 0.25 });
  });

  it('does not emit when disabled', () => {
    const { onChange } = renderPicker({ disabled: true });
    fireEvent.keyDown(screen.getByRole('button', { name: /punto focale/i }), { key: 'ArrowRight' });
    const area = screen.getByTestId('focal-area');
    fireEvent.pointerDown(area, { clientX: 10, clientY: 10 });
    expect(onChange).not.toHaveBeenCalled();
  });
});
