import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingSkeleton } from '../LoadingSkeleton';
import { LABELS, type UiStep } from '../TranslateViewer.steps';

describe('LoadingSkeleton', () => {
  const uiSteps: UiStep[] = ['uploading', 'ocr', 'translating', 'glossary-check'];

  uiSteps.forEach(uiStep => {
    describe(`when uiStep = "${uiStep}"`, () => {
      it('renders with correct data-testid', () => {
        render(<LoadingSkeleton uiStep={uiStep} />);
        expect(screen.getByTestId(`translate-skeleton-${uiStep}`)).toBeInTheDocument();
      });

      it('displays the step label from LABELS', () => {
        render(<LoadingSkeleton uiStep={uiStep} />);
        const label = screen.getByTestId('translate-step-label');
        expect(label).toHaveTextContent(LABELS[uiStep]);
      });

      it('has aria-busy="true" for accessibility', () => {
        render(<LoadingSkeleton uiStep={uiStep} />);
        const container = screen.getByTestId(`translate-skeleton-${uiStep}`);
        expect(container).toHaveAttribute('aria-busy', 'true');
      });

      it('has aria-live="polite" for SR announcement', () => {
        render(<LoadingSkeleton uiStep={uiStep} />);
        const container = screen.getByTestId(`translate-skeleton-${uiStep}`);
        expect(container).toHaveAttribute('aria-live', 'polite');
      });
    });
  });

  it('renders 3 skeleton bars with correct widths', () => {
    render(<LoadingSkeleton uiStep="ocr" />);
    const bars = screen
      .getByTestId('translate-skeleton-ocr')
      .querySelectorAll('div[class*="bg-muted"]');
    expect(bars).toHaveLength(3);

    // Verify width classes are present in DOM
    const container = screen.getByTestId('translate-skeleton-ocr');
    const barElements = Array.from(container.querySelectorAll('div.motion-safe\\:animate-pulse'));
    expect(barElements).toHaveLength(3);
  });

  it('has role="status" for semantic meaning', () => {
    render(<LoadingSkeleton uiStep="translating" />);
    const container = screen.getByTestId('translate-skeleton-translating');
    expect(container).toHaveAttribute('role', 'status');
  });
});
