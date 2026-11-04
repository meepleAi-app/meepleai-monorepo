import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Aaron', email: 'aaron@test.com', role: 'admin' },
  }),
}));

import { MobileBottomBar, isImmersiveRoute } from '@/components/layout/AppNav/MobileBottomBar';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

describe('MobileBottomBar', () => {
  it('renders the 5 fixed sp4 tabs (Dashboard tab labelled "Home")', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeDefined();
    expect(screen.getByText('Libreria')).toBeDefined();
    // PR #2279 (#2190 nav cleanup, 2026-06-13) renamed "Hub" → "Games".
    expect(screen.getByText('Games')).toBeDefined();
    expect(screen.getByText('Chat')).toBeDefined();
    expect(screen.getByText('Profilo')).toBeDefined();
    // The override means the dashboard tab does NOT show "Dashboard".
    expect(screen.queryByText('Dashboard')).toBeNull();
  });

  it('marks the active tab with aria-current="page"', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Libreria').closest('a')?.getAttribute('aria-current')).toBe('page');
    expect(screen.getByText('Home').closest('a')?.getAttribute('aria-current')).toBeNull();
  });

  it('links the Home tab to /dashboard', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home').closest('a')?.getAttribute('href')).toBe('/dashboard');
  });
});

describe('MobileBottomBar — dynamic slot-3 (Issue #2150 D6)', () => {
  // The live session store is the real Zustand store; we drive it directly
  // via its `setSession`/`reset` actions. This avoids over-mocking and
  // exercises the actual selector wiring.
  function withLiveSession(
    sessionId: string,
    status: 'InProgress' | 'Paused' | 'Completed' = 'InProgress'
  ) {
    useLiveSessionStore.setState({ sessionId, status, gameName: 'Catan' });
  }

  function resetSession() {
    useLiveSessionStore.getState().reset();
  }

  it('swaps the Chat slot for a Live link when a session is in progress', () => {
    withLiveSession('sess-1', 'InProgress');
    try {
      render(<MobileBottomBar />);
      expect(screen.queryByText('Chat')).toBeNull();
      expect(screen.getByText('Live')).toBeDefined();
      expect(screen.getByTestId('bottom-tab-live').getAttribute('href')).toBe(
        '/sessions/sess-1/live'
      );
    } finally {
      resetSession();
    }
  });

  it('keeps the Chat slot when the session is Completed', () => {
    withLiveSession('sess-1', 'Completed');
    try {
      render(<MobileBottomBar />);
      expect(screen.getByText('Chat')).toBeDefined();
      expect(screen.queryByText('Live')).toBeNull();
    } finally {
      resetSession();
    }
  });

  it('keeps the Chat slot when no session is active', () => {
    resetSession(); // ensure clean state
    render(<MobileBottomBar />);
    expect(screen.getByText('Chat')).toBeDefined();
    expect(screen.queryByText('Live')).toBeNull();
  });

  it('also swaps the slot when the session is Paused (still actively live)', () => {
    withLiveSession('sess-1', 'Paused');
    try {
      render(<MobileBottomBar />);
      expect(screen.getByText('Live')).toBeDefined();
      expect(screen.queryByText('Chat')).toBeNull();
    } finally {
      resetSession();
    }
  });
});

describe('MobileBottomBar — il banner cookie non deve coprirla (#4060)', () => {
  it('ancora il `bottom` a --cookie-banner-height invece che a 0', () => {
    render(<MobileBottomBar />);
    const nav = screen.getByTestId('mobile-bottom-bar');

    // 🔴 Il difetto che questa asserzione fissa, misurato con `document.elementFromPoint` sul
    // centro della barra a 390x844: con `bottom-0` il banner cookie (`fixed bottom-0 z-50`,
    // 281px a questa larghezza perche' la riga dei pulsanti va a capo) la copriva su TUTTE e
    // sei le rotte provate. Non e' un controllo secondario: e' la navigazione primaria mobile,
    // e finche' il consenso non e' dato l'utente non puo' cambiare sezione.
    //
    // Il banner e' a z-50 e questa barra a z-40: nessun ordine di DOM la salva, va spostata.
    expect(nav.className).toContain('bottom-[var(--cookie-banner-height,0px)]');

    // E il controllo che impedisce alla precedente di passare per sbaglio: `bottom-0` non deve
    // tornare. Senza questo, una regressione che AGGIUNGE `bottom-0` accanto alla variabile
    // lascerebbe il test verde mentre l'ultima classe vince.
    expect(nav.className).not.toMatch(/bottom-0/);
  });
});

describe('isImmersiveRoute', () => {
  it('is true on in-session routes', () => {
    expect(isImmersiveRoute('/sessions/abc-123/live')).toBe(true);
    expect(isImmersiveRoute('/library/catan/play')).toBe(true);
    expect(isImmersiveRoute('/library/catan/play/4')).toBe(true);
  });

  it('is false on normal routes', () => {
    expect(isImmersiveRoute('/library')).toBe(false);
    expect(isImmersiveRoute('/dashboard')).toBe(false);
    expect(isImmersiveRoute('/sessions')).toBe(false);
  });
});
