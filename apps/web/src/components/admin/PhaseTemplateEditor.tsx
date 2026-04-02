/**
 * PhaseTemplateEditor — drag-and-drop editor for game phase templates
 *
 * Game Session Flow v2.0 — Task 13
 *
 * Features:
 * - Sortable phase list via @dnd-kit/sortable
 * - Add / remove phases
 * - AI suggest button (requires PDF) with accept/reject banner
 * - Save callback
 */

'use client';

import { useState, useCallback } from 'react';

import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, Plus, Trash2, Sparkles, Check, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface PhaseTemplateItem {
  id: string; // local-only unique id for dnd-kit
  phaseName: string;
  phaseOrder: number;
  description: string;
}

export interface PhaseTemplateSuggestion {
  phaseName: string;
  phaseOrder: number;
  description: string;
  rationale: string;
}

interface PhaseTemplateEditorProps {
  gameId: string;
  initialTemplates: PhaseTemplateItem[];
  hasPdf: boolean;
  isSaving?: boolean;
  isSuggesting?: boolean;
  suggestions?: PhaseTemplateSuggestion[];
  onSave: (templates: PhaseTemplateItem[]) => void;
  onSuggest: () => void;
  onAcceptSuggestions: (suggestions: PhaseTemplateSuggestion[]) => void;
  onRejectSuggestions: () => void;
}

// ─── SortablePhaseRow ─────────────────────────────────────────────────────────

interface SortablePhaseRowProps {
  phase: PhaseTemplateItem;
  onChangeName: (id: string, value: string) => void;
  onChangeDescription: (id: string, value: string) => void;
  onRemove: (id: string) => void;
}

function SortablePhaseRow({
  phase,
  onChangeName,
  onChangeDescription,
  onRemove,
}: SortablePhaseRowProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: phase.id,
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'flex gap-2 items-start p-3 rounded-xl border bg-card transition-shadow',
        isDragging ? 'shadow-lg border-primary/50 opacity-90 z-10 relative' : 'border-border/60'
      )}
    >
      {/* Drag handle */}
      <button
        {...attributes}
        {...listeners}
        className="mt-2 cursor-grab text-muted-foreground hover:text-foreground shrink-0"
        aria-label="Trascina per riordinare"
      >
        <GripVertical className="h-5 w-5" />
      </button>

      {/* Phase order badge */}
      <div className="mt-2 w-6 h-6 rounded-full bg-primary/10 text-primary flex items-center justify-center text-xs font-bold shrink-0">
        {phase.phaseOrder}
      </div>

      {/* Fields */}
      <div className="flex-1 space-y-2 min-w-0">
        <Input
          value={phase.phaseName}
          onChange={e => onChangeName(phase.id, e.target.value)}
          placeholder="Nome fase (es. Pesca carte)"
          maxLength={50}
          className="h-9 text-sm"
          aria-label={`Nome fase ${phase.phaseOrder}`}
        />
        <Textarea
          value={phase.description}
          onChange={e => onChangeDescription(phase.id, e.target.value)}
          placeholder="Descrizione breve (opzionale)"
          maxLength={200}
          rows={2}
          className="resize-none text-xs"
          aria-label={`Descrizione fase ${phase.phaseOrder}`}
        />
      </div>

      {/* Remove */}
      <button
        onClick={() => onRemove(phase.id)}
        className="mt-2 shrink-0 text-muted-foreground hover:text-destructive transition-colors"
        aria-label={`Rimuovi fase ${phase.phaseOrder}`}
      >
        <Trash2 className="h-4 w-4" />
      </button>
    </div>
  );
}

// ─── PhaseTemplateEditor ──────────────────────────────────────────────────────

