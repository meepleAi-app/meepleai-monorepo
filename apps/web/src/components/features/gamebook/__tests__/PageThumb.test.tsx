/**
 * PageThumb unit tests — SP6 Phase C.2.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot="page-thumb" + data-page-number + data-confidence-level
 *   - data-processing toggles between 'true' / 'false'
 *   - role="status" + aria-live="polite" only when processing=true
 *   - Spinner appears only when processing=true
 *   - ConfidenceBadge renders only when !processing AND confidence !== null
 *   - Retake CTA renders only when retake=true AND onRetakeClick provided
 *   - Retake button calls onRetakeClick on click
 *   - Page number always visible
 *   - Border color changes when retake=true
 *   - Reduced motion: spinner uses motion-reduce:animate-none
 *   - Thumbnail URL renders when provided; placeholder otherwise
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { PageThumb, type PageThumbLabels, type PageThumbProps } from '../PageThumb';

const LABELS: PageThumbLabels = {
  pageLabel: 'Pagina 3',
  retakeAria: 'Riscatta pagina 3',
  processingAria: 'Pagina 3 in elaborazione',
  retakeCta: 'Riscatta',
  confidence: {
    high: 'Alta confidenza',
    medium: 'Media confidenza',
    low: 'Bassa confidenza',
  },
};

function renderThumb(overrides: Partial<PageThumbProps> = {}) {
  const props: PageThumbProps = {
    pageNumber: 3,
    thumbnailUrl: null,
    confidence: 'high',
    processing: false,
    retake: false,
    labels: LABELS,
    ...overrides,
  };
  return render(<PageThumb {...props} />);
}

describe('PageThumb — render shape', () => {
  it('renders data-slot="page-thumb"', () => {
    renderThumb();
    expect(document.querySelector('[data-slot="page-thumb"]')).not.toBeNull();
  });

  it('renders data-page-number matching prop', () => {
    renderThumb({ pageNumber: 7 });
    const root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-page-number')).toBe('7');
  });

  it('renders page number visibly', () => {
    renderThumb({ pageNumber: 12 });
    const num = document.querySelector('[data-slot="page-thumb-num"]');
    expect(num?.textContent).toBe('12');
  });

  it('exposes data-confidence-level matching confidence', () => {
    renderThumb({ confidence: 'medium' });
    const root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-confidence-level')).toBe('medium');
  });

  it('exposes data-confidence-level="processing" when processing=true', () => {
    renderThumb({ processing: true, confidence: null });
    const root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-confidence-level')).toBe('processing');
  });

  it('renders <li> element (children of grid <ul>)', () => {
    renderThumb();
    const root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.tagName).toBe('LI');
  });
});

describe('PageThumb — processing state', () => {
  it('exposes role="status" + aria-live="polite" when processing=true', () => {
    renderThumb({ processing: true, confidence: null });
    const root = document.querySelector('[data-slot="page-thumb"]') as HTMLElement;
    expect(root.getAttribute('role')).toBe('status');
    expect(root.getAttribute('aria-live')).toBe('polite');
  });

  it('omits role="status" when processing=false', () => {
    renderThumb({ processing: false });
    const root = document.querySelector('[data-slot="page-thumb"]') as HTMLElement;
    expect(root.hasAttribute('role')).toBe(false);
  });

  it('renders spinner when processing=true', () => {
    renderThumb({ processing: true, confidence: null });
    expect(document.querySelector('[data-slot="page-thumb-spinner"]')).not.toBeNull();
  });

  it('does NOT render spinner when processing=false', () => {
    renderThumb({ processing: false });
    expect(document.querySelector('[data-slot="page-thumb-spinner"]')).toBeNull();
  });

  it('spinner has motion-safe + motion-reduce classes for reduced motion', () => {
    renderThumb({ processing: true, confidence: null });
    const spinner = document.querySelector('[data-slot="page-thumb-spinner"]');
    expect(spinner?.className).toContain('motion-safe:animate-spin');
    expect(spinner?.className).toContain('motion-reduce:animate-none');
  });

  it('renders sr-only processing announcement when processing=true', () => {
    renderThumb({ processing: true, confidence: null });
    expect(screen.getByText('Pagina 3 in elaborazione')).toBeInTheDocument();
  });

  it('data-processing attribute reflects processing prop', () => {
    const { rerender } = renderThumb({ processing: true, confidence: null });
    let root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-processing')).toBe('true');

    rerender(
      <PageThumb
        pageNumber={3}
        thumbnailUrl={null}
        confidence="high"
        processing={false}
        retake={false}
        labels={LABELS}
      />
    );
    root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-processing')).toBe('false');
  });
});

describe('PageThumb — confidence badge integration', () => {
  it('renders ConfidenceBadge when !processing AND confidence !== null', () => {
    renderThumb({ confidence: 'high' });
    expect(document.querySelector('[data-slot="confidence-badge"]')).not.toBeNull();
  });

  it('does NOT render ConfidenceBadge when processing=true', () => {
    renderThumb({ processing: true, confidence: null });
    expect(document.querySelector('[data-slot="confidence-badge"]')).toBeNull();
  });

  it('does NOT render ConfidenceBadge when confidence=null', () => {
    renderThumb({ processing: false, confidence: null });
    expect(document.querySelector('[data-slot="confidence-badge"]')).toBeNull();
  });

  it('passes confidence labels through to badge', () => {
    renderThumb({ confidence: 'low' });
    expect(screen.getByRole('img', { name: 'Bassa confidenza' })).toBeInTheDocument();
  });
});

describe('PageThumb — retake state', () => {
  it('does NOT render retake CTA when retake=false', () => {
    renderThumb({ retake: false });
    expect(document.querySelector('[data-slot="page-thumb-retake"]')).toBeNull();
  });

  it('does NOT render retake CTA when retake=true but onRetakeClick missing', () => {
    renderThumb({ retake: true });
    expect(document.querySelector('[data-slot="page-thumb-retake"]')).toBeNull();
  });

  it('renders retake CTA when retake=true AND onRetakeClick provided', () => {
    const onRetakeClick = vi.fn();
    renderThumb({ retake: true, onRetakeClick, confidence: 'low' });
    expect(document.querySelector('[data-slot="page-thumb-retake"]')).not.toBeNull();
  });

  it('retake button has aria-label from labels.retakeAria', () => {
    const onRetakeClick = vi.fn();
    renderThumb({ retake: true, onRetakeClick, confidence: 'low' });
    expect(screen.getByRole('button', { name: 'Riscatta pagina 3' })).toBeInTheDocument();
  });

  it('calls onRetakeClick when retake button clicked', async () => {
    const user = userEvent.setup();
    const onRetakeClick = vi.fn();
    renderThumb({ retake: true, onRetakeClick, confidence: 'low' });

    await user.click(screen.getByRole('button', { name: 'Riscatta pagina 3' }));
    expect(onRetakeClick).toHaveBeenCalledOnce();
  });

  it('exposes data-retake="true" when retake=true', () => {
    const onRetakeClick = vi.fn();
    renderThumb({ retake: true, onRetakeClick, confidence: 'low' });
    const root = document.querySelector('[data-slot="page-thumb"]');
    expect(root?.getAttribute('data-retake')).toBe('true');
  });
});

describe('PageThumb — thumbnail rendering', () => {
  it('renders thumbnailUrl as background-image when provided', () => {
    renderThumb({ thumbnailUrl: 'blob:abc-123' });
    const img = document.querySelector('[data-slot="page-thumb-img"]') as HTMLElement;
    expect(img.style.background).toContain('blob:abc-123');
  });

  it('renders deterministic placeholder gradient when thumbnailUrl=null AND !processing', () => {
    renderThumb({ thumbnailUrl: null, processing: false });
    const img = document.querySelector('[data-slot="page-thumb-img"]') as HTMLElement;
    expect(img.style.background).toContain('linear-gradient');
  });

  it('renders neutral skeleton bg when thumbnailUrl=null AND processing=true', () => {
    renderThumb({ thumbnailUrl: null, processing: true, confidence: null });
    const img = document.querySelector('[data-slot="page-thumb-img"]') as HTMLElement;
    // hsl(215, 16%, 92%) — slate-100-ish skeleton
    expect(img.style.background).toMatch(/hsl|rgb/);
  });
});
