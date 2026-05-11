import { createRef } from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuthCard } from './auth-card';

describe('AuthCard', () => {
  it('renders title and children', () => {
    render(
      <AuthCard title="Bentornato">
        <span>form content</span>
      </AuthCard>
    );
    expect(screen.getByRole('heading', { name: 'Bentornato' })).toBeInTheDocument();
    expect(screen.getByText('form content')).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(
      <AuthCard title="Bentornato" subtitle="Accedi al tuo account MeepleAI">
        <span>x</span>
      </AuthCard>
    );
    expect(screen.getByText('Accedi al tuo account MeepleAI')).toBeInTheDocument();
  });

  it('does NOT render subtitle when omitted', () => {
    const { container } = render(
      <AuthCard title="Bentornato">
        <span>x</span>
      </AuthCard>
    );
    // No <p> tag from subtitle should exist; children only contain <span>
    expect(container.querySelector('p')).toBeNull();
  });

  it('renders footerAction when provided', () => {
    render(
      <AuthCard
        title="Bentornato"
        footerAction={<a href="/register">Non hai un account? Registrati</a>}
      >
        <span>x</span>
      </AuthCard>
    );
    expect(
      screen.getByRole('link', { name: 'Non hai un account? Registrati' })
    ).toBeInTheDocument();
  });

  it('shows brand block by default (MeepleAI wordmark visible)', () => {
    render(
      <AuthCard title="Bentornato">
        <span>x</span>
      </AuthCard>
    );
    expect(screen.getByText('MeepleAI')).toBeInTheDocument();
  });

  it('hides brand block when showBrand={false}', () => {
    render(
      <AuthCard title="Bentornato" showBrand={false}>
        <span>x</span>
      </AuthCard>
    );
    expect(screen.queryByText('MeepleAI')).toBeNull();
  });

  it('merges className onto root element', () => {
    const { container } = render(
      <AuthCard title="T" className="custom-xyz">
        <span>x</span>
      </AuthCard>
    );
    const root = container.firstElementChild as HTMLElement;
    expect(root.className).toMatch(/custom-xyz/);
  });

  it('forwards ref to the root element', () => {
    const ref = createRef<HTMLDivElement>();
    render(
      <AuthCard ref={ref} title="T">
        <span>x</span>
      </AuthCard>
    );
    expect(ref.current).toBeInstanceOf(HTMLDivElement);
  });

  it('brand-mark has linear-gradient inline style using entity color tokens', () => {
    const { container } = render(
      <AuthCard title="T">
        <span>x</span>
      </AuthCard>
    );
    const brandMark = container.querySelector<HTMLElement>('[data-testid="auth-card-brand-mark"]');
    expect(brandMark).not.toBeNull();
    const bg = brandMark?.style.background ?? '';
    expect(bg).toMatch(/linear-gradient/);
    expect(bg).toMatch(/--e-game/);
    expect(bg).toMatch(/--e-event/);
    expect(bg).toMatch(/--e-player/);
  });
});
