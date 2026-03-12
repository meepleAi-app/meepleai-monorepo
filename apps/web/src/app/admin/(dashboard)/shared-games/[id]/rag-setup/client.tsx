/**
 * RAG Setup Dashboard Client - Admin Dashboard
 *
 * 4-panel single-page dashboard layout:
 * ┌──────────────────────────────────────┐
 * │ Header: Back + Game title + Readiness│
 * ├──────────────┬───────────────────────┤
 * │ Left Column  │ Right Column          │
 * │ 1. PDF Upload│ 3. Agent Setup        │
 * │ 2. Doc List  │ 4. Chat               │
 * │   + Status   │                       │
 * └──────────────┴───────────────────────┘
 */

'use client';

import { use, useState } from 'react';

import { ArrowLeft, FileText } from 'lucide-react';
import Link from 'next/link';

import { PdfIndexingStatus } from '@/components/admin/shared-games/PdfIndexingStatus';
import { PdfUploadSection } from '@/components/admin/shared-games/PdfUploadSection';
import { AgentSetupPanel } from '@/components/admin/shared-games/rag-setup/AgentSetupPanel';
import { InlineChatPanel } from '@/components/admin/shared-games/rag-setup/InlineChatPanel';
import { RagReadinessIndicator } from '@/components/admin/shared-games/rag-setup/RagReadinessIndicator';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameRagReadiness } from '@/hooks/queries/useGameRagReadiness';

interface RagSetupClientProps {
  params: Promise<{ id: string }>;
}

export function RagSetupClient({ params }: RagSetupClientProps) {
  const { id: gameId } = use(params);
  const { data: readiness, isLoading } = useGameRagReadiness(gameId);
  const [agentInfo, setAgentInfo] = useState<{
    agentId: string;
    chatThreadId: string;
  } | null>(null);

  // Derive active agent/thread from either new creation or existing linked agent
  const activeAgentId =
    agentInfo?.agentId ?? readiness?.linkedAgent?.agentId ?? null;
  const activeChatThreadId = agentInfo?.chatThreadId ?? null;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-16 w-full rounded-xl" />
        <div className="grid gap-6 lg:grid-cols-2">
          <Skeleton className="h-96 w-full rounded-xl" />
          <Skeleton className="h-96 w-full rounded-xl" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Link href={`/admin/shared-games/${gameId}`}>
          <Button variant="ghost" size="icon">
            <ArrowLeft className="h-5 w-5" />
          </Button>
        </Link>
        <div className="flex-1">
          <h1 className="text-2xl font-bold">
            RAG Setup: {readiness?.gameTitle ?? 'Gioco'}
          </h1>
          <p className="text-sm text-muted-foreground">
            Configura documenti, agente e chat per questo gioco
          </p>
        </div>
      </div>

      {/* Readiness Indicator */}
      {readiness && <RagReadinessIndicator readiness={readiness} />}

      {/* Two-column layout */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Left Column: Documents */}
        <div className="space-y-6">
          {/* PDF Upload */}
          <PdfUploadSection gameId={gameId} />

          {/* Document List with Status */}
          {readiness && readiness.documents.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <FileText className="h-5 w-5" />
                  Documenti ({readiness.readyDocuments}/{readiness.totalDocuments}{' '}
                  pronti)
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {readiness.documents.map((doc) => (
                  <div
                    key={doc.documentId}
                    className="flex items-center justify-between rounded-lg border p-3"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {doc.fileName}
                      </p>
                    </div>
                    <PdfIndexingStatus
                      pdfId={doc.documentId}
                      fileName={doc.fileName}
                      compact
                    />
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </div>

        {/* Right Column: Agent + Chat */}
        <div className="space-y-6">
          {/* Agent Setup */}
          <AgentSetupPanel
            gameId={gameId}
            gameTitle={readiness?.gameTitle ?? ''}
            documents={readiness?.documents ?? []}
            existingAgent={readiness?.linkedAgent ?? null}
            onAgentCreated={(info) => setAgentInfo(info)}
          />

          {/* Inline Chat */}
          <InlineChatPanel
            agentId={activeAgentId}
            chatThreadId={activeChatThreadId}
          />
        </div>
      </div>
    </div>
  );
}
