import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KBRulesQuickGlance, type KBRule } from './index';

const SAMPLE_RULES: ReadonlyArray<KBRule> = [
  { icon: '🔁', text: 'Phase order · Growth → Fast → Slow', src: '§ 4.2' },
  { icon: '😱', text: 'Fear deck · 9 carte iniziali', src: '§ 5.1' },
  { icon: '⚡', text: 'Power growth · 1 minor + 1 major', src: '§ 6.3' },
];

describe('KBRulesQuickGlance', () => {
  describe('status="ok" (default)', () => {
    it('renders rules count in title', () => {
      render(<KBRulesQuickGlance rules={SAMPLE_RULES} />);
      expect(screen.getByText(/Rules quick-glance · 3 bullet/)).toBeInTheDocument();
    });

    it('renders all rule texts', () => {
      render(<KBRulesQuickGlance rules={SAMPLE_RULES} />);
      SAMPLE_RULES.forEach(r => {
        expect(screen.getByText(r.text)).toBeInTheDocument();
      });
    });

    it('renders src reference pills when provided', () => {
      render(<KBRulesQuickGlance rules={SAMPLE_RULES} />);
      expect(screen.getByText('§ 4.2')).toBeInTheDocument();
      expect(screen.getByText('§ 5.1')).toBeInTheDocument();
    });

    it('omits src element when rule.src missing', () => {
      const rules: ReadonlyArray<KBRule> = [{ text: 'rule without src' }];
      render(<KBRulesQuickGlance rules={rules} />);
      expect(screen.getByText('rule without src')).toBeInTheDocument();
      expect(screen.queryByText('§')).toBeNull();
    });

    it('icon spans are aria-hidden (decorative)', () => {
      render(<KBRulesQuickGlance rules={SAMPLE_RULES} />);
      const icons = screen.getAllByText('🔁');
      expect(icons[0]).toHaveAttribute('aria-hidden', 'true');
    });

    it('renders 0 bullet title with empty rules array', () => {
      render(<KBRulesQuickGlance rules={[]} />);
      expect(screen.getByText(/Rules quick-glance · 0 bullet/)).toBeInTheDocument();
    });

    it('accepts custom title override', () => {
      render(<KBRulesQuickGlance rules={SAMPLE_RULES} title="Custom title" />);
      expect(screen.getByText(/Custom title · 3 bullet/)).toBeInTheDocument();
    });
  });

  describe('status="loading"', () => {
    it('renders skeleton placeholders with aria-busy', () => {
      const { container } = render(<KBRulesQuickGlance status="loading" />);
      expect(container.firstChild).toHaveAttribute('aria-busy', 'true');
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBe(3);
    });

    it('appends "loading…" suffix to title', () => {
      render(<KBRulesQuickGlance status="loading" />);
      expect(screen.getByText(/Rules quick-glance · loading…/)).toBeInTheDocument();
    });

    it('renders loadingHint when provided', () => {
      render(<KBRulesQuickGlance status="loading" loadingHint="Fetching KB…" />);
      expect(screen.getByText('Fetching KB…')).toBeInTheDocument();
    });
  });

  describe('status="error"', () => {
    it('renders alert role with default error copy', () => {
      render(<KBRulesQuickGlance status="error" />);
      const alert = screen.getByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(screen.getByText('Rules non disponibili offline')).toBeInTheDocument();
    });

    it('renders custom errorTitle + errorMessage', () => {
      render(
        <KBRulesQuickGlance
          status="error"
          errorTitle="Custom error"
          errorMessage="Custom message"
        />
      );
      expect(screen.getByText('Custom error')).toBeInTheDocument();
      expect(screen.getByText('Custom message')).toBeInTheDocument();
    });

    it('invokes onRetry when Riprova button clicked', async () => {
      const onRetry = vi.fn();
      render(<KBRulesQuickGlance status="error" onRetry={onRetry} />);
      await userEvent.click(screen.getByRole('button', { name: /Riprova/ }));
      expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it('invokes onProceed when "Procedi senza" button clicked', async () => {
      const onProceed = vi.fn();
      render(<KBRulesQuickGlance status="error" onProceed={onProceed} />);
      await userEvent.click(screen.getByRole('button', { name: /Procedi senza/ }));
      expect(onProceed).toHaveBeenCalledTimes(1);
    });
  });
});
