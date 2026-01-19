/**
 * Tests for MeepleLogo component
 * Issue #1951: Add coverage for logo SVG component
 */

import { render } from '@testing-library/react';
import { MeepleLogo } from '../meeple/meeple-logo';

describe('MeepleLogo', () => {
  it('renders SVG logo', () => {
    const { container } = render(<MeepleLogo />);
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('renders path elements', () => {
    const { container } = render(<MeepleLogo />);
    const paths = container.querySelectorAll('path');
    expect(paths.length).toBeGreaterThan(0);
  });

  it('renders without errors', () => {
    const { container } = render(<MeepleLogo />);
    expect(container.firstChild).toBeInTheDocument();
  });
});
