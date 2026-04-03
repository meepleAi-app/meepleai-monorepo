import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

export function buildPlayerCardProps(player: SessionPlayer): Partial<MeepleCardProps> {
  return {
    title: player.displayName,
  };
}
