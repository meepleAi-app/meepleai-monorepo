import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StepProgress, type Step } from './step-progress';

const makeSteps = (n: number): Step[] =>
  Array.from({ length: n }, (_, i) => ({ label: `Step ${i + 1}` }));

describe('StepProgress', () => {
  it('renders N circles for N steps', () => {
    const { container } = render(<StepProgress steps={makeSteps(4)} currentIndex={1} />);
    const circles = container.querySelectorAll('[data-step-circle]');
    expect(circles).toHaveLength(4);
  });

  it('renders step labels', () => {
    render(<StepProgress steps={makeSteps(3)} currentIndex={0} />);
    expect(screen.getByText('Step 1')).toBeInTheDocument();
    expect(screen.getByText('Step 2')).toBeInTheDocument();
    expect(screen.getByText('Step 3')).toBeInTheDocument();
  });

  it('completed steps have completed styling (inline background color)', () => {
    const { container } = render(<StepProgress steps={makeSteps(4)} currentIndex={2} />);
    const completed = container.querySelector('[data-step-status="completed"]');
    expect(completed).toBeInTheDocument();
    // Check data-step-circle child has inline style
    const circle = completed?.querySelector('[data-step-circle]') as HTMLElement | null;
    expect(circle?.style.backgroundColor).toMatch(/hsl/i);
  });

  it('current step has current styling (ring and scale)', () => {
    const { container } = render(<StepProgress steps={makeSteps(4)} currentIndex={2} />);
    const current = container.querySelector('[data-step-status="current"]');
    expect(current).toBeInTheDocument();
    const circle = current?.querySelector('[data-step-circle]');
    expect(circle?.className).toMatch(/ring/);
    expect(circle?.className).toMatch(/scale-110/);
  });

  it('pending steps have muted styling', () => {
    const { container } = render(<StepProgress steps={makeSteps(4)} currentIndex={1} />);
    const pending = container.querySelector('[data-step-status="pending"]');
    expect(pending).toBeInTheDocument();
    const circle = pending?.querySelector('[data-step-circle]');
    expect(circle?.className).toMatch(/muted|gray|bg-muted/);
  });

  it('currentIndex drives which step is current (when status field absent)', () => {
    const { container } = render(<StepProgress steps={makeSteps(5)} currentIndex={3} />);
    const items = container.querySelectorAll('[data-step-status]');
    expect(items[0]?.getAttribute('data-step-status')).toBe('completed');
    expect(items[1]?.getAttribute('data-step-status')).toBe('completed');
    expect(items[2]?.getAttribute('data-step-status')).toBe('completed');
    expect(items[3]?.getAttribute('data-step-status')).toBe('current');
    expect(items[4]?.getAttribute('data-step-status')).toBe('pending');
  });

  it('explicit status field overrides currentIndex derivation', () => {
    const steps: Step[] = [
      { label: 'A', status: 'completed' },
      { label: 'B', status: 'completed' },
      { label: 'C', status: 'current' },
      { label: 'D', status: 'pending' },
    ];
    const { container } = render(<StepProgress steps={steps} currentIndex={0} />);
    const items = container.querySelectorAll('[data-step-status]');
    expect(items[0]?.getAttribute('data-step-status')).toBe('completed');
    expect(items[2]?.getAttribute('data-step-status')).toBe('current');
    expect(items[3]?.getAttribute('data-step-status')).toBe('pending');
  });

  it('entity prop applies correct HSL inline to completed circles', () => {
    const { container } = render(
      <StepProgress steps={makeSteps(3)} currentIndex={2} entity="session" />
    );
    const completed = container.querySelector(
      '[data-step-status="completed"] [data-step-circle]'
    ) as HTMLElement | null;
    expect(completed?.style.backgroundColor).toContain('var(--e-session)');
  });

  it('default entity (no prop) uses --e-game brand color', () => {
    const { container } = render(<StepProgress steps={makeSteps(3)} currentIndex={2} />);
    const completed = container.querySelector(
      '[data-step-status="completed"] [data-step-circle]'
    ) as HTMLElement | null;
    expect(completed?.style.backgroundColor).toContain('var(--e-game)');
  });

  it('sets role="progressbar" and aria-valuenow reflects currentIndex+1', () => {
    const { container } = render(<StepProgress steps={makeSteps(5)} currentIndex={2} />);
    const bar = container.querySelector('[role="progressbar"]');
    expect(bar).toBeInTheDocument();
    expect(bar).toHaveAttribute('aria-valuenow', '3');
    expect(bar).toHaveAttribute('aria-valuemax', '5');
    expect(bar).toHaveAttribute('aria-valuemin', '1');
  });

  it('aria-label defaults to "Progresso" if not provided', () => {
    const { container } = render(<StepProgress steps={makeSteps(3)} currentIndex={0} />);
    const bar = container.querySelector('[role="progressbar"]');
    expect(bar).toHaveAttribute('aria-label', 'Progresso');
  });

  it('uses custom ariaLabel when provided', () => {
    const { container } = render(
      <StepProgress steps={makeSteps(3)} currentIndex={0} ariaLabel="Passi del percorso" />
    );
    const bar = container.querySelector('[role="progressbar"]');
    expect(bar).toHaveAttribute('aria-label', 'Passi del percorso');
  });

  it('merges className on root', () => {
    const { container } = render(
      <StepProgress steps={makeSteps(3)} currentIndex={0} className="custom-xyz" />
    );
    const bar = container.querySelector('[role="progressbar"]');
    expect(bar?.className).toMatch(/custom-xyz/);
  });

  it('handles kb entity mapping to --e-document', () => {
    const { container } = render(
      <StepProgress steps={makeSteps(3)} currentIndex={2} entity="kb" />
    );
    const completed = container.querySelector(
      '[data-step-status="completed"] [data-step-circle]'
    ) as HTMLElement | null;
    expect(completed?.style.backgroundColor).toContain('var(--e-document)');
  });

  it('handles 2 steps (min edge)', () => {
    const { container } = render(<StepProgress steps={makeSteps(2)} currentIndex={0} />);
    const circles = container.querySelectorAll('[data-step-circle]');
    expect(circles).toHaveLength(2);
    const connectors = container.querySelectorAll('[data-step-connector]');
    expect(connectors).toHaveLength(1);
  });

  it('handles 5 steps (typical onboarding)', () => {
    const { container } = render(<StepProgress steps={makeSteps(5)} currentIndex={2} />);
    const circles = container.querySelectorAll('[data-step-circle]');
    expect(circles).toHaveLength(5);
    const connectors = container.querySelectorAll('[data-step-connector]');
    expect(connectors).toHaveLength(4);
  });

  it('current step li has aria-current="step"', () => {
    const { container } = render(<StepProgress steps={makeSteps(4)} currentIndex={1} />);
    const current = container.querySelector('[aria-current="step"]');
    expect(current).toBeInTheDocument();
    expect(current?.getAttribute('data-step-status')).toBe('current');
  });
});
