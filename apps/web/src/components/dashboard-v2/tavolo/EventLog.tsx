'use client';

export interface EventLogItem {
  id: string;
  message: string;
  isRecent: boolean;
}

interface EventLogProps {
  events: EventLogItem[];
}

const MAX_VISIBLE = 5;

export function EventLog({ events }: EventLogProps) {
  const visible = events.slice(-MAX_VISIBLE);

  return (
    <div data-testid="event-log" className="space-y-1.5">
      <p className="font-quicksand text-xs font-bold uppercase tracking-wide text-muted-foreground">
        Ultimi eventi
      </p>

      {visible.length === 0 ? (
        <p className="font-nunito text-xs text-muted-foreground">Nessun evento</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {visible.map(event => (
            <li
              key={event.id}
              className={`font-nunito text-xs ${
                event.isRecent ? 'text-foreground' : 'text-muted-foreground'
              }`}
            >
              {event.message}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
