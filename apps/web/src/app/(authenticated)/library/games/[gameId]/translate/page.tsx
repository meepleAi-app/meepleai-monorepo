/**
 * TranslateDemoPage — DEMO-ONLY page (Path 5a / Nanolith demo).
 *
 * Route: /library/games/[gameId]/translate
 *
 * Tracked in: <issue URL once filed>
 * Demo session: Nanolith one-shot, NOT for production.
 *
 * Before the demo:
 *   1. Fill DEMO_AGENT_LOOKUP with the target game's UUID → agent UUID mapping.
 *   2. Navigate to /library/games/<gameId>/translate
 *   3. Enter paragraph number and optional chapter context, submit.
 *
 * Architecture:
 *   Thin client shell. Resolves agentId from DEMO_AGENT_LOOKUP keyed by gameId.
 *   Renders an informative error if the mapping is missing (guides operator to
 *   fill the lookup map before the demo).
 *
 * No server-side data fetching — the demo agent chat stream is fully client-side
 * via useTranslateParagraph → useAgentChatStream → SSE.
 */

'use client';

import { Suspense } from 'react';

import { useParams } from 'next/navigation';

import { TranslateParagraphDemo } from '@/components/v2/gamebook/TranslateParagraphDemo';

// ---------------------------------------------------------------------------
// DEMO-ONLY: populate this map before the demo session.
// Format: { '<gameId-uuid>': '<agentId-uuid>' }
// Leave empty by default — the page shows a setup guide when agentId is missing.
// ---------------------------------------------------------------------------
const DEMO_AGENT_LOOKUP: Record<string, string> = {
  // Example (remove before committing real UUIDs):
  // 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee': 'ffffffff-0000-1111-2222-333333333333',
};

// ---------------------------------------------------------------------------
// Inner component (needs useParams — must be client)
// ---------------------------------------------------------------------------

function TranslateDemoInner(): React.ReactElement {
  const params = useParams<{ gameId: string }>();
  const gameId = params?.gameId ?? '';
  const agentId = DEMO_AGENT_LOOKUP[gameId] ?? '';

  if (!gameId) {
    return (
      <div className="flex flex-col gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-6">
        <p className="font-semibold text-amber-800">gameId non trovato nei parametri URL.</p>
        <p className="text-sm text-amber-700">
          Controlla che la route sia{' '}
          <code className="font-mono">/library/games/{'<gameId>'}/translate</code>.
        </p>
      </div>
    );
  }

  if (!agentId) {
    return (
      <div className="flex flex-col gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-6">
        <p className="font-semibold text-amber-800">Agente non configurato per questa partita.</p>
        <p className="text-sm text-amber-700">
          Aggiungi la riga seguente a <code className="font-mono">DEMO_AGENT_LOOKUP</code> in{' '}
          <code className="font-mono">translate/page.tsx</code>:
        </p>
        <pre className="overflow-x-auto rounded-lg bg-amber-100 px-3 py-2 text-xs text-amber-900">
          {`'${gameId}': '<agentId-uuid>',`}
        </pre>
        <p className="text-xs text-amber-600">
          Trovi l&apos;agentId nella pagina di dettaglio agente (URL:{' '}
          <code className="font-mono">/agents/{'<agentId>'}</code>).
        </p>
      </div>
    );
  }

  return <TranslateParagraphDemo gameId={gameId} agentId={agentId} />;
}

// ---------------------------------------------------------------------------
// Page default export
// ---------------------------------------------------------------------------

export default function TranslateDemoPage(): React.ReactElement {
  return (
    <main className="mx-auto max-w-2xl px-4 py-8 sm:px-6">
      {/* DEMO badge */}
      <div className="mb-6 inline-flex items-center rounded-full border border-amber-300 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
        DEMO-ONLY — Path 5a workaround
      </div>

      <Suspense
        fallback={
          <div className="h-48 animate-pulse rounded-2xl border border-slate-200 bg-slate-100" />
        }
      >
        <TranslateDemoInner />
      </Suspense>
    </main>
  );
}
