'use client';

import { useState, useEffect, useCallback } from 'react';

import { Search, Loader2, Gamepad2 } from 'lucide-react';

import type { SharedGameSummary } from '@/components/admin/sandbox/contexts/SourceContext';
import {
  Command,
  CommandInput,
  CommandList,
  CommandItem,
  CommandEmpty,
  CommandGroup,
} from '@/components/ui/navigation/command';
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/overlays/popover';

interface GameSearchComboboxProps {
  games: SharedGameSummary[];
  isLoading: boolean;
  onSearch: (term: string) => void;
  onSelect: (game: SharedGameSummary) => void;
}

export function GameSearchCombobox({
  games,
  isLoading,
  onSearch,
  onSelect,
}: GameSearchComboboxProps) {
  const [open, setOpen] = useState(false);
  const [inputValue, setInputValue] = useState('');

  // Debounce search by 300ms
  useEffect(() => {
    if (!inputValue.trim()) return;

    const timer = setTimeout(() => {
      onSearch(inputValue.trim());
    }, 300);

    return () => clearTimeout(timer);
  }, [inputValue, onSearch]);

  const handleSelect = useCallback(
    (game: SharedGameSummary) => {
      onSelect(game);
      setOpen(false);
      setInputValue('');
    },
    [onSelect]
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          role="combobox"
          aria-expanded={open}
          className="flex w-full items-center gap-2 rounded-lg border bg-white/50 px-3 py-2 text-sm text-muted-foreground hover:bg-white/80 transition-colors"
        >
          <Search className="h-4 w-4 shrink-0" />
          <span className="font-nunito">Cerca un gioco...</span>
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput
            placeholder="Cerca per titolo..."
            value={inputValue}
            onValueChange={setInputValue}
          />
          <CommandList>
            {isLoading ? (
              <div className="flex items-center justify-center py-6">
                <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
              </div>
            ) : (
              <>
                <CommandEmpty>Nessun gioco trovato</CommandEmpty>
                <CommandGroup>
                  {games.map(game => (
                    <CommandItem
                      key={game.id}
                      value={game.id}
                      onSelect={() => handleSelect(game)}
                      className="flex items-center gap-3 cursor-pointer"
                    >
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-amber-100">
                        <Gamepad2 className="h-4 w-4 text-amber-700" />
                      </div>
                      <div className="flex flex-col min-w-0">
                        <span className="font-quicksand font-semibold text-sm truncate">
                          {game.title}
                        </span>
                        <span className="font-nunito text-xs text-muted-foreground truncate">
                          {game.publisher ?? 'Publisher sconosciuto'}
                          {' \u00b7 '}
                          {game.pdfCount} PDF
                        </span>
                      </div>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
