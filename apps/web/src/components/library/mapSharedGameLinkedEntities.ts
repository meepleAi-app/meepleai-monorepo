import type { LinkedEntityInfo } from '@/components/ui/data-display/meeple-card-features/ManaLinkFooter';

/**
 * Maps shared game fields to LinkedEntityInfo[] for ManaLinkFooter.
 * ManaPip KB appears when isRagPublic is true (game has public KB docs).
 *
 * Unlike personal library entries which track kbIndexedCount,
 * shared games only expose a boolean flag.
 */
export function mapSharedGameToLinkedEntities(game: {
  isRagPublic?: boolean;
}): LinkedEntityInfo[] {
  const entities: LinkedEntityInfo[] = [{ entityType: 'game', count: 1 }];

  if (game.isRagPublic) {
    entities.push({ entityType: 'kb', count: 1 });
  }

  return entities;
}
