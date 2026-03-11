/**
 * Step 1: Upload — Drag & drop + file input for PDFs
 *
 * Accept only application/pdf, max 50MB per file.
 * Shows file list with remove button.
 * "Avanti" button disabled until at least 1 file.
 */

'use client';

import { useCallback, useRef, useState } from 'react';

import { FileUp, FileText, X } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

// ── Constants ───────────────────────────────────────────────────────────

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
const MAX_FILE_COUNT = 20;

// ── Props ───────────────────────────────────────────────────────────────

interface StepUploadProps {
  files: File[];
  onFilesChange: (files: File[]) => void;
  onNext: () => void;
}

// ── Helpers ─────────────────────────────────────────────────────────────

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// ── Component ───────────────────────────────────────────────────────────

export function StepUpload({ files, onFilesChange, onNext }: StepUploadProps) {
  const [dragActive, setDragActive] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const validateAndAddFiles = useCallback(
    (newFiles: FileList | File[]) => {
      const validFiles: File[] = [];
      const remaining = MAX_FILE_COUNT - files.length;

      if (remaining <= 0) {
        toast.error(`Massimo ${MAX_FILE_COUNT} file consentiti`);
        return;
      }

      for (const file of Array.from(newFiles)) {
        if (validFiles.length >= remaining) {
          toast.error(`Massimo ${MAX_FILE_COUNT} file consentiti`);
          break;
        }
        if (file.type !== 'application/pdf') {
          toast.error(`"${file.name}" non e un PDF valido`);
          continue;
        }
        if (file.size > MAX_FILE_SIZE) {
          toast.error(`"${file.name}" supera il limite di 50MB`);
          continue;
        }
        // Skip duplicates by name
        if (files.some(f => f.name === file.name && f.size === file.size)) {
          toast.info(`"${file.name}" gia aggiunto`);
          continue;
        }
        validFiles.push(file);
      }

      if (validFiles.length > 0) {
        onFilesChange([...files, ...validFiles]);
      }
    },
    [files, onFilesChange]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragActive(false);
      if (e.dataTransfer.files.length > 0) {
        validateAndAddFiles(e.dataTransfer.files);
      }
    },
    [validateAndAddFiles]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
  }, []);

  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files && e.target.files.length > 0) {
        validateAndAddFiles(e.target.files);
      }
      // Reset input so the same file can be selected again
      e.target.value = '';
    },
    [validateAndAddFiles]
  );

  const removeFile = useCallback(
    (index: number) => {
      onFilesChange(files.filter((_, i) => i !== index));
    },
    [files, onFilesChange]
  );

  return (
    <div className="space-y-4">
      {/* Drop zone */}
      <div
        className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer ${
          dragActive
            ? 'border-primary bg-primary/5'
            : 'border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/50'
        }`}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => inputRef.current?.click()}
      >
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf"
          multiple
          className="hidden"
          onChange={handleInputChange}
        />
        <FileUp className="h-12 w-12 mx-auto text-muted-foreground mb-3" />
        <p className="font-medium">Trascina i PDF qui o clicca per selezionare</p>
        <p className="text-sm text-muted-foreground mt-1">Solo file PDF, massimo 50MB ciascuno</p>
      </div>

      {/* File list */}
      {files.length > 0 && (
        <div className="space-y-2">
          <p className="text-sm font-medium text-muted-foreground">
            {files.length} file{files.length !== 1 ? '' : ''} selezionat
            {files.length !== 1 ? 'i' : 'o'}
          </p>
          {files.map((file, index) => (
            <div
              key={`${file.name}-${file.size}`}
              className="flex items-center justify-between rounded-lg border bg-white/60 dark:bg-zinc-800/60 px-4 py-3"
            >
              <div className="flex items-center gap-3 min-w-0">
                <FileText className="h-5 w-5 text-muted-foreground shrink-0" />
                <div className="min-w-0">
                  <p className="text-sm font-medium truncate">{file.name}</p>
                  <p className="text-xs text-muted-foreground">{formatFileSize(file.size)}</p>
                </div>
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="shrink-0 h-8 w-8"
                onClick={e => {
                  e.stopPropagation();
                  removeFile(index);
                }}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Navigation */}
      <div className="flex justify-end pt-2">
        <Button onClick={onNext} disabled={files.length === 0}>
          Avanti
        </Button>
      </div>
    </div>
  );
}
