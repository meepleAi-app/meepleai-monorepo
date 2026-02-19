/**
 * Knowledge Base Document Detail Page - /knowledge-base/[id]
 *
 * Shows document details with navigation footer linking to Game and Agent.
 * Uses MeepleCard entity=document with hero variant.
 *
 * @see Issue #4694
 */

'use client';

import { use, useEffect, useState } from 'react';

import { ArrowLeft, FileText, Calendar, HardDrive } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useEntityNavigation } from '@/hooks/useEntityNavigation';

interface DocumentDetail {
  id: string;
  fileName: string;
  gameId?: string;
  gameName?: string;
  agentId?: string;
  uploadedAt?: string;
  fileSize?: number;
  chunkCount?: number;
}

export default function KnowledgeBaseDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: documentId } = use(params);
  const [document, setDocument] = useState<DocumentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const navigationLinks = useEntityNavigation('document', {
    id: documentId,
    gameId: document?.gameId,
    agentId: document?.agentId,
  });

  useEffect(() => {
    // Attempt to load document metadata
    // The actual API may vary - this provides the page structure
    setLoading(false);
    setDocument({
      id: documentId,
      fileName: 'Documento',
    });
  }, [documentId]);

  if (loading) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Skeleton className="h-8 w-48 mb-6" />
          <Skeleton className="h-[400px] w-full max-w-2xl mx-auto" />
        </div>
      </div>
    );
  }

  if (error || !document) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Alert variant="destructive">
            <AlertDescription>{error || 'Documento non trovato'}</AlertDescription>
          </Alert>
          <Button asChild className="mt-4">
            <Link href="/library">
              <ArrowLeft className="mr-2 h-4 w-4" /> Torna alla Libreria
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href="/library">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna alla Libreria
          </Link>
        </Button>

        {/* Hero Card */}
        <section className="mb-8 flex justify-center">
          <MeepleCard
            entity="document"
            variant="hero"
            title={document.fileName}
            subtitle={document.gameName ? `Gioco: ${document.gameName}` : 'Knowledge Base'}
            metadata={[
              { icon: FileText, value: 'PDF' },
              ...(document.uploadedAt
                ? [{ icon: Calendar, value: new Date(document.uploadedAt).toLocaleDateString('it-IT') }]
                : []),
              ...(document.chunkCount
                ? [{ icon: HardDrive, value: `${document.chunkCount} chunks` }]
                : []),
            ]}
            navigateTo={navigationLinks}
          />
        </section>

        {/* Document Info */}
        <div className="max-w-4xl mx-auto">
          <Card className="border-l-4 border-l-[hsl(210,40%,55%)] shadow-lg">
            <CardHeader>
              <CardTitle className="font-quicksand text-xl">
                Dettagli Documento
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4 font-nunito text-sm">
                <div>
                  <span className="text-muted-foreground">ID</span>
                  <p className="font-mono text-xs mt-1">{document.id}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Nome File</span>
                  <p className="mt-1">{document.fileName}</p>
                </div>
                {document.gameName && (
                  <div>
                    <span className="text-muted-foreground">Gioco</span>
                    <p className="mt-1">{document.gameName}</p>
                  </div>
                )}
                {document.fileSize && (
                  <div>
                    <span className="text-muted-foreground">Dimensione</span>
                    <p className="mt-1">
                      {(document.fileSize / 1024).toFixed(1)} KB
                    </p>
                  </div>
                )}
              </div>

              <Alert>
                <AlertDescription className="font-nunito">
                  Il viewer PDF completo sarà disponibile in una versione futura.
                  Per ora puoi navigare alle entità correlate tramite i link sopra.
                </AlertDescription>
              </Alert>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
