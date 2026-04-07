import type { LinkedEntityInfo } from '@/types/linked-entity';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

/**
 * Maps UserLibraryEntry DTO fields to LinkedEntityInfo[] for ManaLinkFooter.
 * ManaPip KB appears ONLY if kbIndexedCount > 0 (per spec DD-02).
 */
export function mapLibraryEntryToLinkedEntities(entry: UserLibraryEntry): LinkedEntityInfo[] {
  const entities: LinkedEntityInfo[] = [];

  // Game pip is always present (source entity)
  entities.push({ entityType: 'game', count: 1 });

  // KB pip: only if at least 1 document is indexed (Ready)
  if (entry.kbIndexedCount > 0) {
    entities.push({ entityType: 'kb', count: entry.kbIndexedCount });
  }

  return entities;
}
