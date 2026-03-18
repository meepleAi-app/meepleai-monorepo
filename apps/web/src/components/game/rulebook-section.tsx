'use client';

import { useRef } from 'react';

import {
  FileText,
  Upload,
  Loader2,
  CheckCircle,
  MessageSquare,
  RefreshCw,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { useRulebookUpload } from '@/lib/hooks/use-rulebook-upload';

interface RulebookInfo {
  pdfDocumentId: string;
  fileName: string;
  kbStatus: 'ready' | 'processing' | 'failed';
  indexedAt: string | null;
}

interface RulebookSectionProps {
  gameId: string;
  rulebooks: RulebookInfo[];
  onChatClick?: () => void;
  onRetry?: (pdfDocumentId: string) => void;
  onRemove?: (pdfDocumentId: string) => void;
}

export function RulebookSection({
  gameId,
  rulebooks,
  onChatClick,
  onRetry,
  onRemove,
}: RulebookSectionProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { mutateAsync: uploadRulebook, isPending: isUploading } = useRulebookUpload(gameId);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      const result = await uploadRulebook(file);
      if (!result.isNew) {
        toast.info('Questo regolamento è già nel sistema — collegato al tuo gioco!');
      } else {
        toast.success(result.message);
      }
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Upload fallito');
    }

    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const hasReady = rulebooks.some(r => r.kbStatus === 'ready');
  const title = rulebooks.length > 1 ? 'Regolamenti' : 'Regolamento';

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <FileText className="h-4 w-4 text-muted-foreground" />
          <h3 className="text-sm font-medium">{title}</h3>
        </div>
        {hasReady && (
          <span className="flex items-center gap-1 text-xs text-green-600">
            <CheckCircle className="h-3 w-3" /> Pronto
          </span>
        )}
      </div>

      {rulebooks.length === 0 ? (
        <>
          <input
            ref={fileInputRef}
            type="file"
            accept=".pdf"
            className="hidden"
            onChange={handleFileSelect}
          />
          <Button
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            disabled={isUploading}
          >
            {isUploading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Carica regolamento
          </Button>
        </>
      ) : (
        <div className="space-y-2">
          {rulebooks.map(r => (
            <div key={r.pdfDocumentId} className="flex items-center justify-between text-sm">
              <span className="truncate">{r.fileName}</span>
              <div className="flex items-center gap-2">
                {r.kbStatus === 'ready' && <span className="text-xs text-green-600">Pronto</span>}
                {r.kbStatus === 'processing' && (
                  <span className="flex items-center gap-1 text-xs text-amber-600">
                    <Loader2 className="h-3 w-3 animate-spin" /> In elaborazione...
                  </span>
                )}
                {r.kbStatus === 'failed' && (
                  <div className="flex items-center gap-1">
                    <span className="text-xs text-red-600">Elaborazione fallita</span>
                    {onRetry && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        onClick={() => onRetry(r.pdfDocumentId)}
                      >
                        <RefreshCw className="h-3 w-3" />
                      </Button>
                    )}
                    {onRemove && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        onClick={() => onRemove(r.pdfDocumentId)}
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    )}
                  </div>
                )}
              </div>
            </div>
          ))}

          <div className="flex gap-2 pt-1">
            {hasReady && onChatClick && (
              <Button variant="default" size="sm" onClick={onChatClick}>
                <MessageSquare className="mr-2 h-4 w-4" /> Chatta con l&apos;agente
              </Button>
            )}
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf"
              className="hidden"
              onChange={handleFileSelect}
            />
            <Button
              variant="outline"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
              disabled={isUploading}
            >
              {isUploading ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Upload className="mr-2 h-4 w-4" />
              )}
              Carica altro
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
