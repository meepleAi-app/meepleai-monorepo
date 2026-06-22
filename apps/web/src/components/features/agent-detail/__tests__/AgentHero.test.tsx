/**
 * AgentHero unit tests — Wave C.2 Task 2
 *
 * 6 tests covering variant matrix × CTA presence per Phase 0.5 contract sez. 4.2.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentHero } from '../AgentHero';

const LABELS = {
  back: 'Torna agli agenti',
  backAriaLabel: 'Torna alla lista degli agenti',
  activeBadge: 'Attivo',
  draftBadge: 'In setup',
  archivedBadge: 'Archiviato',
  metaType: 'Tipo: {type}',
  metaModel: 'Modello: {model}',
  metaCreated: 'Creato il {date}',
  metaLastUsed: 'Ultimo utilizzo: {date}',
  metaLastUsedNever: 'Mai utilizzato',
  metaInvocations: '{count} invocazioni',
  metaGameNone: 'Agente standalone',
  ctaPlay: 'Avvia chat',
  ctaSetup: 'Continua setup',
  ctaUnarchive: 'Riattiva',
  ctaShare: 'Condividi',
  ctaShareAriaLabel: 'Condividi questo agente',
  setupBannerTitle: 'Questo agente è in fase di setup',
  setupBannerSubtitle: 'Completa la configurazione per iniziare a usarlo.',
  archivedBannerTitle: 'Questo agente è archiviato',
  archivedBannerSubtitle: 'Riattivalo per poterlo utilizzare nuovamente.',
};

const BASE_PROPS = {
  name: 'Mago di Wingspan',
  avatar: '🧙‍♂️',
  persona: 'Esperto di strategia',
  meta: {
    type: 'agent',
    model: 'Claude Sonnet 4',
    createdAt: '12 Gen 2026',
    lastUsed: '5 minuti fa',
    invocations: 230,
  },
  labels: LABELS,
};

describe('AgentHero', () => {
  it('renders data-slot attribute', () => {
    render(<AgentHero {...BASE_PROPS} variant="active" />);
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeTruthy();
  });

  it('active variant: renders Play CTA when callback provided', () => {
    const onPlay = vi.fn();
    render(<AgentHero {...BASE_PROPS} variant="active" ctaPlay={onPlay} />);
    expect(screen.getByRole('button', { name: /avvia chat/i })).toBeInTheDocument();
  });

  it('active variant: no setup or unarchive banner', () => {
    render(<AgentHero {...BASE_PROPS} variant="active" />);
    expect(screen.queryByText(/fase di setup/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/archiviato/i)).not.toBeInTheDocument();
  });

  it('draft variant: renders setup banner and Setup CTA', () => {
    const onSetup = vi.fn();
    render(<AgentHero {...BASE_PROPS} variant="draft" ctaSetup={onSetup} />);
    expect(screen.getByText(/fase di setup/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /continua setup/i })).toBeInTheDocument();
    // Play button should NOT appear for draft
    expect(screen.queryByRole('button', { name: /avvia chat/i })).not.toBeInTheDocument();
  });

  it('archived variant: renders archived banner and Unarchive CTA', () => {
    const onUnarchive = vi.fn();
    render(<AgentHero {...BASE_PROPS} variant="archived" ctaUnarchive={onUnarchive} />);
    expect(screen.getByText(/agente è archiviato/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /riattiva/i })).toBeInTheDocument();
    // Play button should NOT appear for archived
    expect(screen.queryByRole('button', { name: /avvia chat/i })).not.toBeInTheDocument();
  });

  it('optional CTA absent → button NOT rendered', () => {
    // No ctaPlay, ctaSetup, ctaUnarchive, ctaShare provided
    render(<AgentHero {...BASE_PROPS} variant="active" />);
    expect(screen.queryByRole('button', { name: /avvia chat/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /condividi/i })).not.toBeInTheDocument();
  });
});
