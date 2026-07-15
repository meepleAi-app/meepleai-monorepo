import { clsx } from 'clsx';

export interface PendingRsvpCardProps {
  eventId: string;
  title: string;
  inviterName: string;
  onConfirm: () => void;
  onDecline: () => void;
  disabled?: boolean;
  className?: string;
}

/**
 * Card "pending-RSVP" per la dashboard mobile (invariante #17): l'invitato vede
 * la serata come "Da confermare" finché non risponde. Componente puro — riceve
 * i dati e i callback, non conosce hook/backend.
 */
export function PendingRsvpCard({
  eventId,
  title,
  inviterName,
  onConfirm,
  onDecline,
  disabled = false,
  className,
}: PendingRsvpCardProps) {
  return (
    <div
      data-testid="pending-rsvp-card"
      data-event-id={eventId}
      title={disabled ? 'Offline — RSVP disponibile alla riconnessione' : undefined}
      className={clsx(
        'rounded-lg border border-dashed border-warning/50 bg-warning/[0.06] p-4',
        disabled && 'opacity-70',
        className
      )}
    >
      <span className="inline-flex items-center rounded-full bg-warning/15 px-2 py-0.5 text-xs font-bold text-warning-ink">
        Da confermare
      </span>
      <h3 className="mt-2 font-quicksand font-bold text-base text-entity-event">{title}</h3>
      <p className="mt-0.5 text-sm text-muted-foreground">{inviterName} ti ha invitato</p>
      <div className="mt-3 flex gap-2">
        <button
          type="button"
          onClick={onConfirm}
          disabled={disabled}
          className="min-h-11 flex-1 rounded-md bg-entity-event font-quicksand font-bold text-sm text-white transition-colors disabled:opacity-60 motion-reduce:transition-none"
        >
          Conferma
        </button>
        <button
          type="button"
          onClick={onDecline}
          disabled={disabled}
          className="min-h-11 flex-1 rounded-md border border-border-strong font-quicksand font-bold text-sm text-muted-foreground transition-colors disabled:opacity-60 motion-reduce:transition-none"
        >
          Declina
        </button>
      </div>
    </div>
  );
}
