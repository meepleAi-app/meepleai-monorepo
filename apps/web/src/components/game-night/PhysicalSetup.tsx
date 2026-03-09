/**
 * PhysicalSetup — Step 2 of Setup Wizard
 *
 * Issue #5583: Setup Wizard — guided game preparation
 *
 * Features:
 * - Setup checklist derived from RulebookAnalysis.GamePhases (phase "Setup")
 * - Initial resources per player (from RulebookAnalysis.Resources)
 * - Toggle active expansions (from EntityLinks ExpansionOf)
 * - Graceful fallback for games without analysis
 */

'use client';

import { useState, useMemo } from 'react';

import {
  AlertTriangle,
  Box,
  CheckSquare,
  Package,
  Square,
  ToggleLeft,
  ToggleRight,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import type { EntityLinkDto, GamePhaseDto, ResourceDto } from '@/lib/api';
import { cn } from '@/lib/utils';

interface PhysicalSetupProps {
  gamePhases: GamePhaseDto[];
  resources: ResourceDto[];
  expansions: EntityLinkDto[];
  activeExpansionIds: string[];
  onActiveExpansionsChange: (ids: string[]) => void;
  /** When true, shows a warning that no analysis is available */
  hasNoAnalysis?: boolean;
}

export function PhysicalSetup({
  gamePhases,
  resources,
  expansions,
  activeExpansionIds,
  onActiveExpansionsChange,
  hasNoAnalysis = false,
}: PhysicalSetupProps) {
  // Track checked setup items locally
  const [checkedItems, setCheckedItems] = useState<Set<string>>(new Set());

  // Extract setup-related phases
  const setupPhases = useMemo(
    () =>
      gamePhases
        .filter(p => p.name.toLowerCase().includes('setup') || p.order === 0)
        .sort((a, b) => a.order - b.order),
    [gamePhases]
  );

  // If no explicit setup phases, show all phases as checklist
  const checklistItems = useMemo(() => {
    if (setupPhases.length > 0) return setupPhases;
    // Fallback: use all phases as a general checklist
    return gamePhases.sort((a, b) => a.order - b.order);
  }, [setupPhases, gamePhases]);

  const toggleChecked = (phaseName: string) => {
    setCheckedItems(prev => {
      const next = new Set(prev);
      if (next.has(phaseName)) {
        next.delete(phaseName);
      } else {
        next.add(phaseName);
      }
      return next;
    });
  };

  const toggleExpansion = (linkId: string) => {
    if (activeExpansionIds.includes(linkId)) {
      onActiveExpansionsChange(activeExpansionIds.filter(id => id !== linkId));
    } else {
      onActiveExpansionsChange([...activeExpansionIds, linkId]);
    }
  };

  if (hasNoAnalysis) {
    return (
      <div className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold font-quicksand">Preparazione Fisica</h2>
          <p className="text-sm text-muted-foreground">Prepara il tavolo e i componenti di gioco</p>
        </div>

        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 dark:bg-amber-950/20 dark:border-amber-800">
          <div className="flex items-start gap-3">
            <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
            <div>
              <p className="font-medium text-sm text-amber-800 dark:text-amber-200">
                Nessuna analisi disponibile
              </p>
              <p className="text-sm text-amber-700 dark:text-amber-300 mt-1">
                Questo gioco non ha un PDF analizzato. La checklist di preparazione e le risorse
                iniziali non sono disponibili. Puoi procedere direttamente al riepilogo regole.
              </p>
            </div>
          </div>
        </div>

        {/* Still show expansions if available */}
        {expansions.length > 0 && (
          <ExpansionToggles
            expansions={expansions}
            activeExpansionIds={activeExpansionIds}
            onToggle={toggleExpansion}
          />
        )}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Preparazione Fisica</h2>
        <p className="text-sm text-muted-foreground">
          Checklist di setup e risorse iniziali dal regolamento
        </p>
      </div>

      {/* Setup checklist */}
      {checklistItems.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-medium flex items-center gap-2">
            <CheckSquare className="h-4 w-4 text-indigo-500" />
            Checklist Preparazione
          </h3>
          <div className="space-y-1" role="list" aria-label="Checklist preparazione">
            {checklistItems.map(phase => {
              const isChecked = checkedItems.has(phase.name);
              return (
                <button
                  key={phase.name}
                  type="button"
                  role="listitem"
                  onClick={() => toggleChecked(phase.name)}
                  className={cn(
                    'flex items-start gap-3 w-full text-left rounded-lg border p-3 transition-colors',
                    isChecked
                      ? 'border-green-200 bg-green-50 dark:bg-green-950/20 dark:border-green-800'
                      : 'border-border bg-card hover:bg-muted/50'
                  )}
                  aria-label={`${phase.name}: ${isChecked ? 'completato' : 'da fare'}`}
                >
                  {isChecked ? (
                    <CheckSquare className="h-5 w-5 text-green-600 shrink-0 mt-0.5" />
                  ) : (
                    <Square className="h-5 w-5 text-muted-foreground shrink-0 mt-0.5" />
                  )}
                  <div className="flex-1 min-w-0">
                    <p
                      className={cn(
                        'text-sm font-medium',
                        isChecked && 'line-through text-muted-foreground'
                      )}
                    >
                      {phase.name}
                    </p>
                    {phase.description && (
                      <p className="text-xs text-muted-foreground mt-0.5">{phase.description}</p>
                    )}
                    {phase.isOptional && (
                      <Badge variant="outline" className="text-xs mt-1">
                        Opzionale
                      </Badge>
                    )}
                  </div>
                </button>
              );
            })}
          </div>
          <p className="text-xs text-muted-foreground">
            {checkedItems.size}/{checklistItems.length} completati
          </p>
        </div>
      )}

      {/* Resources per player */}
      {resources.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-medium flex items-center gap-2">
            <Box className="h-4 w-4 text-indigo-500" />
            Risorse Iniziali per Giocatore
          </h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            {resources.map(resource => (
              <div
                key={resource.name}
                className="flex items-center gap-3 rounded-lg border border-border bg-card p-3"
              >
                <Package className="h-4 w-4 text-muted-foreground shrink-0" />
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium truncate">{resource.name}</p>
                  <p className="text-xs text-muted-foreground">{resource.usage}</p>
                </div>
                <div className="flex items-center gap-1">
                  <Badge variant="outline" className="text-xs">
                    {resource.type}
                  </Badge>
                  {resource.isLimited && (
                    <Badge variant="destructive" className="text-xs">
                      Limitata
                    </Badge>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Expansion toggles */}
      {expansions.length > 0 && (
        <ExpansionToggles
          expansions={expansions}
          activeExpansionIds={activeExpansionIds}
          onToggle={toggleExpansion}
        />
      )}

      {checklistItems.length === 0 && resources.length === 0 && expansions.length === 0 && (
        <p className="text-sm text-muted-foreground text-center py-4">
          Nessun dato di preparazione disponibile per questo gioco.
        </p>
      )}
    </div>
  );
}

// ========== Sub-components ==========

function ExpansionToggles({
  expansions,
  activeExpansionIds,
  onToggle,
}: {
  expansions: EntityLinkDto[];
  activeExpansionIds: string[];
  onToggle: (linkId: string) => void;
}) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-medium flex items-center gap-2">
        <Package className="h-4 w-4 text-indigo-500" />
        Espansioni
      </h3>
      <div className="space-y-1" role="list" aria-label="Espansioni disponibili">
        {expansions.map(link => {
          const isActive = activeExpansionIds.includes(link.id);
          return (
            <button
              key={link.id}
              type="button"
              role="listitem"
              onClick={() => onToggle(link.id)}
              className={cn(
                'flex items-center gap-3 w-full text-left rounded-lg border p-3 transition-colors',
                isActive
                  ? 'border-indigo-200 bg-indigo-50 dark:bg-indigo-950/20 dark:border-indigo-800'
                  : 'border-border bg-card hover:bg-muted/50'
              )}
              aria-label={`Espansione ${link.targetEntityId}: ${isActive ? 'attiva' : 'disattiva'}`}
            >
              {isActive ? (
                <ToggleRight className="h-5 w-5 text-indigo-600 shrink-0" />
              ) : (
                <ToggleLeft className="h-5 w-5 text-muted-foreground shrink-0" />
              )}
              <span className="text-sm font-medium flex-1">
                {link.metadata ? link.metadata : `Espansione ${link.targetEntityId.slice(0, 8)}`}
              </span>
              <Badge variant={isActive ? 'default' : 'outline'} className="text-xs">
                {isActive ? 'Attiva' : 'Disattiva'}
              </Badge>
            </button>
          );
        })}
      </div>
    </div>
  );
}
