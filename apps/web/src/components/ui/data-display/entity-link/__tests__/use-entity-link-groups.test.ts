import { groupEntityLinks } from '../use-entity-link-groups';
import type { EntityLinkDto } from '@/lib/api/schemas/entity-links.schemas';

/**
 * Helper to build a minimal EntityLinkDto for testing.
 * Only sourceEntityType, sourceEntityId, targetEntityType, targetEntityId,
 * linkType, isOwner, and isBggImported are semantically relevant to grouping;
 * remaining fields use sensible defaults.
 */
function makeLink(
  overrides: Partial<EntityLinkDto> &
    Pick<EntityLinkDto, 'id' | 'targetEntityType' | 'targetEntityId'>
): EntityLinkDto {
  return {
    sourceEntityType: 'Game',
    sourceEntityId: 'g1',
    linkType: 'RelatedTo',
    isBidirectional: false,
    scope: 'User',
    ownerUserId: '00000000-0000-0000-0000-000000000001',
    metadata: null,
    isAdminApproved: false,
    isBggImported: false,
    isOwner: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('groupEntityLinks', () => {
  it('returns empty array for empty links', () => {
    expect(groupEntityLinks([])).toEqual([]);
  });

  it('groups links by targetEntityType with count', () => {
    const links: EntityLinkDto[] = [
      makeLink({ id: '1', targetEntityType: 'Session', targetEntityId: 's1' }),
      makeLink({ id: '2', targetEntityType: 'Session', targetEntityId: 's2' }),
      makeLink({ id: '3', targetEntityType: 'Agent', targetEntityId: 'a1' }),
    ];
    const groups = groupEntityLinks(links);
    expect(groups).toHaveLength(2);
    expect(groups).toContainEqual(expect.objectContaining({ entityType: 'Session', count: 2 }));
    expect(groups).toContainEqual(expect.objectContaining({ entityType: 'Agent', count: 1 }));
  });

  it('excludes entity types with count 0', () => {
    const groups = groupEntityLinks([]);
    expect(groups.find(g => g.count === 0)).toBeUndefined();
  });

  it('includes KbCard entity type', () => {
    const links: EntityLinkDto[] = [
      makeLink({ id: '1', targetEntityType: 'KbCard', targetEntityId: 'k1' }),
    ];
    const groups = groupEntityLinks(links);
    expect(groups).toContainEqual(expect.objectContaining({ entityType: 'KbCard', count: 1 }));
  });

  it('uses metadata as firstTargetName for the first link in a group', () => {
    const links: EntityLinkDto[] = [
      makeLink({ id: '1', targetEntityType: 'Game', targetEntityId: 'g2', metadata: 'Catan' }),
      makeLink({ id: '2', targetEntityType: 'Game', targetEntityId: 'g3', metadata: 'Wingspan' }),
    ];
    const groups = groupEntityLinks(links);
    const gameGroup = groups.find(g => g.entityType === 'Game');
    expect(gameGroup).toBeDefined();
    expect(gameGroup!.count).toBe(2);
    expect(gameGroup!.firstTargetName).toBe('Catan');
  });

  it('sets firstTargetName to undefined when metadata is null', () => {
    const links: EntityLinkDto[] = [
      makeLink({ id: '1', targetEntityType: 'Document', targetEntityId: 'd1', metadata: null }),
    ];
    const groups = groupEntityLinks(links);
    expect(groups[0].firstTargetName).toBeUndefined();
  });
});
