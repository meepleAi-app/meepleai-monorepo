/**
 * Multi-Document Collection Upload Component
 *
 * Issue #2051: Multi-document upload with collection creation
 *
 * Workflow:
 * 1. User adds multiple PDFs (max 5 files, 50MB each)
 * 2. User assigns document type per file
 * 3. User enters collection name/description
 * 4. Upload all PDFs to backend
 * 5. Create collection with uploaded PDF IDs
 * 6. Show success/error states
 */

'use client';

import * as React from 'react';
import { AlertCircle, CheckCircle2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';
import { Card } from '@/components/ui/data-display/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { FileUploadList, type FileUploadItem } from './FileUploadList';
import { api, ApiError } from '@/lib/api';
import type { DocumentMetadata } from '@/lib/api/schemas/documents.schemas';
import { LoadingButton } from '@/components/loading';

export interface MultiDocumentCollectionUploadProps {
  gameId: string;
  gameName: string;
  onSuccess?: (collectionId: string) => void;
  onCancel?: () => void;
  className?: string;
}

interface UploadState {
  status: 'idle' | 'uploading' | 'creating_collection' | 'success' | 'error';
  progress: number;
  uploadedCount: number;
  totalCount: number;
  error?: string;
  collectionId?: string;
}

export function MultiDocumentCollectionUpload({
  gameId,
  gameName,
  onSuccess,
  onCancel,
  className,
}: MultiDocumentCollectionUploadProps) {
  const [files, setFiles] = React.useState<FileUploadItem[]>([]);
  const [collectionName, setCollectionName] = React.useState('');
  const [collectionDescription, setCollectionDescription] = React.useState('');
  const [uploadState, setUploadState] = React.useState<UploadState>({
    status: 'idle',
    progress: 0,
    uploadedCount: 0,
    totalCount: 0,
  });

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const canSubmit =
    files.length > 0 &&
    files.every(f => !f.error) &&
    collectionName.trim().length > 0 &&
    uploadState.status === 'idle';

  /**
   * Upload a single PDF file
   */
  const uploadSinglePdf = async (file: File): Promise<string> => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('gameId', gameId);
    formData.append('language', 'en'); // Default to English

    const response = await fetch(`${API_BASE}/api/v1/pdfs/upload`, {
      method: 'POST',
      credentials: 'include',
      body: formData,
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || `Upload failed: ${response.statusText}`);
    }

    const data = await response.json();
    return data.documentId;
  };

  /**
   * Handle form submission - upload PDFs and create collection
   */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!canSubmit) return;

    try {
      setUploadState({
        status: 'uploading',
        progress: 0,
        uploadedCount: 0,
        totalCount: files.length,
      });

      // Upload all PDFs concurrently
      const uploadPromises = files.map(async (fileItem, index) => {
        try {
          const pdfId = await uploadSinglePdf(fileItem.file);

          setUploadState(prev => ({
            ...prev,
            uploadedCount: prev.uploadedCount + 1,
            progress: Math.round(((prev.uploadedCount + 1) / prev.totalCount) * 100),
          }));

          return {
            pdfDocumentId: pdfId,
            documentType: fileItem.documentType,
            sortOrder: fileItem.sortOrder,
          } as DocumentMetadata;
        } catch (error) {
          throw new Error(
            `Failed to upload ${fileItem.file.name}: ${error instanceof Error ? error.message : 'Unknown error'}`
          );
        }
      });

      const uploadedDocuments = await Promise.all(uploadPromises);

      // Create collection with uploaded documents
      setUploadState(prev => ({
        ...prev,
        status: 'creating_collection',
        progress: 100,
      }));

      const collection = await api.documents.createCollection(gameId, {
        name: collectionName.trim(),
        description: collectionDescription.trim() || undefined,
        initialDocuments: uploadedDocuments,
      });

      setUploadState({
        status: 'success',
        progress: 100,
        uploadedCount: files.length,
        totalCount: files.length,
        collectionId: collection.id,
      });

      // Notify parent
      onSuccess?.(collection.id);
    } catch (error) {
      const errorMessage =
        error instanceof ApiError
          ? error.message
          : error instanceof Error
            ? error.message
            : 'Failed to create collection';

      setUploadState(prev => ({
        ...prev,
        status: 'error',
        error: errorMessage,
      }));
    }
  };

  /**
   * Reset form to initial state
   */
  const handleReset = () => {
    setFiles([]);
    setCollectionName('');
    setCollectionDescription('');
    setUploadState({
      status: 'idle',
      progress: 0,
      uploadedCount: 0,
      totalCount: 0,
    });
  };

  return (
    <div className={className}>
      <Card className="p-6">
        <h2 className="text-2xl font-semibold mb-4">Create Document Collection</h2>
        <p className="text-sm text-muted-foreground mb-6">
          Upload multiple PDF documents and organize them into a collection for {gameName}.
        </p>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Collection Name */}
          <div className="space-y-2">
            <Label htmlFor="collection-name">Collection Name *</Label>
            <Input
              id="collection-name"
              value={collectionName}
              onChange={e => setCollectionName(e.target.value)}
              placeholder="e.g., Catan Base Rules"
              maxLength={200}
              disabled={uploadState.status !== 'idle'}
              required
            />
          </div>

          {/* Collection Description */}
          <div className="space-y-2">
            <Label htmlFor="collection-description">Description (Optional)</Label>
            <Textarea
              id="collection-description"
              value={collectionDescription}
              onChange={e => setCollectionDescription(e.target.value)}
              placeholder="Add a description for this collection..."
              maxLength={1000}
              rows={3}
              disabled={uploadState.status !== 'idle'}
            />
          </div>

          {/* File Upload List */}
          <div className="space-y-2">
            <Label>Documents *</Label>
            <FileUploadList files={files} onChange={setFiles} maxFiles={5} maxSizeMB={50} />
          </div>

          {/* Upload Progress */}
          {uploadState.status === 'uploading' && (
            <Alert>
              <Upload className="h-4 w-4" />
              <AlertTitle>Uploading PDFs...</AlertTitle>
              <AlertDescription>
                {uploadState.uploadedCount} of {uploadState.totalCount} files uploaded (
                {uploadState.progress}%)
              </AlertDescription>
            </Alert>
          )}

          {/* Collection Creation Progress */}
          {uploadState.status === 'creating_collection' && (
            <Alert>
              <Upload className="h-4 w-4" />
              <AlertTitle>Creating Collection...</AlertTitle>
              <AlertDescription>
                All files uploaded successfully. Creating collection...
              </AlertDescription>
            </Alert>
          )}

          {/* Success State */}
          {uploadState.status === 'success' && (
            <Alert className="border-green-600 bg-green-50">
              <CheckCircle2 className="h-4 w-4 text-green-600" />
              <AlertTitle className="text-green-600">Collection Created Successfully!</AlertTitle>
              <AlertDescription className="text-green-600">
                {uploadState.uploadedCount} documents uploaded and organized into collection &quot;
                {collectionName}&quot;
              </AlertDescription>
            </Alert>
          )}

          {/* Error State */}
          {uploadState.status === 'error' && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Upload Failed</AlertTitle>
              <AlertDescription>{uploadState.error}</AlertDescription>
            </Alert>
          )}

          {/* Action Buttons */}
          <div className="flex gap-3">
            {uploadState.status === 'idle' && (
              <>
                <LoadingButton type="submit" disabled={!canSubmit} isLoading={false}>
                  <Upload className="mr-2 h-4 w-4" />
                  Create Collection
                </LoadingButton>
                {onCancel && (
                  <Button type="button" variant="secondary" onClick={onCancel}>
                    Cancel
                  </Button>
                )}
              </>
            )}

            {uploadState.status === 'uploading' && (
              <Button type="button" disabled>
                <Upload className="mr-2 h-4 w-4 animate-pulse" />
                Uploading... ({uploadState.uploadedCount}/{uploadState.totalCount})
              </Button>
            )}

            {uploadState.status === 'creating_collection' && (
              <Button type="button" disabled>
                <Upload className="mr-2 h-4 w-4 animate-pulse" />
                Creating Collection...
              </Button>
            )}

            {uploadState.status === 'success' && (
              <>
                <Button type="button" onClick={handleReset} variant="outline">
                  Create Another Collection
                </Button>
                {onCancel && (
                  <Button type="button" onClick={onCancel}>
                    Done
                  </Button>
                )}
              </>
            )}

            {uploadState.status === 'error' && (
              <>
                <Button type="button" onClick={handleReset} variant="outline">
                  Try Again
                </Button>
                {onCancel && (
                  <Button type="button" variant="secondary" onClick={onCancel}>
                    Cancel
                  </Button>
                )}
              </>
            )}
          </div>
        </form>
      </Card>
    </div>
  );
}
