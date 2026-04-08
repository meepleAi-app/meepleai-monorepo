import type { DiaryEntry } from '@/stores/game-night/types';

interface Props {
  entry: DiaryEntry;
}

export function GameNightDiaryEntry({ entry }: Props) {
  const time = new Intl.DateTimeFormat('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(entry.timestamp));

  return (
    <div className="flex items-start gap-3 py-2">
      <span className="text-xs text-muted-foreground font-mono w-12 shrink-0 pt-0.5">{time}</span>
      <p className="text-sm leading-relaxed">{entry.description}</p>
    </div>
  );
}
