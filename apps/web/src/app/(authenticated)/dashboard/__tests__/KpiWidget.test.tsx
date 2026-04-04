import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KpiWidget } from '../widgets/KpiWidget';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }));

describe('KpiWidget', () => {
  beforeEach(() => mockPush.mockClear());

  it('renders label and value', () => {
    render(
      <KpiWidget
        label="Partite (mese)"
        value={12}
        accentColor="hsl(25,95%,45%)"
        colSpan={4}
        rowSpan={2}
      />
    );
    expect(screen.getByText('Partite (mese)')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('renders badge with emerald styles when badgePositive', () => {
    render(
      <KpiWidget
        label="x"
        value={1}
        badge="+15%"
        badgePositive={true}
        accentColor="hsl(25,95%,45%)"
        colSpan={4}
        rowSpan={2}
      />
    );
    expect(screen.getByText('+15%').className).toContain('emerald');
  });

  it('renders badge with neutral styles when not badgePositive', () => {
    render(
      <KpiWidget
        label="x"
        value={1}
        badge="-5%"
        badgePositive={false}
        accentColor="hsl(25,95%,45%)"
        colSpan={4}
        rowSpan={2}
      />
    );
    expect(screen.getByText('-5%').className).not.toContain('emerald');
  });

  it('navigates to href when clicked', async () => {
    const user = userEvent.setup();
    render(
      <KpiWidget
        label="Sessioni"
        value={42}
        accentColor="hsl(25,95%,45%)"
        colSpan={4}
        rowSpan={2}
        href="/sessions"
      />
    );
    await user.click(screen.getByText('42'));
    expect(mockPush).toHaveBeenCalledWith('/sessions');
  });

  it('renders sub text when no badge', () => {
    render(
      <KpiWidget
        label="Ore"
        value="3h"
        sub="questa settimana"
        accentColor="hsl(240,60%,55%)"
        colSpan={3}
        rowSpan={2}
      />
    );
    expect(screen.getByText('questa settimana')).toBeInTheDocument();
  });
});
