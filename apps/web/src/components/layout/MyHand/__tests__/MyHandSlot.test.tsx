import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MyHandSlot } from '../MyHandSlot';

describe('MyHandSlot', () => {
  it('renders empty state with CTA', () => {
    render(
      <MyHandSlot
        slotType="toolkit"
        slot={{
          slotType: 'toolkit',
          entityId: null,
          entityType: null,
          entityLabel: null,
          entityImageUrl: null,
          pinnedAt: null,
          isEntityValid: true,
        }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText(/Toolkit/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /seleziona/i })).toBeInTheDocument();
  });

  it('renders populated state with entity label', () => {
    render(
      <MyHandSlot
        slotType="game"
        slot={{
          slotType: 'game',
          entityId: 'g-1',
          entityType: 'game',
          entityLabel: 'Agricola',
          entityImageUrl: null,
          pinnedAt: '2026-04-09T00:00:00Z',
          isEntityValid: true,
        }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText('Agricola')).toBeInTheDocument();
  });

  it('renders degraded state with warning', () => {
    render(
      <MyHandSlot
        slotType="session"
        slot={{
          slotType: 'session',
          entityId: 's-1',
          entityType: 'session',
          entityLabel: 'Partita',
          entityImageUrl: null,
          pinnedAt: '2026-04-09T00:00:00Z',
          isEntityValid: false,
        }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText(/non più disponibile/i)).toBeInTheDocument();
  });

  it('calls onAssign when CTA clicked in empty state', () => {
    const onAssign = vi.fn();
    render(
      <MyHandSlot
        slotType="toolkit"
        slot={{
          slotType: 'toolkit',
          entityId: null,
          entityType: null,
          entityLabel: null,
          entityImageUrl: null,
          pinnedAt: null,
          isEntityValid: true,
        }}
        onAssign={onAssign}
        onClear={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /seleziona/i }));
    expect(onAssign).toHaveBeenCalledWith('toolkit');
  });

  it('calls onClear when clear button clicked', () => {
    const onClear = vi.fn();
    render(
      <MyHandSlot
        slotType="game"
        slot={{
          slotType: 'game',
          entityId: 'g-1',
          entityType: 'game',
          entityLabel: 'Agricola',
          entityImageUrl: null,
          pinnedAt: '2026-04-09T00:00:00Z',
          isEntityValid: true,
        }}
        onAssign={vi.fn()}
        onClear={onClear}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /rimuovi/i }));
    expect(onClear).toHaveBeenCalledWith('game');
  });
});
