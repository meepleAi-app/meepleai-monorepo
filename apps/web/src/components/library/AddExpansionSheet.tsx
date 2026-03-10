'use client';

import { useEffect, useRef, useState } from 'react';

import { CheckCircle, Loader2, Search, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';

const DEBOUNCE_MS = 400;

export interface AddExpansionSheetProps {
  open: boolean;
  onClose: () => void;
  onExpansionAdded?: () => void;
  baseGameId: string;
  baseGameTitle: string;
}

type Status = 'idle' | 'searching' | 'selected' | 'adding' | 'success' | 'error';

export function AddExpansionSheet({
  open,
  onClose,
  onExpansionAdded,
  baseGameId,
  baseGameTitle,
}: AddExpansionSheetProps) {
  const [searchTerm, setSearchTerm] = useState(baseGameTitle);
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [status, setStatus] = useState<Status>('idle');
  const [selectedResult, setSelectedResult] = useState<BggSearchResult | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Reset state when sheet opens
  useEffect(() => {
    if (open) {
      setSearchTerm(baseGameTitle);
      setResults([]);
      setStatus('idle');
      setSelectedResult(null);
      setErrorMessage(null);
    }
  }, [open, baseGameTitle]);

  // Debounced BGG search
  useEffect(() => {
    if (!open) return;

    if (debounceRef.current) clearTimeout(debounceRef.current);

    const term = searchTerm.trim();
    if (!term) {
      setResults([]);
      setStatus('idle');
      return;
    }

    debounceRef.current = setTimeout(async () => {
      setStatus('searching');
      setErrorMessage(null);
      try {
        const response = await api.bgg.search(term);
        setResults(response.results);
        setStatus('idle');
      } catch {
        setResults([]);
        setStatus('error');
        setErrorMessage('Errore nella ricerca BGG. Riprova.');
      }
    }, DEBOUNCE_MS);

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchTerm, open]);

  const handleSelectResult = (result: BggSearchResult) => {
    setSelectedResult(result);
    setStatus('selected');
  };

  const handleAdd = async () => {
    if (!selectedResult) return;

    setStatus('adding');
    setErrorMessage(null);

    try {
      // Step 1: Create private game from BGG
      const privateGame = await api.library.addPrivateGame({
        source: 'BoardGameGeek',
        bggId: selectedResult.bggId,
        title: selectedResult.name,
        minPlayers: 1,
        maxPlayers: 4,
        yearPublished: selectedResult.yearPublished ?? undefined,
      });

      // Step 2: Create entity link: expansion → base game
      await api.library.createEntityLink({
        sourceEntityType: 'Game',
        sourceEntityId: privateGame.id,
        targetEntityType: 'Game',
        targetEntityId: baseGameId,
        linkType: 'ExpansionOf',
      });

      setStatus('success');
      onExpansionAdded?.();
    } catch {
      setStatus('error');
      setErrorMessage("Errore durante l'aggiunta dell'espansione. Riprova.");
    }
  };

  const handleClose = () => {
    onClose();
  };

  return (
    <Sheet open={open} onOpenChange={o => !o && handleClose()}>
      <SheetContent side="right" className="w-full sm:max-w-[480px] flex flex-col p-0">
        <SheetHeader className="border-b border-slate-800 px-6 pt-6 pb-4">
          <div className="flex items-center justify-between">
            <SheetTitle className="text-lg font-semibold text-slate-100">
              Aggiungi espansione
            </SheetTitle>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-slate-400 hover:text-slate-200"
              onClick={handleClose}
            >
              <X className="h-4 w-4" />
              <span className="sr-only">Chiudi</span>
            </Button>
          </div>
          <p className="text-sm text-slate-400 mt-1">
            Aggiungi un&apos;espansione per:{' '}
            <span className="text-slate-200 font-medium">{baseGameTitle}</span>
          </p>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-6 py-6 space-y-4">
          {/* Success state */}
          {status === 'success' ? (
            <div
              className="flex flex-col items-center justify-center gap-3 py-12 text-center"
              data-testid="success-state"
            >
              <CheckCircle className="h-12 w-12 text-green-500" />
              <p className="text-lg font-semibold text-slate-100">Espansione aggiunta!</p>
              <p className="text-sm text-slate-400">
                L&apos;espansione è stata aggiunta alla tua libreria e collegata al gioco base.
              </p>
              <Button onClick={handleClose} className="mt-2">
                Chiudi
              </Button>
            </div>
          ) : (
            <>
              {/* Search input */}
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                <Input
                  placeholder="Cerca espansione su BGG..."
                  value={searchTerm}
                  onChange={e => {
                    setSearchTerm(e.target.value);
                    setSelectedResult(null);
                    if (status === 'selected' || status === 'error') setStatus('idle');
                  }}
                  className="pl-9"
                  data-testid="search-input"
                />
              </div>

              {/* Error message */}
              {errorMessage && (
                <p className="text-sm text-red-400" role="alert">
                  {errorMessage}
                </p>
              )}

              {/* Searching indicator */}
              {status === 'searching' && (
                <div className="flex items-center gap-2 text-sm text-slate-400">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Ricerca in corso...
                </div>
              )}

              {/* Results list */}
              {results.length > 0 && status !== 'searching' && (
                <div className="space-y-1" data-testid="search-results">
                  {results.map(result => (
                    <button
                      key={result.bggId}
                      type="button"
                      onClick={() => handleSelectResult(result)}
                      className={`w-full flex items-center gap-3 p-3 rounded-lg transition-colors text-left ${
                        selectedResult?.bggId === result.bggId
                          ? 'bg-amber-500/20 border border-amber-500/40'
                          : 'hover:bg-slate-800/60'
                      }`}
                      data-testid={`result-item-${result.bggId}`}
                    >
                      {/* Thumbnail */}
                      <div className="w-10 h-10 rounded bg-slate-800 overflow-hidden flex-shrink-0">
                        {result.thumbnailUrl ? (
                          // eslint-disable-next-line @next/next/no-img-element
                          <img
                            src={result.thumbnailUrl}
                            alt=""
                            className="w-full h-full object-cover"
                            loading="lazy"
                          />
                        ) : (
                          <div className="w-full h-full flex items-center justify-center text-lg">
                            🎲
                          </div>
                        )}
                      </div>

                      {/* Info */}
                      <div className="flex-1 min-w-0">
                        <p className="font-medium text-sm text-slate-200 truncate">{result.name}</p>
                        {result.yearPublished && (
                          <p className="text-xs text-slate-500">{result.yearPublished}</p>
                        )}
                      </div>
                    </button>
                  ))}
                </div>
              )}

              {/* No results */}
              {results.length === 0 && status === 'idle' && searchTerm.trim().length > 0 && (
                <p className="text-sm text-slate-500 text-center py-4">
                  Nessun risultato trovato. Prova con un termine diverso.
                </p>
              )}

              {/* Add button */}
              {selectedResult && (
                <div className="pt-2 border-t border-slate-800">
                  <div className="mb-3">
                    <p className="text-xs text-slate-400 mb-1">Espansione selezionata:</p>
                    <p className="text-sm font-medium text-slate-200">{selectedResult.name}</p>
                  </div>
                  <Button
                    onClick={handleAdd}
                    disabled={status === 'adding'}
                    className="w-full"
                    data-testid="add-button"
                  >
                    {status === 'adding' ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin mr-2" />
                        Aggiunta in corso...
                      </>
                    ) : (
                      'Aggiungi'
                    )}
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
