'use client';

import { useState, useCallback } from 'react';

import { Gamepad2, FileText, Box, Upload, X } from 'lucide-react';

import { useSource } from '@/components/admin/sandbox/contexts/SourceContext';
import type { SharedGameSummary } from '@/components/admin/sandbox/contexts/SourceContext';
import { DocumentList } from '@/components/admin/sandbox/DocumentList';
import { GameSearchCombobox } from '@/components/admin/sandbox/GameSearchCombobox';
import { Button } from '@/components/ui/button';

export function SourcePanel() {
  const { selectedGame, setSelectedGame, documents } = useSource();

  // Local search state (will be wired to API later)
  const [searchResults, setSearchResults] = useState<SharedGameSummary[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  const handleSearch = useCallback((_term: string) => {
    // Placeholder: will be replaced with actual API call
    setIsSearching(true);
    setTimeout(() => {
      setSearchResults([]);
      setIsSearching(false);
    }, 300);
  }, []);

  const handleSelectGame = useCallback(
    (game: SharedGameSummary) => {
      setSelectedGame(game);
      setSearchResults([]);
    },
    [setSelectedGame]
  );

  const handleClearGame = useCallback(() => {
    setSelectedGame(null);
  }, [setSelectedGame]);

  const handleReindex = useCallback((_id: string) => {
    // Will be wired to API later
  }, []);

  const handleDelete = useCallback((_id: string) => {
    // Will be wired to API later
  }, []);

  return (
    <div className="flex h-full flex-col gap-4 rounded-xl border bg-white/70 backdrop-blur-md p-4">
      {/* Panel Header */}
      <div className="flex items-center gap-2">
        <Gamepad2 className="h-5 w-5 text-amber-600" />
        <h2 className="font-quicksand font-semibold text-lg">Source</h2>
      </div>

      {/* Game Search */}
      {!selectedGame && (
        <GameSearchCombobox
          games={searchResults}
          isLoading={isSearching}
          onSearch={handleSearch}
          onSelect={handleSelectGame}
        />
      )}

      {/* Selected Game Card */}
      {selectedGame && (
        <div className="flex items-start gap-3 rounded-lg border bg-amber-50/50 p-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-amber-100">
            <Gamepad2 className="h-5 w-5 text-amber-700" />
          </div>
          <div className="flex flex-col min-w-0 flex-1">
            <span className="font-quicksand font-semibold text-sm truncate">
              {selectedGame.title}
            </span>
            {selectedGame.publisher && (
              <span className="font-nunito text-xs text-muted-foreground truncate">
                {selectedGame.publisher}
              </span>
            )}
            <div className="mt-1.5 flex items-center gap-3 text-xs text-muted-foreground font-nunito">
              <span className="flex items-center gap-1">
                <FileText className="h-3 w-3" />
                {selectedGame.pdfCount} PDF
              </span>
              <span className="flex items-center gap-1">
                <Box className="h-3 w-3" />
                {selectedGame.chunkCount} chunk
              </span>
              <span className="flex items-center gap-1">
                <Box className="h-3 w-3" />
                {selectedGame.vectorCount} vettori
              </span>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 shrink-0"
            onClick={handleClearGame}
            aria-label="Deseleziona gioco"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      )}

      {/* Document List */}
      {selectedGame && (
        <div className="flex flex-col gap-3 flex-1 min-h-0 overflow-y-auto">
          <h3 className="font-quicksand font-semibold text-sm text-muted-foreground">Documenti</h3>
          <DocumentList documents={documents} onReindex={handleReindex} onDelete={handleDelete} />
        </div>
      )}

      {/* Upload Zone */}
      {selectedGame && (
        <div className="flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed border-muted-foreground/25 bg-white/30 py-6 transition-colors hover:border-amber-400/50 hover:bg-amber-50/30 cursor-pointer">
          <Upload className="h-6 w-6 text-muted-foreground/50" />
          <p className="font-nunito text-sm text-muted-foreground">
            Trascina un PDF o clicca per caricare
          </p>
        </div>
      )}
    </div>
  );
}
