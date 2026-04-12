import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import type { DiaryEntry } from '@/stores/game-night/types';

import { GameNightDiaryEntry } from './GameNightDiaryEntry';

interface Props {
  entries: DiaryEntry[];
}

export function GameNightDiary({ entries }: Props) {
  if (entries.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground text-sm">
        Nessun evento registrato ancora.
      </div>
    );
  }

  return (
    <ScrollArea className="h-[400px]">
      <div className="space-y-0.5 px-1">
        {entries.map(entry => (
          <GameNightDiaryEntry key={entry.id} entry={entry} />
        ))}
      </div>
    </ScrollArea>
  );
}
