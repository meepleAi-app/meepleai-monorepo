import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

/** A linked entity pip shown in the card's mana link footer */
export interface LinkedEntityInfo {
  entityType: MeepleEntityType;
  count: number;
}
