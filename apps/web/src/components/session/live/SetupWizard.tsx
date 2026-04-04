/**
 * SetupWizard
 *
 * Setup Wizard — Task 8
 *
 * Container component with two tabs: "Componenti" and "Setup".
 * Generates and persists setup checklist via API.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Loader2 } from 'lucide-react';

import { api } from '@/lib/api';

import { ComponentChecklist, type SetupComponent } from './ComponentChecklist';
import { SetupStepGuide, type SetupStep } from './SetupStepGuide';

// ─── Types ────────────────────────────────────────────────────────────────────

interface SetupWizardProps {
  sessionId: string;
  playerCount: number;
}

interface SetupChecklistData {
  components: SetupComponent[];
  steps: SetupStep[];
}

type TabId = 'componenti' | 'setup';

// ─── Component ────────────────────────────────────────────────────────────────

export function SetupWizard({ sessionId, playerCount }: SetupWizardProps) {
  const [checklist, setChecklist] = useState<SetupChecklistData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabId>('componenti');

  // Generate checklist on mount
  useEffect(() => {
    let cancelled = false;

    async function generate() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await api.liveSessions.generateSetupChecklist(sessionId, playerCount);

        if (cancelled) return;

        if (!result || (!result.components?.length && !result.steps?.length)) {
          setError('empty');
          return;
        }

        setChecklist(result);
      } catch {
        if (!cancelled) {
          setError('error');
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    generate();
    return () => {
      cancelled = true;
    };
  }, [sessionId, playerCount]);

  // Persist updates
  const persistChecklist = useCallback(
    async (updated: SetupChecklistData) => {
      try {
        await api.liveSessions.updateSetupChecklist(sessionId, updated);
      } catch {
        // Silently fail — local state is still updated
      }
    },
    [sessionId]
  );

  function handleToggleComponent(index: number) {
    if (!checklist) return;

    const updated: SetupChecklistData = {
      ...checklist,
      components: checklist.components.map((c, i) =>
        i === index ? { ...c, checked: !c.checked } : c
      ),
    };
    setChecklist(updated);
    persistChecklist(updated);
  }

  function handleCompleteStep(index: number) {
    if (!checklist) return;

    const updated: SetupChecklistData = {
      ...checklist,
      steps: checklist.steps.map((s, i) => (i === index ? { ...s, completed: true } : s)),
    };
    setChecklist(updated);
    persistChecklist(updated);
  }

  // ─── Loading ──────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-10 gap-3">
        <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
        <p className="text-sm text-gray-500 font-nunito">Generazione checklist di setup...</p>
      </div>
    );
  }

  // ─── Error / Empty ────────────────────────────────────────────────────────

  if (error || !checklist) {
    return (
      <div className="rounded-xl bg-white/70 backdrop-blur-md border border-white/40 p-6 text-center">
        <p className="text-sm text-gray-500 font-nunito">
          Setup non disponibile — usa la chat per chiedere all&apos;agente
        </p>
      </div>
    );
  }

  // ─── Tabs ─────────────────────────────────────────────────────────────────

  const tabs: { id: TabId; label: string }[] = [
    { id: 'componenti', label: 'Componenti' },
    { id: 'setup', label: 'Setup' },
  ];

  return (
    <div className="space-y-4">
      {/* Tab bar */}
      <div className="flex rounded-lg bg-gray-100 p-1">
        {tabs.map(tab => (
          <button
            key={tab.id}
            type="button"
            className={[
              'flex-1 rounded-md px-3 py-1.5 text-sm font-quicksand font-semibold transition-colors',
              activeTab === tab.id
                ? 'bg-white text-gray-900 shadow-sm'
                : 'text-gray-500 hover:text-gray-700',
            ].join(' ')}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'componenti' && (
        <ComponentChecklist components={checklist.components} onToggle={handleToggleComponent} />
      )}

      {activeTab === 'setup' && (
        <SetupStepGuide steps={checklist.steps} onComplete={handleCompleteStep} />
      )}
    </div>
  );
}
