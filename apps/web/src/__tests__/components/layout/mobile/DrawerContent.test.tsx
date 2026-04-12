import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { DrawerContent } from '@/components/layout/mobile/drawer/DrawerContent';

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

beforeEach(() => {
  useCascadeNavigationStore.setState(CLOSED_STATE);
});

describe('DrawerContent — game', () => {
  it('renders Info, Statistiche, Storico tabs', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    expect(screen.getByText('Info')).toBeDefined();
    expect(screen.getByText('Statistiche')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
  });

  it('renders Gioca and Apri footer actions', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    expect(screen.getByText('▶ Gioca')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });

  it('Info tab is active by default', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    const infoTab = screen.getByRole('tab', { name: 'Info' });
    expect(infoTab.getAttribute('aria-selected')).toBe('true');
  });

  it('switches tab on click', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    fireEvent.click(screen.getByRole('tab', { name: 'Statistiche' }));
    expect(screen.getByRole('tab', { name: 'Statistiche' }).getAttribute('aria-selected')).toBe(
      'true'
    );
    expect(screen.getByRole('tab', { name: 'Info' }).getAttribute('aria-selected')).toBe('false');
  });

  it('respects activeTab prop', () => {
    render(
      <DrawerContent entityType="game" entityId="g1" activeTab="Storico" onNavigate={() => {}} />
    );
    expect(screen.getByRole('tab', { name: 'Storico' }).getAttribute('aria-selected')).toBe('true');
  });
});

describe('DrawerContent — session', () => {
  it('renders Live, Toolkit, Timeline tabs', () => {
    render(<DrawerContent entityType="session" entityId="s1" onNavigate={() => {}} />);
    expect(screen.getByText('Live')).toBeDefined();
    expect(screen.getByText('Toolkit')).toBeDefined();
    expect(screen.getByText('Timeline')).toBeDefined();
  });

  it('renders Riprendi and Apri footer actions', () => {
    render(<DrawerContent entityType="session" entityId="s1" onNavigate={() => {}} />);
    expect(screen.getByText('▶ Riprendi')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });
});

describe('DrawerContent — agent', () => {
  it('renders Overview, Storico, Config tabs', () => {
    render(<DrawerContent entityType="agent" entityId="a1" onNavigate={() => {}} />);
    expect(screen.getByText('Overview')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
    expect(screen.getByText('Config')).toBeDefined();
  });

  it('renders Chat and Apri footer actions', () => {
    render(<DrawerContent entityType="agent" entityId="a1" onNavigate={() => {}} />);
    expect(screen.getByText('💬 Chat')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });
});

describe('DrawerContent — kb', () => {
  it('renders Overview, Preview, Citazioni tabs', () => {
    render(<DrawerContent entityType="kb" entityId="kb1" onNavigate={() => {}} />);
    expect(screen.getByText('Overview')).toBeDefined();
    expect(screen.getByText('Preview')).toBeDefined();
    expect(screen.getByText('Citazioni')).toBeDefined();
  });
});

describe('DrawerContent — chat', () => {
  it('renders Messaggi and Fonti tabs', () => {
    render(<DrawerContent entityType="chat" entityId="c1" onNavigate={() => {}} />);
    expect(screen.getByText('Messaggi')).toBeDefined();
    expect(screen.getByText('Fonti')).toBeDefined();
  });

  it('renders Continua and Archivia footer actions', () => {
    render(<DrawerContent entityType="chat" entityId="c1" onNavigate={() => {}} />);
    expect(screen.getByText('💬 Continua')).toBeDefined();
    expect(screen.getByText('📦 Archivia')).toBeDefined();
  });
});

describe('DrawerContent — player', () => {
  it('renders Profilo, Statistiche, Storico tabs', () => {
    render(<DrawerContent entityType="player" entityId="p1" onNavigate={() => {}} />);
    expect(screen.getByText('Profilo')).toBeDefined();
    expect(screen.getByText('Statistiche')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
  });
});

describe('DrawerContent — unknown', () => {
  it('renders null for unknown entityType', () => {
    const { container } = render(
      <DrawerContent entityType={'unknown' as any} entityId="x" onNavigate={() => {}} />
    );
    expect(container.firstChild).toBeNull();
  });
});
