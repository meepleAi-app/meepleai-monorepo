'use client';

import { BookOpen, MessageCircle } from 'lucide-react';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import { isRulebook } from '@/lib/api/schemas/game-documents.schemas';

import { KbDocumentRow } from './KbDocumentRow';

interface KbBottomSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameTitle: string;
  documents: GameDocument[];
  onStartChat: () => void;
  isLoading?: boolean;
}

export function KbBottomSheet({
  open,
  onOpenChange,
  gameTitle,
  documents,
  onStartChat,
  isLoading = false,
}: KbBottomSheetProps) {
  const rulebooks = documents.filter(isRulebook);
  const otherDocs = documents.filter(d => !isRulebook(d));
  const hasIndexedDocs = documents.some(d => d.status === 'indexed');

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="bottom" className="max-h-[80vh] overflow-y-auto rounded-t-2xl">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2 text-base">
            <BookOpen className="h-5 w-5 text-[hsl(174,60%,40%)]" />
            Knowledge Base — {gameTitle}
          </SheetTitle>
        </SheetHeader>

        <div className="space-y-4 pb-4">
          {rulebooks.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold text-muted-foreground">Regolamenti</h3>
              <div className="space-y-2">
                {rulebooks.map(doc => (
                  <KbDocumentRow key={doc.id} document={doc} />
                ))}
              </div>
            </section>
          )}

          {otherDocs.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold text-muted-foreground">
                Altre Knowledge Base
              </h3>
              <div className="space-y-2">
                {otherDocs.map(doc => (
                  <KbDocumentRow key={doc.id} document={doc} />
                ))}
              </div>
            </section>
          )}
        </div>

        <div className="sticky bottom-0 border-t bg-background pt-3 pb-4">
          <Button
            className="w-full"
            size="lg"
            disabled={!hasIndexedDocs || isLoading}
            onClick={onStartChat}
          >
            <MessageCircle className="mr-2 h-5 w-5" />
            Chatta con l&apos;Agente
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
