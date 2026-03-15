import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { HoloOverlay } from '../HoloOverlay';

describe('HoloOverlay', () => {
  it('renders three holo layers', () => {
    const { container } = render(<HoloOverlay />);
    expect(container.querySelector('.holo-rainbow')).toBeTruthy();
    expect(container.querySelector('.holo-sparkle')).toBeTruthy();
    expect(container.querySelector('.holo-border')).toBeTruthy();
  });

  it('all layers have pointer-events-none', () => {
    const { container } = render(<HoloOverlay />);
    const layers = container.querySelectorAll('[class*="holo-"]');
    layers.forEach(layer => {
      expect(layer.className).toContain('pointer-events-none');
    });
  });

  it('renders nothing when disabled', () => {
    const { container } = render(<HoloOverlay disabled />);
    expect(container.querySelector('.holo-rainbow')).toBeNull();
  });
});
