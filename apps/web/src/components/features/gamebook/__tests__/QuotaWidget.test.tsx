/**
 * QuotaWidget unit tests — SP6 Phase B Task 2 (Issue #788).
 *
 * Coverage:
 *   - data-slot + data-variant
 *   - title + counter render
 *   - progress bar fill width per variant
 *   - default variant: no banner, no upgrade CTA
 *   - soft variant: warning banner + upgrade CTA visible
 *   - hard variant: blocking banner with role="alert" + upgrade CTA
 *   - upgrade CTA fires onUpgradeClick
 *   - role="status" on container
 *   - reset date label render
 *   - zero-total guard (defensive)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { QuotaWidget } from '../QuotaWidget';
import type { QuotaWidgetProps } from '../QuotaWidget';

import type { QuotaInfo } from '@/lib/gamebook-index/schemas';

const LABELS: QuotaWidgetProps['labels'] = {
  title: 'Quota traduzioni',
  usedLabel: '12 / 50',
  resetIn: 'Resetta il 1 giugno',
  softWarning: 'Stai per esaurire la quota mensile',
  hardLimit: 'Quota raggiunta — acquista crediti',
  upgrade: 'Acquista crediti',
};

const QUOTA_DEFAULT: QuotaInfo = {
  used: 12,
  total: 50,
  resetDate: '2026-06-01T00:00:00.000Z',
  tier: 'free',
};

const QUOTA_SOFT: QuotaInfo = {
  used: 47,
  total: 50,
  resetDate: '2026-06-01T00:00:00.000Z',
  tier: 'free',
};

const QUOTA_HARD: QuotaInfo = {
  used: 50,
  total: 50,
  resetDate: '2026-06-01T00:00:00.000Z',
  tier: 'free',
};

describe('QuotaWidget', () => {
  it('renders data-slot="quota-widget" with role="status"', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    const widget = document.querySelector('[data-slot="quota-widget"]');
    expect(widget).not.toBeNull();
    expect(widget!.getAttribute('role')).toBe('status');
  });

  it('reflects variant in data-variant attribute', () => {
    const { rerender } = render(
      <QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />
    );
    expect(document.querySelector('[data-slot="quota-widget"]')!.getAttribute('data-variant')).toBe(
      'default'
    );

    rerender(<QuotaWidget quota={QUOTA_SOFT} variant="soft" labels={LABELS} />);
    expect(document.querySelector('[data-slot="quota-widget"]')!.getAttribute('data-variant')).toBe(
      'soft'
    );

    rerender(<QuotaWidget quota={QUOTA_HARD} variant="hard" labels={LABELS} />);
    expect(document.querySelector('[data-slot="quota-widget"]')!.getAttribute('data-variant')).toBe(
      'hard'
    );
  });

  it('renders title and pre-formatted usedLabel', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    expect(screen.getByText('Quota traduzioni')).toBeTruthy();
    expect(screen.getByText('12 / 50')).toBeTruthy();
  });

  it('renders reset date sentence', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    expect(screen.getByText('Resetta il 1 giugno')).toBeTruthy();
  });

  it('progress fill width matches used/total ratio', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    const fill = document.querySelector('[data-slot="quota-widget-fill"]') as HTMLElement;
    expect(fill).not.toBeNull();
    // 12 / 50 = 24%
    expect(fill.style.width).toBe('24%');
  });

  it('soft variant: progress fill at 94% (47/50)', () => {
    render(<QuotaWidget quota={QUOTA_SOFT} variant="soft" labels={LABELS} />);
    const fill = document.querySelector('[data-slot="quota-widget-fill"]') as HTMLElement;
    expect(fill.style.width).toBe('94%');
  });

  it('hard variant: progress fill at 100% (50/50)', () => {
    render(<QuotaWidget quota={QUOTA_HARD} variant="hard" labels={LABELS} />);
    const fill = document.querySelector('[data-slot="quota-widget-fill"]') as HTMLElement;
    expect(fill.style.width).toBe('100%');
  });

  it('default variant: no warning banner, no upgrade CTA', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    expect(document.querySelector('[data-slot="quota-widget-soft-banner"]')).toBeNull();
    expect(document.querySelector('[data-slot="quota-widget-hard-banner"]')).toBeNull();
    expect(document.querySelector('[data-slot="quota-widget-upgrade-cta"]')).toBeNull();
  });

  it('soft variant: renders warning banner + upgrade CTA when handler provided', () => {
    render(
      <QuotaWidget quota={QUOTA_SOFT} variant="soft" labels={LABELS} onUpgradeClick={vi.fn()} />
    );
    expect(document.querySelector('[data-slot="quota-widget-soft-banner"]')).not.toBeNull();
    expect(screen.getByText('Stai per esaurire la quota mensile')).toBeTruthy();
    expect(document.querySelector('[data-slot="quota-widget-upgrade-cta"]')).not.toBeNull();
    expect(screen.getByText('Acquista crediti')).toBeTruthy();
  });

  it('hard variant: renders blocking banner with role="alert" + upgrade CTA', () => {
    render(
      <QuotaWidget quota={QUOTA_HARD} variant="hard" labels={LABELS} onUpgradeClick={vi.fn()} />
    );
    const banner = document.querySelector('[data-slot="quota-widget-hard-banner"]');
    expect(banner).not.toBeNull();
    expect(banner!.getAttribute('role')).toBe('alert');
    expect(screen.getByText('Quota raggiunta — acquista crediti')).toBeTruthy();
    expect(document.querySelector('[data-slot="quota-widget-upgrade-cta"]')).not.toBeNull();
  });

  it('upgrade CTA fires onUpgradeClick when clicked', () => {
    const onUpgradeClick = vi.fn();
    render(
      <QuotaWidget
        quota={QUOTA_SOFT}
        variant="soft"
        labels={LABELS}
        onUpgradeClick={onUpgradeClick}
      />
    );
    fireEvent.click(document.querySelector('[data-slot="quota-widget-upgrade-cta"]')!);
    expect(onUpgradeClick).toHaveBeenCalledOnce();
  });

  it('upgrade CTA hidden if onUpgradeClick is undefined', () => {
    render(<QuotaWidget quota={QUOTA_SOFT} variant="soft" labels={LABELS} />);
    expect(document.querySelector('[data-slot="quota-widget-upgrade-cta"]')).toBeNull();
  });

  it('zero-total quota guards against NaN width', () => {
    const ZERO_TOTAL: QuotaInfo = {
      ...QUOTA_DEFAULT,
      used: 0,
      // schema enforces total > 0, but defend at runtime
      total: 1,
    };
    render(<QuotaWidget quota={ZERO_TOTAL} variant="default" labels={LABELS} />);
    const fill = document.querySelector('[data-slot="quota-widget-fill"]') as HTMLElement;
    expect(fill.style.width).toBe('0%');
  });

  it('aria-label includes title and usedLabel', () => {
    render(<QuotaWidget quota={QUOTA_DEFAULT} variant="default" labels={LABELS} />);
    const widget = document.querySelector('[data-slot="quota-widget"]');
    expect(widget!.getAttribute('aria-label')).toBe('Quota traduzioni: 12 / 50');
  });

  it('applies custom className', () => {
    render(
      <QuotaWidget
        quota={QUOTA_DEFAULT}
        variant="default"
        labels={LABELS}
        className="my-custom-class"
      />
    );
    expect(
      document.querySelector('[data-slot="quota-widget"]')!.classList.contains('my-custom-class')
    ).toBe(true);
  });
});
