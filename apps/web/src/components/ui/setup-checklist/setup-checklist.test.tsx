import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SetupChecklist, type SetupChecklistItem } from './index';

const SAMPLE: ReadonlyArray<SetupChecklistItem> = [
  { icon: '🗺️', text: 'Tabellone · 4 isole', done: true },
  { icon: '🧝', text: 'Spirit panels (4 spiriti scelti)', done: true },
  { icon: '🃏', text: 'Invader deck shuffled', done: false },
  { icon: '💀', text: 'Fear pool · 9 token', done: false },
];

describe('SetupChecklist', () => {
  it('renders default "Setup checklist" title with done/total counter', () => {
    render(<SetupChecklist items={SAMPLE} />);
    expect(screen.getByText(/Setup checklist · 2\/4/)).toBeInTheDocument();
  });

  it('renders all item texts', () => {
    render(<SetupChecklist items={SAMPLE} />);
    SAMPLE.forEach(item => {
      expect(screen.getByText(item.text)).toBeInTheDocument();
    });
  });

  it('renders icon emojis when provided (aria-hidden)', () => {
    render(<SetupChecklist items={SAMPLE} />);
    const mapIcon = screen.getByText('🗺️');
    expect(mapIcon).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders ✓ check mark only for done items', () => {
    render(<SetupChecklist items={SAMPLE} />);
    const checks = screen.getAllByText('✓');
    expect(checks.length).toBe(2);
  });

  it('applies line-through styling to done items only', () => {
    const { container } = render(<SetupChecklist items={SAMPLE} />);
    const textSpans = container.querySelectorAll('li span.font-body');
    expect(textSpans.length).toBe(4);
    expect(textSpans[0].className).toContain('line-through');
    expect(textSpans[1].className).toContain('line-through');
    expect(textSpans[2].className).not.toContain('line-through');
    expect(textSpans[3].className).not.toContain('line-through');
  });

  it('applies entity-toolkit accent on done items, muted on pending', () => {
    const { container } = render(<SetupChecklist items={SAMPLE} />);
    const items = container.querySelectorAll('li');
    expect(items[0].className).toContain('bg-entity-toolkit');
    expect(items[2].className).toContain('bg-muted');
  });

  it('handles 0/N counter when no items done', () => {
    const allPending: ReadonlyArray<SetupChecklistItem> = [
      { text: 'A', done: false },
      { text: 'B', done: false },
    ];
    render(<SetupChecklist items={allPending} />);
    expect(screen.getByText(/Setup checklist · 0\/2/)).toBeInTheDocument();
  });

  it('handles N/N counter when all done', () => {
    const allDone: ReadonlyArray<SetupChecklistItem> = [
      { text: 'A', done: true },
      { text: 'B', done: true },
    ];
    render(<SetupChecklist items={allDone} />);
    expect(screen.getByText(/Setup checklist · 2\/2/)).toBeInTheDocument();
  });

  it('treats undefined done as not done', () => {
    const noFlag: ReadonlyArray<SetupChecklistItem> = [{ text: 'no flag' }];
    render(<SetupChecklist items={noFlag} />);
    expect(screen.getByText(/Setup checklist · 0\/1/)).toBeInTheDocument();
    expect(screen.queryByText('✓')).toBeNull();
  });

  it('accepts custom title override', () => {
    render(<SetupChecklist items={SAMPLE} title="Pre-flight check" />);
    expect(screen.getByText(/Pre-flight check · 2\/4/)).toBeInTheDocument();
  });

  it('omits icon span when item.icon missing', () => {
    const noIcons: ReadonlyArray<SetupChecklistItem> = [{ text: 'no icon', done: true }];
    const { container } = render(<SetupChecklist items={noIcons} />);
    // Only the check ✓ span should have aria-hidden (since icon emoji span is omitted)
    const ariaHidden = container.querySelectorAll('[aria-hidden="true"]');
    // 2 = header 📋 + ✓ checkbox; no item icon span
    expect(ariaHidden.length).toBe(2);
  });

  it('accepts empty items array', () => {
    render(<SetupChecklist items={[]} />);
    expect(screen.getByText(/Setup checklist · 0\/0/)).toBeInTheDocument();
  });
});
