/**
 * @vitest-environment jsdom
 *
 * GameRelationships component tests
 * Issue #US-43 — Entity Relationships frontend integration
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { EntityLinkDto } from '@/components/ui/data-display/entity-link/entity-link-types';
import type { UseEntityLinksResult } from '@/components/ui/data-display/entity-link/use-entity-links';

import { GameRelationships } from '../GameRelationships';

// ============================================================================
// Mock useEntityLinks hook
// ============================================================================

const mockUseEntityLinks = vi.fn<(entityType: string, entityId: string) => UseEntityLinksResult>();

vi.mock('@/components/ui/data-display/entity-link/use-entity-links', () => ({
  useEntityLinks: (...args: [string, string]) => mockUseEntityLinks(...args),
}));

// ============================================================================
// Test data
// ============================================================================

function createMockLink(overrides: Partial<EntityLinkDto> = {}): EntityLinkDto {
  return {
    id: 'link-1',
    sourceEntityType: 'Game',
    sourceEntityId: 'game-1',
    targetEntityType: 'Game',
    targetEntityId: 'game-2',
    linkType: 'ExpansionOf',
    isBidirectional: false,
    scope: 'Shared',
    ownerUserId: 'user-1',
    metadata: JSON.stringify({ name: 'Wingspan: European Expansion' }),
    isAdminApproved: false,
    isBggImported: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    isOwner: false,
    ...overrides,
  };
}

const EXPANSION_LINK = createMockLink({
  id: 'link-1',
  linkType: 'ExpansionOf',
  targetEntityId: 'game-exp-1',
  metadata: JSON.stringify({ name: 'Wingspan: European Expansion' }),
});

const RELATED_LINK = createMockLink({
  id: 'link-2',
  linkType: 'RelatedTo',
  targetEntityId: 'game-rel-1',
  metadata: JSON.stringify({ name: 'Everdell' }),
});

const COMPANION_LINK = createMockLink({
  id: 'link-3',
  linkType: 'CompanionTo',
  targetEntityId: 'game-comp-1',
  metadata: JSON.stringify({ name: 'Wingspan: Oceania Expansion' }),
  isBidirectional: true,
});

// ============================================================================
// Tests
// ============================================================================

describe('GameRelationships', () => {
  it('renders grouped links with Italian labels', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [EXPANSION_LINK, RELATED_LINK],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" gameName="Wingspan" />);

    // Section header
    expect(screen.getByText('Relazioni')).toBeInTheDocument();

    // Group headers (Italian)
    expect(screen.getByText('Espansioni')).toBeInTheDocument();
    expect(screen.getByText('Giochi Correlati')).toBeInTheDocument();

    // Entity names from metadata
    expect(screen.getByText('Wingspan: European Expansion')).toBeInTheDocument();
    expect(screen.getByText('Everdell')).toBeInTheDocument();
  });

  it('returns null when no links exist', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    const { container } = render(<GameRelationships gameId="game-1" />);
    expect(container.innerHTML).toBe('');
  });

  it('returns null on API error (silent failure for supplementary content)', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [],
      loading: false,
      error: 'Network error',
      refresh: vi.fn(),
    });

    const { container } = render(<GameRelationships gameId="game-1" />);
    expect(container.innerHTML).toBe('');
  });

  it('shows loading spinner while fetching', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [],
      loading: true,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" />);
    expect(screen.getByTestId('game-relationships-loading')).toBeInTheDocument();
  });

  it('renders multiple groups correctly', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [EXPANSION_LINK, RELATED_LINK, COMPANION_LINK],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" />);

    // Three groups
    expect(screen.getByTestId('relationship-group-ExpansionOf')).toBeInTheDocument();
    expect(screen.getByTestId('relationship-group-CompanionTo')).toBeInTheDocument();
    expect(screen.getByTestId('relationship-group-RelatedTo')).toBeInTheDocument();

    // Total count badge
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('shows BGG badge for imported links', () => {
    const bggLink = createMockLink({
      id: 'link-bgg',
      linkType: 'ExpansionOf',
      isBggImported: true,
      metadata: JSON.stringify({ name: 'BGG Expansion' }),
    });

    mockUseEntityLinks.mockReturnValue({
      links: [bggLink],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" />);
    expect(screen.getByText('Ext')).toBeInTheDocument();
  });

  it('resolves target name from metadata string (non-JSON fallback)', () => {
    const plainMetadataLink = createMockLink({
      id: 'link-plain',
      linkType: 'RelatedTo',
      metadata: 'Some Plain Name',
    });

    mockUseEntityLinks.mockReturnValue({
      links: [plainMetadataLink],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" />);
    expect(screen.getByText('Some Plain Name')).toBeInTheDocument();
  });

  it('falls back to entity type + truncated ID when no metadata', () => {
    const noMetadataLink = createMockLink({
      id: 'link-no-meta',
      linkType: 'SequelOf',
      targetEntityId: 'abcdefgh-1234-5678-9012-abcdefghijkl',
      metadata: null,
    });

    mockUseEntityLinks.mockReturnValue({
      links: [noMetadataLink],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" />);
    expect(screen.getByText('Game abcdefgh...')).toBeInTheDocument();
  });

  it('passes entityType "Game" to useEntityLinks', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="test-id" />);
    expect(mockUseEntityLinks).toHaveBeenCalledWith('Game', 'test-id');
  });

  it('sets aria-label with game name when provided', () => {
    mockUseEntityLinks.mockReturnValue({
      links: [EXPANSION_LINK],
      loading: false,
      error: null,
      refresh: vi.fn(),
    });

    render(<GameRelationships gameId="game-1" gameName="Wingspan" />);
    expect(screen.getByLabelText('Relazioni di Wingspan')).toBeInTheDocument();
  });
});
