import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GlassCard } from './GlassCard';

describe('GlassCard', () => {
  it('renders children', () => {
    render(<GlassCard>Hello</GlassCard>);
    expect(screen.getByText('Hello')).toBeInTheDocument();
  });

  it('applies glass-card class by default', () => {
    const { container } = render(<GlassCard>Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-card');
  });

  it('applies entity glow class when entity prop is provided', () => {
    const { container } = render(<GlassCard entity="game">Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-glow-game');
  });

  it('does not apply glow class when no entity', () => {
    const { container } = render(<GlassCard>Content</GlassCard>);
    expect(container.firstChild).not.toHaveClass('glass-glow-game');
  });

  it('merges custom className', () => {
    const { container } = render(<GlassCard className="p-4">Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-card');
    expect(container.firstChild).toHaveClass('p-4');
  });

  it('renders as a different element via as prop', () => {
    render(
      <GlassCard as="section" data-testid="glass">
        Content
      </GlassCard>
    );
    const el = screen.getByTestId('glass');
    expect(el.tagName).toBe('SECTION');
  });

  it('forwards ref', () => {
    const ref = { current: null } as React.RefObject<HTMLDivElement | null>;
    render(<GlassCard ref={ref}>Content</GlassCard>);
    expect(ref.current).toBeInstanceOf(HTMLElement);
  });
});
