import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KPIStatGrid, KPIStatCard } from './index';

describe('KPIStatGrid', () => {
  it('renders children inside a grid with role="list"', () => {
    render(
      <KPIStatGrid>
        <KPIStatCard label="Test" value="42" />
      </KPIStatGrid>
    );
    const grid = screen.getByRole('list');
    expect(grid).toBeInTheDocument();
    expect(grid.className).toContain('grid');
  });

  it('defaults to 4 columns responsive (2 cols mobile, 4 cols desktop)', () => {
    render(
      <KPIStatGrid>
        <KPIStatCard label="A" value="1" />
      </KPIStatGrid>
    );
    const grid = screen.getByRole('list');
    expect(grid.className).toContain('grid-cols-2');
    expect(grid.className).toContain('sm:grid-cols-4');
  });

  it('accepts columns=3 prop', () => {
    render(
      <KPIStatGrid columns={3}>
        <KPIStatCard label="A" value="1" />
      </KPIStatGrid>
    );
    const grid = screen.getByRole('list');
    expect(grid.className).toContain('sm:grid-cols-3');
  });

  it('accepts columns=2 prop (mobile-equivalent)', () => {
    render(
      <KPIStatGrid columns={2}>
        <KPIStatCard label="A" value="1" />
      </KPIStatGrid>
    );
    const grid = screen.getByRole('list');
    expect(grid.className).toContain('grid-cols-2');
    expect(grid.className).not.toContain('sm:grid-cols-4');
  });

  it('accepts custom className override', () => {
    render(
      <KPIStatGrid className="custom-class">
        <KPIStatCard label="A" value="1" />
      </KPIStatGrid>
    );
    expect(screen.getByRole('list').className).toContain('custom-class');
  });
});

describe('KPIStatCard', () => {
  it('renders label, value, sub', () => {
    render(<KPIStatCard label="Sessioni" value={3} sub="3 game completati" />);
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('3 game completati')).toBeInTheDocument();
  });

  it('renders icon when provided (decorative aria-hidden)', () => {
    render(<KPIStatCard label="Sessioni" value="3" icon="🎯" />);
    const icon = screen.getByText('🎯');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('omits sub element when sub prop missing', () => {
    const { container } = render(<KPIStatCard label="A" value="1" />);
    expect(container.querySelectorAll('.font-mono').length).toBe(1);
  });

  it('omits icon span when icon prop missing', () => {
    const { container } = render(<KPIStatCard label="A" value="1" />);
    expect(container.querySelector('[aria-hidden="true"]')).toBeNull();
  });

  it('defaults to session tone', () => {
    const { container } = render(<KPIStatCard label="A" value="1" />);
    const card = container.firstChild as HTMLElement;
    expect(card.className).toContain('border-t-entity-session');
  });

  it.each([
    ['event', 'border-t-entity-event', 'text-entity-event'],
    ['player', 'border-t-entity-player', 'text-entity-player'],
    ['chat', 'border-t-entity-chat', 'text-entity-chat'],
    ['toolkit', 'border-t-entity-toolkit', 'text-entity-toolkit'],
    ['game', 'border-t-entity-game', 'text-entity-game'],
    ['kb', 'border-t-entity-document', 'text-entity-document'],
  ] as const)('applies tone=%s utility classes', (tone, borderClass, textClass) => {
    const { container } = render(<KPIStatCard label="A" value="1" tone={tone} />);
    const card = container.firstChild as HTMLElement;
    const value = container.querySelector('.font-display') as HTMLElement;
    expect(card.className).toContain(borderClass);
    expect(value.className).toContain(textClass);
  });

  it('accepts custom className override', () => {
    const { container } = render(<KPIStatCard label="A" value="1" className="extra" />);
    expect((container.firstChild as HTMLElement).className).toContain('extra');
  });

  it('value supports React nodes (not just strings)', () => {
    render(
      <KPIStatCard
        label="Custom"
        value={
          <>
            <span>2</span>
            <span>/3</span>
          </>
        }
      />
    );
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('/3')).toBeInTheDocument();
  });
});
