/**
 * EmptyGamebooks unit tests — SP6 Phase B Task 2 (Issue #788).
 *
 * Coverage:
 *   - data-slot identity
 *   - role="status" on container
 *   - title + description + CTA from labels
 *   - illustration is decorative (aria-hidden="true")
 *   - CTA fires onAddManualClick
 *   - CTA aria-label
 *   - custom className composition
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyGamebooks } from '../EmptyGamebooks';
import type { EmptyGamebooksProps } from '../EmptyGamebooks';

const LABELS: EmptyGamebooksProps['labels'] = {
  title: 'Nessun manuale ancora',
  description: 'Fotografa il tuo primo gamebook per iniziare.',
  cta: '📷 Scatta il primo manuale',
};

const DEFAULT_PROPS: EmptyGamebooksProps = {
  onAddManualClick: vi.fn(),
  labels: LABELS,
};

describe('EmptyGamebooks', () => {
  it('renders data-slot="empty-gamebooks" with role="status"', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} />);
    const el = document.querySelector('[data-slot="empty-gamebooks"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('role')).toBe('status');
  });

  it('renders title from labels as H2', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} />);
    const heading = document.querySelector('h2[data-slot="empty-gamebooks-title"]');
    expect(heading).not.toBeNull();
    expect(heading!.textContent).toBe('Nessun manuale ancora');
  });

  it('renders description', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} />);
    expect(screen.getByText('Fotografa il tuo primo gamebook per iniziare.')).toBeTruthy();
  });

  it('renders CTA button with correct label and aria-label', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} />);
    const cta = document.querySelector('[data-slot="empty-gamebooks-cta"]');
    expect(cta).not.toBeNull();
    expect(cta!.textContent).toBe('📷 Scatta il primo manuale');
    expect(cta!.getAttribute('aria-label')).toBe('📷 Scatta il primo manuale');
  });

  it('illustration is decorative (aria-hidden="true")', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} />);
    const illustration = document.querySelector('[data-slot="empty-gamebooks-illustration"]');
    expect(illustration).not.toBeNull();
    expect(illustration!.getAttribute('aria-hidden')).toBe('true');
  });

  it('fires onAddManualClick when CTA clicked', () => {
    const onAddManualClick = vi.fn();
    render(<EmptyGamebooks {...DEFAULT_PROPS} onAddManualClick={onAddManualClick} />);
    fireEvent.click(document.querySelector('[data-slot="empty-gamebooks-cta"]')!);
    expect(onAddManualClick).toHaveBeenCalledOnce();
  });

  it('applies custom className to outer container', () => {
    render(<EmptyGamebooks {...DEFAULT_PROPS} className="my-extra-class" />);
    const el = document.querySelector('[data-slot="empty-gamebooks"]');
    expect(el!.classList.contains('my-extra-class')).toBe(true);
  });
});
