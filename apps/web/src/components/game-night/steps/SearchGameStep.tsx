'use client';

/**
 * SearchGameStep — Step 1 of GameNightWizard.
 *
 * Issue #2089: Refactored to use the unified <GamePicker> shared component.
 *   - Replaces Enter-only handleSearch (was: B3.1) with debounced search via GamePicker.
 *   - Replaces silent catch{} (was: B3.2) with toast.error feedback via GamePicker.
 *   - Source: 'both' (library + catalog) — was previously catalog-only (B3.3) which
 *     missed games already in the user's library.
 *
 * Game Night MVP hardening F1 — PDF-aware soft filter:
 *   each result shows a GameKbBadge (AI pronto / Solo manuale) and, when a
 *   non-indexed game is selected, a GameKbWarning is surfaced below the picker.
 *
 * Issue #123 — Game Night Quick Start Wizard.
 */

import { useCallback, useState, type JSX } from 'react';

import { GamePicker, type GameOption } from '@/components/features/game-picker';
import { Button } from '@/components/ui/primitives/button';
import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';

import { GameKbBadge, GameKbWarning } from '../GameKbBadge';

// ============================================================================
// Types
// ============================================================================

interface SearchGameStepProps {
  onGameFound: (data: { gameId?: string; privateGameId?: string; gameTitle: string }) => void;
}

// ============================================================================
// KB Badge per result (Rules of Hooks: live in own component)
// ============================================================================

function ResultKbBadge({ gameId }: { gameId: string }): JSX.Element | null {
  const kb = useGameKbStatus(gameId);
  return <GameKbBadge isIndexed={kb.isIndexed} isLoading={kb.isLoading} />;
}

// ============================================================================
// Component
// ============================================================================

export function SearchGameStep({ onGameFound }: SearchGameStepProps): JSX.Element {
  const [selected, setSelected] = useState<GameOption | null>(null);

  const handleConfirm = useCallback(() => {
    if (!selected) return;
    onGameFound({
      gameId: selected.manual ? undefined : selected.id,
      gameTitle: selected.title,
    });
  }, [onGameFound, selected]);

  const renderResultExtra = useCallback(
    (option: GameOption) => (option.manual ? null : <ResultKbBadge gameId={option.id} />),
    []
  );

  return (
    <div className="space-y-4" data-testid="search-game-step">
      <div>
        <h3 className="font-quicksand font-bold text-lg text-foreground">Trova il gioco</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Cerca nel catalogo MeepleAI o nella tua libreria.
        </p>
      </div>

      <GamePicker
        source="both"
        value={selected}
        onChange={setSelected}
        placeholder="Nome del gioco..."
        testId="game-search"
        renderResultExtra={renderResultExtra}
      />

      {/* KB warning for the selected game (surfaces "Solo manuale" advisory). */}
      {selected && !selected.manual && <GameKbWarning gameId={selected.id} />}

      {selected && (
        <div className="flex justify-end">
          <Button type="button" onClick={handleConfirm} data-testid="confirm-game-button">
            Continua con {selected.title}
          </Button>
        </div>
      )}
    </div>
  );
}
