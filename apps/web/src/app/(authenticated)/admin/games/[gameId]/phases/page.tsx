/**
 * Admin — Game Phase Templates Configuration
 *
 * /admin/games/[gameId]/phases
 *
 * Game Session Flow v2.0 — Task 13
 *
 * Allows admins to configure the ordered phase templates for a game's turn.
 * Supports AI suggestion (requires game to have an uploaded PDF/rulebook).
 */

'use client';

import { use, useState, useCallback, useTransition } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { PhaseTemplateEditor } from '@/components/admin/PhaseTemplateEditor';
import type {
  PhaseTemplateItem,
  PhaseTemplateSuggestion,
} from '@/components/admin/PhaseTemplateEditor';
import { toast } from '@/components/layout';
import { api } from '@/lib/api';

import { savePhaseTemplatesAction, suggestPhaseTemplatesAction } from './actions';

interface PhasesPageProps {
  params: Promise<{ gameId: string }>;
}

export default function GamePhasesPage({ params }: PhasesPageProps) {
  const { gameId } = use(params);
  const router = useRouter();

  const [isSavePending, startSaveTransition] = useTransition();
  const [isSuggestPending, startSuggestTransition] = useTransition();
  const [suggestions, setSuggestions] = useState<PhaseTemplateSuggestion[] | undefined>();

  // Fetch game metadata (for name + hasPdf)
  const { data: game } = useQuery({
    queryKey: ['admin-game', gameId],
    queryFn: () => api.games.getById(gameId),
  });

  // Fetch existing phase templates
  const { data: existingTemplates = [], isLoading } = useQuery({
    queryKey: ['admin-phase-templates', gameId],
    queryFn: () => api.games.getPhaseTemplates(gameId),
  });

  const initialItems: PhaseTemplateItem[] = existingTemplates.map(t => ({
    id: t.id,
    phaseName: t.phaseName,
    phaseOrder: t.phaseOrder,
    description: t.description ?? '',
  }));

  // Show AI suggest button whenever the game has PDFs indexed (backend handles the check)
  const hasPdf = true;

  const handleSave = useCallback(
    (templates: PhaseTemplateItem[]) => {
      startSaveTransition(async () => {
        const phases = templates.map(t => ({
          phaseName: t.phaseName,
          phaseOrder: t.phaseOrder,
          description: t.description || undefined,
        }));
        const result = await savePhaseTemplatesAction(gameId, phases);
        if (result.success) {
          toast.success(`${phases.length} fasi configurate e salvate.`);
        } else {
          toast.error(result.error ?? 'Errore durante il salvataggio.');
        }
      });
    },
    [gameId]
  );

  const handleSuggest = useCallback(() => {
    startSuggestTransition(async () => {
      const result = await suggestPhaseTemplatesAction(gameId);
      if (result.success && result.suggestions) {
        setSuggestions(result.suggestions);
      } else {
        toast.error(result.error ?? 'Suggerimenti non disponibili. Carica prima un manuale PDF.');
      }
    });
  }, [gameId]);

  const handleAcceptSuggestions = useCallback(() => {
    setSuggestions(undefined);
  }, []);

  const handleRejectSuggestions = useCallback(() => {
    setSuggestions(undefined);
  }, []);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <p className="text-sm text-muted-foreground">Caricamento fasi...</p>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto px-4 py-6 space-y-6">
      {/* Header */}
      <div className="space-y-1">
        <button
          onClick={() => router.back()}
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors mb-3"
        >
          <ArrowLeft className="h-4 w-4" />
          Indietro
        </button>
        <h1 className="text-2xl font-bold font-quicksand">Configurazione fasi turno</h1>
        {game && <p className="text-sm text-muted-foreground">{game.title}</p>}
      </div>

      {/* Description */}
      <p className="text-sm text-muted-foreground">
        Definisci le fasi di un turno tipo (es. Preparazione → Pesca carte → Azione → Pulizia).
        Durante la sessione live, il TurnStateHeader mostrerà la fase corrente e permetterà di
        avanzare.
      </p>

      {/* Editor */}
      <PhaseTemplateEditor
        gameId={gameId}
        initialTemplates={initialItems}
        hasPdf={hasPdf}
        isSaving={isSavePending}
        isSuggesting={isSuggestPending}
        suggestions={suggestions}
        onSave={handleSave}
        onSuggest={handleSuggest}
        onAcceptSuggestions={handleAcceptSuggestions}
        onRejectSuggestions={handleRejectSuggestions}
      />

      {/* Link back to game management */}
      {game && (
        <div className="pt-4 border-t border-border/40">
          <p className="text-xs text-muted-foreground">
            Altre configurazioni:{' '}
            <Link
              href={`/admin/wizard?gameId=${gameId}`}
              className="underline underline-offset-2 hover:text-foreground"
            >
              Wizard gioco
            </Link>
          </p>
        </div>
      )}
    </div>
  );
}
