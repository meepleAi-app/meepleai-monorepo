// apps/web/src/components/features/gamebook/__tests__/LangBadge.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { LangBadge } from '../LangBadge';

describe('LangBadge', () => {
  describe('tier rendering', () => {
    it('renders "Sorgente: EN" for tier=high with detected lang', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" />);
      expect(screen.getByText(/sorgente: EN/i)).toBeInTheDocument();
    });

    it('renders "Conferma: FR?" for tier=medium with detected lang', () => {
      render(<LangBadge lang="FR" confidence={0.65} tier="medium" />);
      expect(screen.getByText(/conferma: FR\?/i)).toBeInTheDocument();
    });

    it('renders "Sorgente richiesta" for tier=low (independent of lang)', () => {
      render(<LangBadge lang={null} confidence={0.31} tier="low" />);
      expect(screen.getByText(/sorgente richiesta/i)).toBeInTheDocument();
    });

    it('renders nothing for tier=none (returns null)', () => {
      const { container } = render(<LangBadge lang={null} confidence={null} tier="none" />);
      expect(container).toBeEmptyDOMElement();
    });

    it('appends "(override)" suffix when isOverride=true on high tier', () => {
      render(<LangBadge lang="FR" confidence={0.92} tier="high" isOverride />);
      expect(screen.getByText(/sorgente: FR.*override/i)).toBeInTheDocument();
    });
  });

  describe('interaction', () => {
    it('calls onTap when user clicks the badge', async () => {
      const user = userEvent.setup();
      const onTap = vi.fn();
      render(<LangBadge lang="EN" confidence={0.92} tier="high" onTap={onTap} />);

      await user.click(screen.getByRole('button'));
      expect(onTap).toHaveBeenCalledTimes(1);
    });

    it('renders as <span> (non-interactive) when no onTap provided', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('aria-label includes confidence (for screen readers)', () => {
    it('mentions confidence percentage in aria-label on tier=high', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/sorgente.*inglese.*confidenza alta.*92%/i)
      );
    });

    it('mentions confidence percentage in aria-label on tier=medium', () => {
      render(<LangBadge lang="FR" confidence={0.65} tier="medium" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/conferma.*francese.*confidenza media.*65%/i)
      );
    });

    it('aria-label on tier=low signals action required', () => {
      render(<LangBadge lang={null} confidence={0.31} tier="low" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/sorgente richiesta.*conferma manualmente/i)
      );
    });
  });

  describe('a11y (jest-axe)', () => {
    it('high tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang="EN" confidence={0.92} tier="high" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('medium tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang="FR" confidence={0.65} tier="medium" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('low tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang={null} confidence={0.31} tier="low" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });
  });
});