export function PhaseTemplateEditor({
  initialTemplates,
  hasPdf,
  isSaving = false,
  isSuggesting = false,
  suggestions,
  onSave,
  onSuggest,
  onAcceptSuggestions,
  onRejectSuggestions,
}: PhaseTemplateEditorProps) {
  const [phases, setPhases] = useState<PhaseTemplateItem[]>(initialTemplates);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      setPhases(prev => {
        const oldIndex = prev.findIndex(p => p.id === active.id);
        const newIndex = prev.findIndex(p => p.id === over.id);
        const reordered = arrayMove(prev, oldIndex, newIndex);
        return reordered.map((p, i) => ({ ...p, phaseOrder: i + 1 }));
      });
    }
  }, []);

  const handleChangeName = useCallback((id: string, value: string) => {
    setPhases(prev => prev.map(p => (p.id === id ? { ...p, phaseName: value } : p)));
  }, []);

  const handleChangeDescription = useCallback((id: string, value: string) => {
    setPhases(prev => prev.map(p => (p.id === id ? { ...p, description: value } : p)));
  }, []);

  const handleRemove = useCallback((id: string) => {
    setPhases(prev => {
      const filtered = prev.filter(p => p.id !== id);
      return filtered.map((p, i) => ({ ...p, phaseOrder: i + 1 }));
    });
  }, []);

  const handleAddPhase = useCallback(() => {
    setPhases(prev => [
      ...prev,
      {
        id: `new-${Date.now()}`,
        phaseName: '',
        phaseOrder: prev.length + 1,
        description: '',
      },
    ]);
  }, []);

  const handleSave = useCallback(() => {
    onSave(phases);
  }, [phases, onSave]);

  const handleAccept = useCallback(() => {
    if (!suggestions) return;
    onAcceptSuggestions(suggestions);
    const accepted: PhaseTemplateItem[] = suggestions.map((s, i) => ({
      id: `ai-${i}-${Date.now()}`,
      phaseName: s.phaseName,
      phaseOrder: s.phaseOrder,
      description: s.description,
    }));
    setPhases(accepted);
  }, [suggestions, onAcceptSuggestions]);

  const hasUnsaved = JSON.stringify(phases) !== JSON.stringify(initialTemplates);

  return (
    <div className="space-y-4">
      {/* AI suggestions banner */}
      {suggestions && suggestions.length > 0 && (
        <div className="rounded-xl border border-purple-500/30 bg-purple-500/10 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <Sparkles className="h-4 w-4 text-purple-400" />
            <p className="text-sm font-medium text-purple-300">
              L&apos;AI ha suggerito {suggestions.length} fasi basate sul manuale
            </p>
          </div>
          <div className="space-y-1">
            {suggestions.map((s, i) => (
              <p key={i} className="text-xs text-muted-foreground">
                <span className="text-purple-400 font-medium">
                  {i + 1}. {s.phaseName}
                </span>
                {s.rationale && <span className="ml-1">— {s.rationale}</span>}
              </p>
            ))}
          </div>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={handleAccept}
              className="gap-1.5 text-purple-300 border-purple-500/40 hover:bg-purple-500/20"
            >
              <Check className="h-3.5 w-3.5" />
              Accetta
            </Button>
            <Button
              size="sm"
              variant="ghost"
              onClick={onRejectSuggestions}
              className="gap-1.5 text-muted-foreground"
            >
              <X className="h-3.5 w-3.5" />
              Ignora
            </Button>
          </div>
        </div>
      )}

      {/* Phase list */}
      {phases.length === 0 ? (
        <div className="rounded-xl border border-dashed border-border/60 p-8 text-center">
          <p className="text-sm text-muted-foreground">
            Nessuna fase configurata. Aggiungine una o usa il suggerimento AI.
          </p>
        </div>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={phases.map(p => p.id)} strategy={verticalListSortingStrategy}>
            <div className="space-y-2">
              {phases.map(phase => (
                <SortablePhaseRow
                  key={phase.id}
                  phase={phase}
                  onChangeName={handleChangeName}
                  onChangeDescription={handleChangeDescription}
                  onRemove={handleRemove}
                />
              ))}
            </div>
          </SortableContext>
        </DndContext>
      )}

      {/* Actions */}
      <div className="flex items-center gap-2 pt-2">
        <Button variant="outline" size="sm" onClick={handleAddPhase} className="gap-1.5">
          <Plus className="h-4 w-4" />
          Aggiungi fase
        </Button>

        {hasPdf && (
          <Button
            variant="outline"
            size="sm"
            onClick={onSuggest}
            disabled={isSuggesting}
            className="gap-1.5 text-purple-400 border-purple-500/40 hover:bg-purple-500/10"
          >
            <Sparkles className="h-4 w-4" />
            {isSuggesting ? 'Analisi in corso...' : 'Suggerisci con AI'}
          </Button>
        )}

        <div className="flex-1" />

        <Button
          onClick={handleSave}
          disabled={isSaving || !hasUnsaved || phases.some(p => !p.phaseName.trim())}
          size="sm"
        >
          {isSaving ? 'Salvataggio...' : 'Salva fasi'}
        </Button>
      </div>

      {hasUnsaved && <p className="text-xs text-amber-400">Hai modifiche non salvate</p>}
    </div>
  );
}
