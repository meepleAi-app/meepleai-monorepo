// apps/web/src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { LangOverrideModal } from '../LangOverrideModal';

describe('LangOverrideModal', () => {
  describe('rendering', () => {
    it('renders no dialog when open=false', () => {
      render(
        <LangOverrideModal open={false} onOpenChange={() => {}} dismissable onConfirm={() => {}} />
      );
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('renders dialog with title + 5 radio options when open=true', () => {
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText(/seleziona la lingua/i)).toBeInTheDocument();
      expect(screen.getAllByRole('radio')).toHaveLength(5);
      // Each label
      expect(screen.getByLabelText('Inglese')).toBeInTheDocument();
      expect(screen.getByLabelText('Francese')).toBeInTheDocument();
      expect(screen.getByLabelText('Tedesco')).toBeInTheDocument();
      expect(screen.getByLabelText('Spagnolo')).toBeInTheDocument();
      expect(screen.getByLabelText('Italiano')).toBeInTheDocument();
    });

    it('preselects the radio matching preselect prop', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="FR"
          onConfirm={() => {}}
        />
      );
      expect(screen.getByLabelText('Francese')).toBeChecked();
      expect(screen.getByLabelText('Inglese')).not.toBeChecked();
    });
  });

  describe('confirm', () => {
    it('confirm button disabled when no selection + no preselect', () => {
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).toBeDisabled();
    });

    it('confirm button enabled when preselect provided', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="FR"
          onConfirm={() => {}}
        />
      );
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).not.toBeDisabled();
    });

    it('confirm enables after user picks a radio (from no-preselect)', async () => {
      const user = userEvent.setup();
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      await user.click(screen.getByLabelText('Tedesco'));
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).not.toBeDisabled();
    });

    it('invokes onConfirm with selected lang on submit', async () => {
      const user = userEvent.setup();
      const onConfirm = vi.fn();
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="EN"
          onConfirm={onConfirm}
        />
      );
      await user.click(screen.getByLabelText('Spagnolo'));
      await user.click(screen.getByRole('button', { name: /conferma e ritraduci/i }));
      expect(onConfirm).toHaveBeenCalledWith('ES');
    });
  });

  describe('dismiss', () => {
    it('renders Chiudi button when dismissable=true', () => {
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      expect(screen.getByRole('button', { name: /chiudi/i })).toBeInTheDocument();
    });

    it('hides Chiudi button when dismissable=false', () => {
      render(
        <LangOverrideModal open onOpenChange={() => {}} dismissable={false} onConfirm={() => {}} />
      );
      expect(screen.queryByRole('button', { name: /chiudi/i })).not.toBeInTheDocument();
    });

    it('calls onOpenChange(false) when Chiudi clicked', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      render(
        <LangOverrideModal open onOpenChange={onOpenChange} dismissable onConfirm={() => {}} />
      );
      await user.click(screen.getByRole('button', { name: /chiudi/i }));
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });

    it('calls onOpenChange(false) on Escape when dismissable=true', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      render(
        <LangOverrideModal open onOpenChange={onOpenChange} dismissable onConfirm={() => {}} />
      );
      await user.keyboard('{Escape}');
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  describe('a11y', () => {
    it('dialog has aria-modal=true (Radix automatic)', () => {
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
    });

    it('axe finds zero violations when open (no preselect)', async () => {
      const { container } = render(
        <LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('axe finds zero violations when open (with preselect)', async () => {
      const { container } = render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="DE"
          onConfirm={() => {}}
        />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('axe finds zero violations when dismissable=false', async () => {
      const { container } = render(
        <LangOverrideModal open onOpenChange={() => {}} dismissable={false} onConfirm={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });
  });

  describe('focus', () => {
    it.skip('moves focus inside dialog on mount (focus trap)', async () => {
      // jsdom limitation: Radix Dialog focus trap doesn't reliably fire in jsdom
      render(<LangOverrideModal open onOpenChange={() => {}} dismissable onConfirm={() => {}} />);
      await waitFor(() => {
        // Radix moves focus to the first focusable element (typically close button or first radio)
        const dialog = screen.getByRole('dialog');
        expect(dialog.contains(document.activeElement)).toBe(true);
      });
    });
  });
});
