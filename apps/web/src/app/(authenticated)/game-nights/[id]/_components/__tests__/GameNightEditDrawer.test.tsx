import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConflictError } from '@/lib/api/core/errors';

import { GameNightEditDrawer } from '../GameNightEditDrawer';

// ── Hook mocks ────────────────────────────────────────────────────────────────
const replaceMock = vi.fn();
const getSearchParamMock = vi.fn<(key: string) => string | null>();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: replaceMock }),
  useSearchParams: () => ({ get: getSearchParamMock }),
}));

const currentUserMock = vi.fn<() => { data: { id: string } | null | undefined }>();
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => currentUserMock(),
}));

const mutateMock = vi.fn();
const isPendingMock = { value: false };
vi.mock('@/hooks/queries/useGameNights', () => ({
  useUpdateGameNight: () => ({ mutate: mutateMock, isPending: isPendingMock.value }),
}));

const toastMock = vi.fn();
vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: toastMock }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  // identity translator — assert on keys, decoupled from copy
  useTranslation: () => ({ t: (key: string) => key, locale: 'it-IT' }),
}));

// jsdom shims (mirrors ui/drawer/drawer.test.tsx) — vaul/radix portal + pointer APIs
function installMatchMedia(matches: boolean): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue({
      matches,
      media: '(min-width: 768px)',
      addEventListener: () => {},
      removeEventListener: () => {},
      onchange: null,
    }),
  });
}

const ORGANIZER_ID = 'organizer-1';
const OTHER_USER_ID = 'intruder-9';

const baseProps = {
  gameNightId: 'gn-42',
  organizerId: ORGANIZER_ID,
  defaultValues: {
    title: 'Serata Catan',
    description: 'Porta gli snack',
    scheduledAt: '2026-08-01T20:00:00.000Z',
    location: 'Casa di Marco',
    maxPlayers: 6,
  },
};

beforeEach(() => {
  vi.clearAllMocks();
  isPendingMock.value = false;
  installMatchMedia(true); // desktop → Radix Dialog (deterministic in jsdom)
  currentUserMock.mockReturnValue({ data: { id: ORGANIZER_ID } });
  getSearchParamMock.mockImplementation((key: string) => (key === 'action' ? 'edit' : null));
});

describe('GameNightEditDrawer', () => {
  it('opens with prefilled form when ?action=edit and the viewer is the organizer', () => {
    render(<GameNightEditDrawer {...baseProps} />);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Serata Catan')).toBeInTheDocument();
  });

  it('IDOR guard: a non-organizer with ?action=edit never mounts the drawer', () => {
    currentUserMock.mockReturnValue({ data: { id: OTHER_USER_ID } });

    render(<GameNightEditDrawer {...baseProps} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue('Serata Catan')).not.toBeInTheDocument();
  });

  it('stays closed when the action param is absent', () => {
    getSearchParamMock.mockReturnValue(null);

    render(<GameNightEditDrawer {...baseProps} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('submits via useUpdateGameNight, shows the saved toast and closes on success', async () => {
    const user = userEvent.setup();
    mutateMock.mockImplementation((_vars, opts?: { onSuccess?: () => void }) =>
      opts?.onSuccess?.()
    );

    render(<GameNightEditDrawer {...baseProps} />);
    await user.click(screen.getByRole('button', { name: 'gameNightDetail.editDrawer.submit' }));

    expect(mutateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        id: 'gn-42',
        input: expect.objectContaining({ title: 'Serata Catan' }),
      }),
      expect.any(Object)
    );
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'gameNightDetail.editDrawer.savedToast' })
    );
    expect(replaceMock).toHaveBeenCalledWith('/game-nights/gn-42');
  });

  it('surfaces a reload-and-retry conflict toast on a 409 concurrent edit', async () => {
    const user = userEvent.setup();
    mutateMock.mockImplementation((_vars, opts?: { onError?: (e: unknown) => void }) =>
      opts?.onError?.(new ConflictError({ message: 'concurrent', code: 'concurrent_edit' }))
    );

    render(<GameNightEditDrawer {...baseProps} />);
    await user.click(screen.getByRole('button', { name: 'gameNightDetail.editDrawer.submit' }));

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'gameNightDetail.errors.concurrentEdit.title',
          variant: 'destructive',
        })
      )
    );
    // The success path must NOT run on conflict.
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it('shows a generic error toast on a non-conflict failure', async () => {
    const user = userEvent.setup();
    mutateMock.mockImplementation((_vars, opts?: { onError?: (e: unknown) => void }) =>
      opts?.onError?.(new Error('boom'))
    );

    render(<GameNightEditDrawer {...baseProps} />);
    await user.click(screen.getByRole('button', { name: 'gameNightDetail.editDrawer.submit' }));

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'gameNightDetail.editDrawer.errorGeneric',
          variant: 'destructive',
        })
      )
    );
  });
});
