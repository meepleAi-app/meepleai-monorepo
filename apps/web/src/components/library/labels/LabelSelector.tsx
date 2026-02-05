/**
 * LabelSelector Component (Issue #3514)
 *
 * Dropdown/popover for selecting and managing labels:
 * - Search/filter labels
 * - Predefined labels section
 * - User custom labels section
 * - "Create new" input for custom labels
 */

'use client';

import { useState, useMemo } from 'react';

import { Check, Loader2, Plus, Search, Tag } from 'lucide-react';
import { toast } from 'sonner';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLabels, useAddLabelToGame, useCreateCustomLabel } from '@/hooks/queries/useLabels';
import type { LabelDto } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

export interface LabelSelectorProps {
  gameId: string;
  currentLabels: LabelDto[];
  onLabelAdded?: (label: LabelDto) => void;
  disabled?: boolean;
  className?: string;
}

// Default color for new custom labels
const DEFAULT_CUSTOM_LABEL_COLOR = '#6b7280';

export function LabelSelector({
  gameId,
  currentLabels,
  onLabelAdded,
  disabled = false,
  className,
}: LabelSelectorProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [isOpen, setIsOpen] = useState(false);

  const { data: allLabels = [], isLoading } = useLabels();
  const addLabelMutation = useAddLabelToGame();
  const createLabelMutation = useCreateCustomLabel();

  // Current label IDs for quick lookup
  const currentLabelIds = useMemo(
    () => new Set(currentLabels.map((l) => l.id)),
    [currentLabels]
  );

  // Filter labels based on search term
  const filteredLabels = useMemo(() => {
    const term = searchTerm.toLowerCase().trim();
    if (!term) return allLabels;
    return allLabels.filter((label) => label.name.toLowerCase().includes(term));
  }, [allLabels, searchTerm]);

  // Separate predefined and custom labels
  const predefinedLabels = useMemo(
    () => filteredLabels.filter((l) => l.isPredefined),
    [filteredLabels]
  );
  const customLabels = useMemo(
    () => filteredLabels.filter((l) => !l.isPredefined),
    [filteredLabels]
  );

  // Check if we can create a new label with current search term
  const canCreateNew = useMemo(() => {
    const term = searchTerm.trim();
    if (!term || term.length < 2) return false;
    // Check if label with this name already exists
    return !allLabels.some((l) => l.name.toLowerCase() === term.toLowerCase());
  }, [searchTerm, allLabels]);

  const handleSelectLabel = async (label: LabelDto) => {
    if (currentLabelIds.has(label.id)) return; // Already added

    try {
      await addLabelMutation.mutateAsync({ gameId, label });
      onLabelAdded?.(label);
      toast.success('Etichetta aggiunta', {
        description: `"${label.name}" aggiunta al gioco`,
      });
    } catch (_error) {
      toast.error('Errore', {
        description: "Impossibile aggiungere l'etichetta",
      });
    }
  };

  const handleCreateLabel = async () => {
    const name = searchTerm.trim();
    if (!name || name.length < 2) return;

    setIsCreating(true);
    try {
      const newLabel = await createLabelMutation.mutateAsync({
        name,
        color: DEFAULT_CUSTOM_LABEL_COLOR,
      });

      // Add the newly created label to the game
      await addLabelMutation.mutateAsync({ gameId, label: newLabel });
      onLabelAdded?.(newLabel);

      setSearchTerm('');
      toast.success('Etichetta creata', {
        description: `"${name}" creata e aggiunta al gioco`,
      });
    } catch (_error) {
      toast.error('Errore', {
        description: "Impossibile creare l'etichetta",
      });
    } finally {
      setIsCreating(false);
    }
  };

  const isAdding = addLabelMutation.isPending || isCreating;

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || isAdding}
          className={cn(
            'gap-1.5 border-dashed border-[rgba(45,42,38,0.2)] font-nunito text-sm font-medium text-[#6B665C] hover:border-[rgba(45,42,38,0.4)] hover:text-[#2D2A26]',
            className
          )}
        >
          {isAdding ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Plus className="h-3.5 w-3.5" />
          )}
          Aggiungi
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64 p-2">
        {/* Search Input */}
        <div className="relative mb-2">
          <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Cerca etichetta..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="h-8 pl-8 text-sm"
            autoFocus
          />
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-4">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <>
            {/* Predefined Labels */}
            {predefinedLabels.length > 0 && (
              <>
                <div className="mb-1 px-2 py-1 font-nunito text-xs font-semibold text-muted-foreground">
                  Etichette predefinite
                </div>
                {predefinedLabels.map((label) => (
                  <LabelMenuItem
                    key={label.id}
                    label={label}
                    isSelected={currentLabelIds.has(label.id)}
                    onSelect={handleSelectLabel}
                    disabled={isAdding}
                  />
                ))}
              </>
            )}

            {/* Custom Labels */}
            {customLabels.length > 0 && (
              <>
                {predefinedLabels.length > 0 && <DropdownMenuSeparator className="my-2" />}
                <div className="mb-1 px-2 py-1 font-nunito text-xs font-semibold text-muted-foreground">
                  Le tue etichette
                </div>
                {customLabels.map((label) => (
                  <LabelMenuItem
                    key={label.id}
                    label={label}
                    isSelected={currentLabelIds.has(label.id)}
                    onSelect={handleSelectLabel}
                    disabled={isAdding}
                  />
                ))}
              </>
            )}

            {/* Create New Label */}
            {canCreateNew && (
              <>
                <DropdownMenuSeparator className="my-2" />
                <DropdownMenuItem
                  onClick={handleCreateLabel}
                  disabled={isCreating}
                  className="cursor-pointer gap-2"
                >
                  <Plus className="h-4 w-4 text-[hsl(25,95%,38%)]" />
                  <span>
                    Crea &quot;<span className="font-semibold">{searchTerm.trim()}</span>&quot;
                  </span>
                </DropdownMenuItem>
              </>
            )}

            {/* Empty State */}
            {filteredLabels.length === 0 && !canCreateNew && (
              <div className="flex flex-col items-center justify-center py-4 text-center">
                <Tag className="mb-2 h-8 w-8 text-muted-foreground/50" />
                <p className="font-nunito text-sm text-muted-foreground">
                  Nessuna etichetta trovata
                </p>
              </div>
            )}
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

interface LabelMenuItemProps {
  label: LabelDto;
  isSelected: boolean;
  onSelect: (label: LabelDto) => void;
  disabled?: boolean;
}

function LabelMenuItem({ label, isSelected, onSelect, disabled }: LabelMenuItemProps) {
  return (
    <DropdownMenuItem
      onClick={() => !isSelected && onSelect(label)}
      disabled={disabled || isSelected}
      className={cn(
        'cursor-pointer gap-2',
        isSelected && 'bg-muted/50 opacity-60'
      )}
    >
      <span
        className="h-3 w-3 rounded-full"
        style={{ backgroundColor: label.color }}
      />
      <span className="flex-1 font-nunito">{label.name}</span>
      {isSelected && <Check className="h-4 w-4 text-[hsl(25,95%,38%)]" />}
    </DropdownMenuItem>
  );
}
