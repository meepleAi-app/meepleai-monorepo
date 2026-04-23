import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConnectionChipPopover } from '../ConnectionChipPopover';

const items = [
  { id: '1', label: 'Wingspan Night #12', href: '/sessions/1' },
  { id: '2', label: 'Wingspan Night #13', href: '/sessions/2' },
];

describe('ConnectionChipPopover', () => {
  it('renders children as trigger', () => {
    render(
      <ConnectionChipPopover
        open={false}
        onOpenChange={() => {}}
        items={items}
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    expect(screen.getByRole('button', { name: /trigger/i })).toBeInTheDocument();
  });

  it('shows items when open', () => {
    render(
      <ConnectionChipPopover open onOpenChange={() => {}} items={items} entityType="session">
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    expect(screen.getByText('Wingspan Night #12')).toBeInTheDocument();
    expect(screen.getByText('Wingspan Night #13')).toBeInTheDocument();
  });

  it('renders create button when onCreate provided', () => {
    const onCreate = vi.fn();
    render(
      <ConnectionChipPopover
        open
        onOpenChange={() => {}}
        items={items}
        onCreate={onCreate}
        createLabel="Nuova sessione"
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    expect(screen.getByRole('button', { name: /nuova sessione/i })).toBeInTheDocument();
  });

  it('calls onCreate and closes popover on create click', async () => {
    const onCreate = vi.fn();
    const onOpenChange = vi.fn();
    render(
      <ConnectionChipPopover
        open
        onOpenChange={onOpenChange}
        items={[]}
        onCreate={onCreate}
        createLabel="Nuova sessione"
        entityType="session"
      >
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    await userEvent.click(screen.getByRole('button', { name: /nuova sessione/i }));
    expect(onCreate).toHaveBeenCalledTimes(1);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('renders Lucide icon matching entity type (not emoji)', () => {
    render(
      <ConnectionChipPopover open onOpenChange={() => {}} items={items} entityType="session">
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    // Radix Popover renders content in a Portal; query the full document.
    expect(document.querySelector('svg')).toBeTruthy();
  });

  it('PopoverContent has aria-labelledby pointing to the header title', () => {
    render(
      <ConnectionChipPopover open onOpenChange={() => {}} items={items} entityType="session">
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    // Radix Popover renders content in a Portal. PopoverContent exposes role="dialog" by default.
    const content = document.querySelector('[role="dialog"]');
    expect(content).toBeTruthy();

    const labelledBy = content?.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();

    const header = document.getElementById(labelledBy!);
    expect(header).toBeTruthy();
    // Header mostra "Session (N)" — il testo include l'entity label
    expect(header?.textContent?.toLowerCase()).toMatch(/session/);
  });

  it('pressing Escape closes the popover', async () => {
    const onOpenChange = vi.fn();
    render(
      <ConnectionChipPopover open onOpenChange={onOpenChange} items={items} entityType="session">
        <button>trigger</button>
      </ConnectionChipPopover>
    );
    // Radix ascolta Escape a livello document; il focus non deve essere forzato dentro il popover.
    await userEvent.keyboard('{Escape}');
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
