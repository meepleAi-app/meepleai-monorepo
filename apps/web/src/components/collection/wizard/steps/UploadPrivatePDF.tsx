'use client';

/**
 * Step 3: Upload Private PDF (Optional)
 * Issue #3477: Upload private rulebook PDF for personal reference
 *
 * Reuses pattern from admin/wizard/steps/PdfUploadStep.tsx
 */

import { useState, useCallback } from 'react';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Button } from '@/components/ui/primitives/button';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';

export function UploadPrivatePDF() {
  const { setUploadedPdf, goNext, goBack } = useAddGameWizard();

  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragActive, setDragActive] = useState(false);

  const handleFileSelect = useCallback((selectedFile: File) => {
    if (selectedFile.type !== 'application/pdf') {
      toast.error('Please select a PDF file');
      return;
    }
    if (selectedFile.size > 50 * 1024 * 1024) {
      // 50MB limit
      toast.error('File must be less than 50MB');
      return;
    }
    setFile(selectedFile);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragActive(false);
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile) {
        handleFileSelect(droppedFile);
      }
    },
    [handleFileSelect]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
  }, []);

  const handleUpload = useCallback(async () => {
    if (!file) {
      toast.error('Please select a PDF file');
      return;
    }

    setUploading(true);
    setUploadProgress(0);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('language', 'it'); // Default to Italian

      // Use XHR for progress tracking (same pattern as admin wizard)
      const xhr = new XMLHttpRequest();

      const uploadPromise = new Promise<{ documentId: string; fileName: string }>(
        (resolve, reject) => {
          xhr.upload.addEventListener('progress', e => {
            if (e.lengthComputable) {
              setUploadProgress(Math.round((e.loaded / e.total) * 100));
            }
          });

          xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
              try {
                const response = JSON.parse(xhr.responseText);
                resolve(response);
              } catch {
                reject(new Error('Invalid response from server'));
              }
            } else {
              try {
                const error = JSON.parse(xhr.responseText);
                reject(new Error(error.error || 'Upload failed'));
              } catch {
                reject(new Error(`Upload failed: ${xhr.statusText}`));
              }
            }
          });

          xhr.addEventListener('error', () => {
            reject(new Error('Network error during upload'));
          });

          // Upload to ingest endpoint (private PDF, not public library)
          xhr.open('POST', '/api/v1/ingest/pdf');
          xhr.withCredentials = true;
          xhr.send(formData);
        }
      );

      const result = await uploadPromise;

      toast.success('PDF uploaded successfully!');
      setUploadedPdf(result.documentId, file.name);
      goNext(); // Automatically advance to review step
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      toast.error(`Upload failed: ${message}`);
    } finally {
      setUploading(false);
    }
  }, [file, setUploadedPdf, goNext]);

  const handleSkip = () => {
    goNext(); // Skip PDF upload and go to review
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Upload Private PDF
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Optionally upload a rulebook PDF for personal reference. This PDF will be private to your
          account.
        </p>
      </div>

      {/* Drop Zone */}
      <div
        className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer ${
          dragActive
            ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
            : file
              ? 'border-green-500 bg-green-50 dark:bg-green-900/20'
              : 'border-slate-300 dark:border-slate-600 hover:border-blue-400 hover:bg-slate-50 dark:hover:bg-slate-700/50'
        }`}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => document.getElementById('pdf-file-input')?.click()}
      >
        <input
          id="pdf-file-input"
          type="file"
          accept="application/pdf"
          className="hidden"
          onChange={e => {
            const selectedFile = e.target.files?.[0];
            if (selectedFile) handleFileSelect(selectedFile);
          }}
        />

        {file ? (
          <div className="space-y-2">
            <div className="text-4xl">📄</div>
            <p className="font-medium text-slate-900 dark:text-white">{file.name}</p>
            <p className="text-sm text-slate-500">{(file.size / 1024 / 1024).toFixed(2)} MB</p>
            <Button
              variant="outline"
              size="sm"
              onClick={e => {
                e.stopPropagation();
                setFile(null);
              }}
            >
              Change File
            </Button>
          </div>
        ) : (
          <div className="space-y-2">
            <div className="text-4xl">📁</div>
            <p className="font-medium text-slate-900 dark:text-white">
              Drag PDF here or click to select
            </p>
            <p className="text-sm text-slate-500">PDF up to 50MB</p>
          </div>
        )}
      </div>

      {/* Progress Bar */}
      {uploading && (
        <div className="space-y-2">
          <div className="flex justify-between text-sm">
            <span>Uploading...</span>
            <span>{uploadProgress}%</span>
          </div>
          <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-600 transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {/* Info Box */}
      <div className="p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
        <p className="text-sm text-blue-900 dark:text-blue-100">
          <strong>Privacy:</strong> Your uploaded PDF will be private and only visible to you. It
          won't be shared in the public library.
        </p>
      </div>

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={goBack} disabled={uploading}>
          ← Back
        </Button>

        <div className="flex gap-3">
          <Button variant="outline" onClick={handleSkip} disabled={uploading}>
            Skip (No PDF)
          </Button>

          <Button onClick={handleUpload} disabled={!file || uploading} className="min-w-32">
            {uploading ? (
              <>
                <Spinner size="sm" className="mr-2" />
                Uploading...
              </>
            ) : (
              'Upload PDF →'
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}
