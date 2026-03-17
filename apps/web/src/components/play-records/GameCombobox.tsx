/**
 * GameCombobox - Searchable game selector with autocomplete
 *
 * Features:
 * - Search across UserLibrary + SharedGames + PrivateGames
 * - Debounced search (300ms)
 * - Source badges (Private/Library/Catalog)
 * - Empty state with "Search on BGG" link
 * - Keyboard navigation
 *
 * Issue #4273: PlayRecord Wizard - Game Search Autocomplete
 */

'use client';

import { useState } from 'react';

import { Check, ChevronsUpDown, Loader2, Search } from 'lucide-react';

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/navigation/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';
import { Button } from '@/components/ui/primitives/button';
import { useGameSearch } from '@/lib/domain-hooks/useGameSearch';
import { cn } from '@/lib/utils';

export interface GameComboboxProps {
  value?: string;
  onSelect: (gameId: string, gameName: string) => void;
  onNotFound?: () => void;
  disabled?: boolean;
  placeholder?: string;
}

export function GameCombobox({
  value,
  onSelect,
  onNotFound,
  disabled = false,
  placeholder = 'Search your games...',
}: GameComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');

  const { data: games = [], isLoading } = useGameSearch(search);

  const selectedGame = games.find(g => g.id === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-full justify-between"
          disabled={disabled}
        >
          {selectedGame ? (
            <span className="flex items-center gap-2">
              <span className="truncate">{selectedGame.name}</span>
              {selectedGame.source && <GameSourceBadge source={selectedGame.source} />}
            </span>
          ) : (
            <span className="text-muted-foreground">{placeholder}</span>
          )}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-full p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput placeholder="Type to search..." value={search} onValueChange={setSearch} />
          <CommandList>
            {isLoading ? (
              <div className="flex items-center justify-center py-6">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                <span className="ml-2 text-sm text-muted-foreground">Searching...</span>
              </div>
            ) : games.length === 0 ? (
              <CommandEmpty>
                <div className="py-6 text-center">
                  <p className="text-sm text-muted-foreground mb-2">No games found</p>
                  {onNotFound && search && (
                    <Button
                      variant="link"
                      size="sm"
                      onClick={() => {
                        setOpen(false);
                        onNotFound();
                      }}
                      className="text-primary"
                    >
                      <Search className="mr-1 h-3 w-3" />
                      Search on BGG
                    </Button>
                  )}
                </div>
              </CommandEmpty>
            ) : (
              <CommandGroup>
                {games.map(game => (
                  <CommandItem
                    key={game.id}
                    value={game.id}
                    onSelect={() => {
                      onSelect(game.id, game.name);
                      setOpen(false);
                      setSearch('');
                    }}
                  >
                    <Check
                      className={cn(
                        'mr-2 h-4 w-4',
                        value === game.id ? 'opacity-100' : 'opacity-0'
                      )}
                    />
                    <div className="flex items-center justify-between flex-1">
                      <span className="truncate">{game.name}</span>
                      {game.source && <GameSourceBadge source={game.source} />}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

interface GameSourceBadgeProps {
  source: 'library' | 'catalog' | 'private';
}

function GameSourceBadge({ source }: GameSourceBadgeProps) {
  const config = {
    library: { icon: '📚', label: 'Library', className: 'bg-blue-100 text-blue-700' },
    catalog: { icon: '🌐', label: 'Catalog', className: 'bg-green-100 text-green-700' },
    private: { icon: '🔒', label: 'Private', className: 'bg-purple-100 text-purple-700' },
  }[source];

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium',
        config.className
      )}
    >
      <span>{config.icon}</span>
      <span>{config.label}</span>
    </span>
  );
}
