import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

describe('ManaPips — interactive button rendering', () => {
  describe('non-interactive pips (no onCreate)', () => {
    it('renders as <span> when onCreate is not provided', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 2 }];
      const { container } = render(<ManaPips pips={pips} />);
      const pip = container.querySelector('[data-pip]');
      expect(pip?.tagName.toLowerCase()).toBe('span');
    });
  });

  describe('interactive pips (with onCreate)', () => {
    it('renders as <button> when onCreate is provided', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 2, onCreate: vi.fn() }];
      const { container } = render(<ManaPips pips={pips} />);
      const pip = container.querySelector('[data-pip]');
      expect(pip?.tagName.toLowerCase()).toBe('button');
    });

    it('has aria-label equal to entityType', () => {
      const pips: ManaPip[] = [{ entityType: 'kb', count: 1, onCreate: vi.fn() }];
      render(<ManaPips pips={pips} />);
      expect(screen.getByRole('button', { name: 'kb' })).toBeInTheDocument();
    });

    it('calls onCreate when count=0 and pip is clicked', async () => {
      const onCreate = vi.fn();
      const pips: ManaPip[] = [{ entityType: 'agent', count: 0, onCreate }];
      render(<ManaPips pips={pips} />);
      await userEvent.click(screen.getByRole('button', { name: 'agent' }));
      expect(onCreate).toHaveBeenCalledTimes(1);
    });

    it('calls onCreate when count>0 and pip is clicked', async () => {
      const onCreate = vi.fn();
      const pips: ManaPip[] = [{ entityType: 'session', count: 3, onCreate }];
      render(<ManaPips pips={pips} />);
      await userEvent.click(screen.getByRole('button', { name: 'session' }));
      expect(onCreate).toHaveBeenCalledTimes(1);
    });

    it('calls onCreate when count is undefined and pip is clicked', async () => {
      const onCreate = vi.fn();
      const pips: ManaPip[] = [{ entityType: 'toolkit', onCreate }];
      render(<ManaPips pips={pips} />);
      await userEvent.click(screen.getByRole('button', { name: 'toolkit' }));
      expect(onCreate).toHaveBeenCalledTimes(1);
    });

    it('has hover:scale-125 transition class applied', () => {
      const pips: ManaPip[] = [{ entityType: 'game', count: 1, onCreate: vi.fn() }];
      const { container } = render(<ManaPips pips={pips} />);
      const pip = container.querySelector('[data-pip]');
      expect(pip?.className).toMatch(/hover:scale-125/);
    });
  });

  describe('mixed pips — some interactive, some not', () => {
    it('renders button for interactive and span for non-interactive', () => {
      const pips: ManaPip[] = [
        { entityType: 'session', count: 2, onCreate: vi.fn() },
        { entityType: 'kb', count: 1 },
      ];
      const { container } = render(<ManaPips pips={pips} />);
      const allPips = container.querySelectorAll('[data-pip]');
      expect(allPips[0].tagName.toLowerCase()).toBe('button');
      expect(allPips[1].tagName.toLowerCase()).toBe('span');
    });

    it('only interactive pip receives click handler', async () => {
      const onCreate = vi.fn();
      const pips: ManaPip[] = [
        { entityType: 'session', count: 0, onCreate },
        { entityType: 'kb', count: 1 },
      ];
      render(<ManaPips pips={pips} />);
      await userEvent.click(screen.getByRole('button', { name: 'session' }));
      expect(onCreate).toHaveBeenCalledTimes(1);
    });
  });

  describe('lg size — text label rendering', () => {
    it('renders text label with count and entityLabel for lg size with count>0', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 3, onCreate: vi.fn() }];
      render(<ManaPips pips={pips} size="lg" />);
      expect(screen.getByText('3 Session')).toBeInTheDocument();
    });

    it('renders text label with zero count for lg size with count=0', () => {
      const pips: ManaPip[] = [{ entityType: 'kb', count: 0, onCreate: vi.fn() }];
      render(<ManaPips pips={pips} size="lg" />);
      expect(screen.getByText('0 KB')).toBeInTheDocument();
    });

    it('does not render text label for md size', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 3 }];
      render(<ManaPips pips={pips} size="md" />);
      expect(screen.queryByText('3 Session')).not.toBeInTheDocument();
    });

    it('does not render text label for sm size', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 3 }];
      render(<ManaPips pips={pips} size="sm" />);
      expect(screen.queryByText('3 Session')).not.toBeInTheDocument();
    });
  });

  describe('backward compatibility', () => {
    it('existing pips without onCreate still render as span with data-pip', () => {
      const pips: ManaPip[] = [
        { entityType: 'session', count: 5 },
        { entityType: 'kb', count: 2 },
        { entityType: 'agent', count: 1 },
      ];
      const { container } = render(<ManaPips pips={pips} />);
      const dots = container.querySelectorAll('[data-pip]');
      expect(dots).toHaveLength(3);
      dots.forEach(dot => expect(dot.tagName.toLowerCase()).toBe('span'));
    });

    it('count badge still renders for md size on non-interactive pip', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 5 }];
      render(<ManaPips pips={pips} size="md" />);
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('no badge rendered when size=sm', () => {
      const pips: ManaPip[] = [{ entityType: 'session', count: 5 }];
      render(<ManaPips pips={pips} size="sm" />);
      expect(screen.queryByText('5')).not.toBeInTheDocument();
    });
  });
});
