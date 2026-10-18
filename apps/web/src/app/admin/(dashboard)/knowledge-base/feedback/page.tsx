'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { KbFeedbackPanel } from '@/components/admin/knowledge-base/kb-feedback-panel';
import { KbFeedbackVersionSummary } from '@/components/admin/knowledge-base/kb-feedback-version-summary';
import { api } from '@/lib/api';

/**
 * KB-08, #4469 (ADR-095 fetta 7a): i 👍👎 degli utenti per versione del profilo, senza digitare
 * l'UUID del gioco. In alto il riepilogo per versione, sotto la lista; un clic su una versione la
 * filtra.
 */
export default function KbFeedbackPage() {
  const [version, setVersion] = useState<string | null>(null);

  const { data: summary, isLoading } = useQuery({
    queryKey: ['admin-kb-feedback-summary'],
    queryFn: () => api.knowledgeBase.getAdminKbFeedbackSummary(),
  });

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-xl font-semibold text-foreground">Feedback KB Utenti</h2>
        <p className="text-sm text-muted-foreground mt-1">
          I feedback utile / non utile degli utenti sulle risposte, per versione del profilo
          dell&apos;agente
        </p>
      </div>

      {isLoading && <div className="animate-pulse h-24 rounded-lg bg-muted" />}

      {summary && (
        <>
          <KbFeedbackVersionSummary
            versions={summary.versions}
            selected={version}
            onSelect={setVersion}
          />
          <KbFeedbackPanel
            versions={summary.versions}
            games={summary.games}
            version={version}
            onVersionChange={setVersion}
          />
        </>
      )}
    </div>
  );
}
