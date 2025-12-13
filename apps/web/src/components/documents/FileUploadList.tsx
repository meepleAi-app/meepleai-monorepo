/**
 * File Upload List Component
 *
 * Multi-file upload interface with:
 * - Accept multiple PDFs (max 5 files, 50MB each)
 * - Document type selection per file
 * - Drag & drop reordering for sort_order
 * - File size validation and status display
 *
 * Issue #2051: Multi-document upload frontend components
 */

'use client';

import * as React from 'react';
import { X, FileText, GripVertical, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';
import { Card } from '@/components/ui/data-display/card';
import { DocumentTypeSelector, type DocumentType } from './DocumentTypeSelector';
import { cn } from '@/lib/utils';

export interface FileUploadItem {
  id: string;
  file: File;
  documentType: DocumentType;
  sortOrder: number;
  error?: string;
}

export interface FileUploadListProps {
  files: FileUploadItem[];
  onChange: (files: FileUploadItem[]) => void;
  maxFiles?: number;
  maxSizeMB?: number;
  className?: string;
}

const MAX_FILE_SIZE_MB = 50;
const MAX_FILES = 5;

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function validateFile(file: File, maxSizeMB: number): string | undefined {
  const maxBytes = maxSizeMB * 1024 * 1024;

  if (!file.type.includes('pdf')) {
    return 'File must be a PDF';
  }

  if (file.size > maxBytes) {
    return `File size exceeds ${maxSizeMB}MB limit`;
  }

  return undefined;
}

export function FileUploadList({
  files,
  onChange,
  maxFiles = MAX_FILES,
  maxSizeMB = MAX_FILE_SIZE_MB,
  className,
}: FileUploadListProps) {
  const [draggedIndex, setDraggedIndex] = React.useState<number | null>(null);

  const handleFileAdd = React.useCallback(
    (newFiles: FileList | File[]) => {
      const fileArray = Array.from(newFiles);

      if (files.length + fileArray.length > maxFiles) {
        alert(`Maximum ${maxFiles} files allowed`);
        return;
      }

      const newItems: FileUploadItem[] = fileArray.map((file, index) => ({
        id: `${Date.now()}-${index}`,
        file,
        documentType: 'base' as DocumentType,
        sortOrder: files.length + index,
        error: validateFile(file, maxSizeMB),
      }));

      onChange([...files, ...newItems]);
    },
    [files, onChange, maxFiles, maxSizeMB]
  );

  const handleFileRemove = React.useCallback(
    (id: string) => {
      const updated = files.filter(item => item.id !== id);
      // Reorder sort indices
      const reordered = updated.map((item, index) => ({
        ...item,
        sortOrder: index,
      }));
      onChange(reordered);
    },
    [files, onChange]
  );

  const handleDocumentTypeChange = React.useCallback(
    (id: string, documentType: DocumentType) => {
      const updated = files.map(item => (item.id === id ? { ...item, documentType } : item));
      onChange(updated);
    },
    [files, onChange]
  );

  const handleDragStart = React.useCallback((index: number) => {
    setDraggedIndex(index);
  }, []);

  const handleDragOver = React.useCallback(
    (e: React.DragEvent, index: number) => {
      e.preventDefault();

      if (draggedIndex === null || draggedIndex === index) return;

      const reordered = [...files];
      const [draggedItem] = reordered.splice(draggedIndex, 1);
      reordered.splice(index, 0, draggedItem);

      // Update sort orders
      const updated = reordered.map((item, idx) => ({
        ...item,
        sortOrder: idx,
      }));

      onChange(updated);
      setDraggedIndex(index);
    },
    [draggedIndex, files, onChange]
  );

  const handleDragEnd = React.useCallback(() => {
    setDraggedIndex(null);
  }, []);

  const handleInputChange = React.useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files) {
        handleFileAdd(e.target.files);
      }
    },
    [handleFileAdd]
  );

  return (
    <div className={cn('space-y-4', className)}>
      {/* File Input */}
      <div className="flex items-center gap-2">
        <label htmlFor="file-upload" className="cursor-pointer">
          <Button
            type="button"
            variant="outline"
            disabled={files.length >= maxFiles}
            onClick={() => document.getElementById('file-upload')?.click()}
          >
            <FileText className="mr-2 h-4 w-4" />
            Add PDF Files
          </Button>
        </label>
        <input
          id="file-upload"
          type="file"
          accept="application/pdf"
          multiple
          className="hidden"
          onChange={handleInputChange}
          disabled={files.length >= maxFiles}
        />
        <span className="text-sm text-muted-foreground">
          {files.length}/{maxFiles} files (max {maxSizeMB}MB each)
        </span>
      </div>

      {/* File List */}
      {files.length === 0 ? (
        <div className="p-8 text-center border-2 border-dashed rounded-lg bg-muted/50">
          <FileText className="mx-auto h-12 w-12 text-muted-foreground mb-2" />
          <p className="text-sm text-muted-foreground">
            No files added yet. Click &quot;Add PDF Files&quot; to begin.
          </p>
        </div>
      ) : (
        <div className="space-y-2" role="list" aria-label="Uploaded files">
          {files.map((item, index) => (
            <Card
              key={item.id}
              draggable
              onDragStart={() => handleDragStart(index)}
              onDragOver={e => handleDragOver(e, index)}
              onDragEnd={handleDragEnd}
              className={cn(
                'p-4 transition-all cursor-move',
                draggedIndex === index && 'opacity-50',
                item.error && 'border-destructive'
              )}
              role="listitem"
            >
              <div className="flex items-start gap-3">
                {/* Drag Handle */}
                <div className="mt-1 cursor-grab active:cursor-grabbing">
                  <GripVertical className="h-5 w-5 text-muted-foreground" />
                </div>

                {/* File Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-2">
                    <FileText className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                    <span className="font-medium truncate" title={item.file.name}>
                      {item.file.name}
                    </span>
                    <span className="text-sm text-muted-foreground flex-shrink-0">
                      {formatFileSize(item.file.size)}
                    </span>
                  </div>

                  {/* Document Type Selector */}
                  <div className="flex items-center gap-2">
                    <label
                      htmlFor={`type-${item.id}`}
                      className="text-sm font-medium flex-shrink-0"
                    >
                      Type:
                    </label>
                    <DocumentTypeSelector
                      value={item.documentType}
                      onChange={type => handleDocumentTypeChange(item.id, type)}
                      disabled={!!item.error}
                      className="w-64"
                    />
                  </div>

                  {/* Error Message */}
                  {item.error && (
                    <div className="flex items-center gap-2 mt-2 text-sm text-destructive">
                      <AlertCircle className="h-4 w-4" />
                      <span>{item.error}</span>
                    </div>
                  )}
                </div>

                {/* Remove Button */}
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => handleFileRemove(item.id)}
                  aria-label={`Remove ${item.file.name}`}
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
