import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { CardFooter } from '../CardFooter';

describe('CardFooter', () => {
  it('renders nothing when both status and badge are absent', () => {
    const { container } = render(<CardFooter />);
    expect(container.firstChild).toBeNull();
  });

  it('renders status text when status is provided', () => {
    render(<CardFooter status="owned" />);
    expect(screen.getByText(/owned/i)).toBeInTheDocument();
  });

  it('renders badge text in uppercase when badge is provided', () => {
    const { container } = render(<CardFooter badge="indexed" />);
    const text = container.textContent ?? '';
    expect(text.toLowerCase()).toContain('indexed');
    // The element rendering the badge uses uppercase via class.
    const badgeEl = container.querySelector('[data-slot="footer-badge"]');
    expect(badgeEl?.className).toMatch(/uppercase/);
  });

  it('prefers badge over status as the displayed label when both present', () => {
    const { container } = render(<CardFooter status="owned" badge="indexed" />);
    const badgeEl = container.querySelector('[data-slot="footer-badge"]');
    expect(badgeEl?.textContent?.toLowerCase()).toContain('indexed');
    expect(badgeEl?.textContent?.toLowerCase()).not.toContain('owned');
  });

  it('uses border-top divider class', () => {
    const { container } = render(<CardFooter status="owned" />);
    const root = container.firstChild as HTMLElement;
    expect(root.className).toMatch(/border-t\b/);
  });

  it('renders a StatusDot when status is provided', () => {
    const { container } = render(<CardFooter status="owned" />);
    const dot = container.querySelector('[data-slot="footer-status-dot"]');
    expect(dot).not.toBeNull();
  });
});
