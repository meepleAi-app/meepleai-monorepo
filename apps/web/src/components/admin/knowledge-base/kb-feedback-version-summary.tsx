'use client';

import { ThumbsDown, ThumbsUp } from 'lucide-react';

import type { AdminKbFeedbackVersion } from '@/lib/api/schemas/knowledge-base.schemas';

/** La chiave del filtro per i feedback senza versione: è il valore che l'API accetta (#4469). */
export const WITHOUT_VERSION = 'none';

/** La chiave di una voce del riepilogo nel filtro della lista. */
export function versionKey(version: Pick<AdminKbFeedbackVersion, 'profileVersionId'>): string {
  return version.profileVersionId ?? WITHOUT_VERSION;
}

/** Il nome di una versione: «v4», «Senza versione», o una versione che il profilo non ha più. */
export function versionLabel(
  version: Pick<AdminKbFeedbackVersion, 'profileVersionId' | 'versionNumber'>
): string {
  if (version.profileVersionId === null) return 'Senza versione';
  return version.versionNumber === null ? 'Versione sconosciuta' : `v${version.versionNumber}`;
}

const STATUS_LABELS: Record<string, string> = {
  Published: 'Pubblicata',
  Archived: 'Archiviata',
  Draft: 'Bozza',
};

interface Props {
  versions: AdminKbFeedbackVersion[];
  /** La versione filtrata nella lista, con la chiave di {@link versionKey}; null per tutte. */
  selected: string | null;
  onSelect: (key: string | null) => void;
}

/**
 * #4469 (ADR-095 fetta 7a): i 👍👎 per versione del profilo. Un clic su una versione filtra la lista;
 * un secondo clic toglie il filtro.
 */
export function KbFeedbackVersionSummary({ versions, selected, onSelect }: Props) {
  if (versions.length === 0) {
    return (
      <p className="font-mono text-[11px] text-muted-foreground">
        Nessun feedback sulle risposte, per ora.
      </p>
    );
  }

  return (
    <section
      aria-label="Feedback per versione del profilo"
      className="rounded-[10px] border border-border/60 bg-card overflow-hidden"
    >
      <ul className="divide-y divide-border/60">
        {versions.map(version => {
          const key = versionKey(version);
          const isSelected = selected === key;
          return (
            <li key={key}>
              <button
                type="button"
                aria-pressed={isSelected}
                onClick={() => onSelect(isSelected ? null : key)}
                className={`flex w-full items-center gap-3 px-3.5 py-2 text-left hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                  isSelected ? 'bg-muted' : ''
                }`}
              >
                <span className="font-quicksand text-sm font-bold text-foreground w-36">
                  {versionLabel(version)}
                </span>
                <span className="text-[11px] text-muted-foreground flex-1">
                  {version.status ? (STATUS_LABELS[version.status] ?? version.status) : ''}
                  {version.publishedAt
                    ? ` dal ${new Date(version.publishedAt).toLocaleDateString('it-IT')}`
                    : ''}
                </span>
                <span
                  className="flex items-center gap-1 font-mono text-[12px] text-entity-toolkit"
                  aria-label={`${version.helpful} utili`}
                >
                  <ThumbsUp className="h-3.5 w-3.5" aria-hidden="true" />
                  {version.helpful}
                </span>
                <span
                  className="flex items-center gap-1 font-mono text-[12px] text-entity-event"
                  aria-label={`${version.notHelpful} non utili`}
                >
                  <ThumbsDown className="h-3.5 w-3.5" aria-hidden="true" />
                  {version.notHelpful}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
