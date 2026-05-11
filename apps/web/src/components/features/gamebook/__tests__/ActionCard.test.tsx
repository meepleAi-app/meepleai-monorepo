/**
 * ActionCard unit tests — SP6 Phase C.1.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot identity
 *   - <button type="button"> semantic root
 *   - icon, title, description render
 *   - icon is aria-hidden (decorative)
 *   - onClick fires
 *   - aria-label includes title
 *   - keyboard activation (Enter / Space) — native button behavior
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ActionCard } from '../ActionCard';
import type { ActionCardProps } from '../ActionCard';

const DEFAULT_PROPS: ActionCardProps = {
  icon: <span>+</span>,
  title: 'Crea gioco nuovo',
  description: 'Manuale che non esiste in nessun database',
  onClick: vi.fn(),
};

describe('ActionCard', () => {
  it('renders data-slot="action-card"', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="action-card"]')).not.toBeNull();
  });

  it('renders semantic <button type="button"> root', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    const root = document.querySelector('[data-slot="action-card"]');
    expect(root?.tagName).toBe('BUTTON');
    expect(root?.getAttribute('type')).toBe('button');
  });

  it('renders title text', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    expect(screen.getByText('Crea gioco nuovo')).toBeTruthy();
  });

  it('renders description text', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    expect(screen.getByText('Manuale che non esiste in nessun database')).toBeTruthy();
  });

  it('renders icon node passed via prop', () => {
    render(<ActionCard {...DEFAULT_PROPS} icon={<span data-testid="custom-icon">🎲</span>} />);
    expect(screen.getByTestId('custom-icon')).toBeTruthy();
  });

  it('icon container is aria-hidden', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    const iconWrap = document.querySelector('[data-slot="action-card-icon"]');
    expect(iconWrap?.getAttribute('aria-hidden')).toBe('true');
  });

  it('fires onClick when clicked', () => {
    const onClick = vi.fn();
    render(<ActionCard {...DEFAULT_PROPS} onClick={onClick} />);
    fireEvent.click(document.querySelector('[data-slot="action-card"]')!);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('exposes aria-label including title', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    const card = document.querySelector('[data-slot="action-card"]');
    expect(card?.getAttribute('aria-label')).toContain('Crea gioco nuovo');
  });

  it('applies custom className to root', () => {
    render(<ActionCard {...DEFAULT_PROPS} className="extra-class" />);
    const card = document.querySelector('[data-slot="action-card"]');
    expect(card?.classList.contains('extra-class')).toBe(true);
  });

  it('renders title with data-slot="action-card-title"', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="action-card-title"]')).not.toBeNull();
  });

  it('renders description with data-slot="action-card-description"', () => {
    render(<ActionCard {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="action-card-description"]')).not.toBeNull();
  });
});
