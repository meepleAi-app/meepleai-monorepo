import { FileText, Upload } from 'lucide-react';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import type { PdfDocumentDto } from '@/lib/api';

/**
 * Step 2: Document Selection
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

interface Step2DocumentSelectionProps {
  gameId: string;
  documents?: PdfDocumentDto[];
  selectedDocumentIds: string[];
  onSelectionChange: (documentIds: string[]) => void;
  isLoading?: boolean;
}

export function Step2DocumentSelection({
  documents = [],
  selectedDocumentIds,
  onSelectionChange,
  isLoading,
}: Step2DocumentSelectionProps) {
  const handleToggle = (documentId: string) => {
    const newSelection = selectedDocumentIds.includes(documentId)
      ? selectedDocumentIds.filter((id) => id !== documentId)
      : [...selectedDocumentIds, documentId];

    onSelectionChange(newSelection);
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">Loading documents...</p>
        <div className="space-y-2">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-16 animate-pulse rounded-lg bg-muted" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        Select documents to include with your share request (optional). These could be rulebooks,
        player aids, or reference sheets you've uploaded.
      </p>

      {documents.length === 0 ? (
        <Alert>
          <FileText className="h-4 w-4" />
          <AlertDescription>
            <p className="font-medium">No documents attached to this game yet.</p>
            <p className="mt-2 text-sm">
              You can upload documents from your library before creating a share request, or submit
              without documents and add them later.
            </p>
          </AlertDescription>
        </Alert>
      ) : (
        <div className="space-y-2">
          {documents.map((doc) => (
            <div
              key={doc.id}
              className="flex items-start gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50"
            >
              <Checkbox
                id={`doc-${doc.id}`}
                checked={selectedDocumentIds.includes(doc.id)}
                onCheckedChange={() => handleToggle(doc.id)}
                className="mt-1"
              />
              <label
                htmlFor={`doc-${doc.id}`}
                className="flex-1 cursor-pointer space-y-1"
              >
                <div className="flex items-center gap-2">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <p className="font-medium">{doc.fileName}</p>
                </div>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(doc.fileSizeBytes)}
                  {doc.pageCount && ` • ${doc.pageCount} pages`}
                  {doc.documentType && ` • ${doc.documentType}`}
                </p>
              </label>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <Upload className="h-3 w-3" />
        <span>
          Selected: {selectedDocumentIds.length} document{selectedDocumentIds.length !== 1 ? 's' : ''}
        </span>
      </div>
    </div>
  );
}
