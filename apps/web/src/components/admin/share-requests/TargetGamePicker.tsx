'use client';

/**
 * #5076: picks the existing shared game a proposal is approved onto (ApproveAsVariant, MergeKnowledgeBase). It
 * searches the published catalog: the public search returns published games only.
 */

import { useEffect, useState, type JSX } from 'react';

import { Input } from '@/components/ui/primitives/input';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { useGameTitle } from '@/lib/i18n/use-game-title';

export interface TargetGame {
  id: string;
  title: string;
  yearPublished: number;
}

interface TargetGamePickerProps {
  value: TargetGame | null;
  onChange: (game: TargetGame | null) => void;
}

const MIN_SEARCH = 2;

function TargetGameOption({
  game,
  selected,
  onToggle,
}: {
  game: SharedGame;
  selected: boolean;
  onToggle: (game: TargetGame | null) => void;
}): JSX.Element {
  const { value: title } = useGameTitle(game);

  return (
    <li>
      <button
        type="button"
        aria-pressed={selected}
        onClick={() =>
          onToggle(selected ? null : { id: game.id, title, yearPublished: game.yearPublished })
        }
        className={`w-full rounded-md border px-3 py-2 text-left text-sm transition-colors hover:bg-muted ${
          selected ? 'border-primary bg-muted' : 'border-border'
        }`}
      >
        {title} <span className="text-muted-foreground">({game.yearPublished})</span>
      </button>
    </li>
  );
}

export function TargetGamePicker({ value, onChange }: TargetGamePickerProps): JSX.Element {
  const [input, setInput] = useState('');
  const [term, setTerm] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setTerm(input.trim()), 300);
    return () => clearTimeout(timer);
  }, [input]);

  const enabled = term.length >= MIN_SEARCH;
  const { data, isFetching } = useSharedGames({ searchTerm: term, pageSize: 8 }, enabled, true);
  const games = enabled ? (data?.items ?? []) : [];

  return (
    <div className="space-y-2">
      <Input
        type="search"
        value={input}
        onChange={e => setInput(e.target.value)}
        placeholder="Cerca un gioco pubblicato nel catalogo"
        aria-label="Gioco del catalogo"
      />
      {value && (
        <p className="text-sm" data-testid="target-game-selected">
          Scelto: <span className="font-medium">{value.title}</span> ({value.yearPublished})
        </p>
      )}
      {enabled && (
        <ul className="max-h-56 space-y-1 overflow-y-auto" aria-label="Risultati">
          {games.length === 0 && !isFetching && (
            <li className="text-sm text-muted-foreground">
              Nessun gioco pubblicato con questo titolo.
            </li>
          )}
          {games.map(game => (
            <TargetGameOption
              key={game.id}
              game={game}
              selected={value?.id === game.id}
              onToggle={onChange}
            />
          ))}
        </ul>
      )}
    </div>
  );
}
