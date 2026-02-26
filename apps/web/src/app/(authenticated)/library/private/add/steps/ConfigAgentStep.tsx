'use client';

/**
 * Step 3: Config Agent (User Wizard)
 * Issue #4: User Wizard - Agent Configuration
 *
 * Uses TypologySelector + StrategySelector from Issue #3
 */

import { useMemo, useState } from 'react';

import { TypologySelector, StrategySelector } from '@/components/agent/config';
import { toast } from '@/components/layout';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

interface ConfigAgentStepProps {
  gameId: string;
  gameName: string;
  pdfId: string;
  onComplete: () => void;
  onSkip: () => void;
  onBack: () => void;
}

export function ConfigAgentStep({
  gameId,
  gameName,
  pdfId: _pdfId,
  onComplete,
  onSkip,
  onBack,
}: ConfigAgentStepProps) {
  const [typologyId, setTypologyId] = useState<string>();
  const [strategyName, setStrategyName] = useState<string>('Balanced');
  const [isCreating, setIsCreating] = useState(false);

  const { data: currentUser } = useCurrentUser();

  // Map auth role to StrategySelector tier (Issue #9: replaces hardcoded 'Free')
  const userTier = useMemo((): 'Free' | 'Basic' | 'Pro' | 'Enterprise' => {
    const role = currentUser?.role?.toLowerCase() ?? 'user';
    if (role === 'superadmin' || role === 'admin' || role === 'editor') return 'Enterprise';
    return 'Free';
  }, [currentUser?.role]);

  const handleCreate = async () => {
    if (!typologyId) {
      toast.error('Seleziona un tipo di agente');
      return;
    }

    setIsCreating(true);
    try {
      // Issue #5: Create agent via API
      const response = await fetch(`/api/v1/library/games/${gameId}/agent`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          typologyId,
          strategyName,
          strategyParameters: null,
        }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Failed' }));
        throw new Error(error.message || 'Creazione agente fallita');
      }

      toast.success('Agente creato con successo!');
      onComplete();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Errore: ${message}`);
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Configura Agente RAG
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Scegli il tipo di agente e la strategia RAG per "{gameName}".
        </p>
      </div>

      {/* Agent Type Selection */}
      <TypologySelector
        value={typologyId}
        onChange={setTypologyId}
      />

      {/* Strategy Selection */}
      <StrategySelector
        value={strategyName}
        onChange={setStrategyName}
        userTier={userTier}
      />

      {/* Info Card */}
      <Card className="p-4 bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800">
        <p className="text-sm text-blue-800 dark:text-blue-200">
          💡 L'agente verrà creato utilizzando il PDF caricato come knowledge base.
          Potrai chattare con l'agente per ricevere risposte sulle regole del gioco.
        </p>
      </Card>

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={onBack} disabled={isCreating}>
          ← Indietro
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" onClick={onSkip} disabled={isCreating}>
            Salta Agente
          </Button>
          <Button onClick={handleCreate} disabled={!typologyId || isCreating} className="min-w-32">
            {isCreating ? (
              <>
                <Spinner size="sm" className="mr-2" />
                Creazione...
              </>
            ) : (
              'Crea Agente ✓'
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}
