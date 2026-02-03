'use client';

/**
 * Step 1: PDF Upload
 *
 * Upload a PDF rulebook and optionally mark it as public (visible to registered users).
 */

import { useState, useCallback } from 'react';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';

interface PdfUploadStepProps {
  onComplete: (pdfId: string, fileName: string, isPublic: boolean) => void;
}

export function PdfUploadStep({ onComplete }: PdfUploadStepProps) {
  const [file, setFile] = useState<File | null>(null);
  const [isPublic, setIsPublic] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragActive, setDragActive] = useState(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const handleFileSelect = useCallback((selectedFile: File) => {
    if (selectedFile.type !== 'application/pdf') {
      toast.error('Seleziona un file PDF');
      return;
    }
    if (selectedFile.size > 50 * 1024 * 1024) {
      // 50MB limit
      toast.error('Il file deve essere inferiore a 50MB');
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
      toast.error('Seleziona un file PDF');
      return;
    }

    setUploading(true);
    setUploadProgress(0);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('language', 'it'); // Default to Italian

      // Use XHR for progress tracking
      const xhr = new XMLHttpRequest();

      const uploadPromise = new Promise<{ id: string }>((resolve, reject) => {
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
              reject(new Error('Risposta non valida dal server'));
            }
          } else {
            try {
              const error = JSON.parse(xhr.responseText);
              reject(new Error(error.error || 'Upload fallito'));
            } catch {
              reject(new Error(`Upload fallito: ${xhr.statusText}`));
            }
          }
        });

        xhr.addEventListener('error', () => {
          reject(new Error('Errore di rete durante upload'));
        });

        // Note: We upload to a temporary game ID, then associate with the real game in step 2
        // For now, we use a placeholder approach - upload without gameId
        xhr.open('POST', `${API_BASE}/api/v1/ingest/upload`);
        xhr.withCredentials = true;
        xhr.send(formData);
      });

      const result = await uploadPromise;

      toast.success('PDF caricato con successo!');
      onComplete(result.id, file.name, isPublic);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Upload fallito: ${message}`);
    } finally {
      setUploading(false);
    }
  }, [file, isPublic, API_BASE, onComplete]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Carica il Regolamento PDF
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Carica il file PDF del regolamento del gioco. Verra' processato per estrarre le regole.
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
              Cambia file
            </Button>
          </div>
        ) : (
          <div className="space-y-2">
            <div className="text-4xl">📁</div>
            <p className="font-medium text-slate-900 dark:text-white">
              Trascina qui il PDF o clicca per selezionare
            </p>
            <p className="text-sm text-slate-500">PDF fino a 50MB</p>
          </div>
        )}
      </div>

      {/* Public checkbox */}
      <div className="flex items-start space-x-3">
        <Checkbox
          id="is-public"
          checked={isPublic}
          onCheckedChange={checked => setIsPublic(checked === true)}
        />
        <div className="space-y-1">
          <Label htmlFor="is-public" className="font-medium cursor-pointer">
            Aggiungi alla Libreria Pubblica
          </Label>
          <p className="text-sm text-slate-500">
            Il PDF sara' visibile a tutti gli utenti registrati nella libreria pubblica.
          </p>
        </div>
      </div>

      {/* Progress bar */}
      {uploading && (
        <div className="space-y-2">
          <div className="flex justify-between text-sm">
            <span>Caricamento in corso...</span>
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

      {/* Actions */}
      <div className="flex justify-end gap-3">
        <Button onClick={handleUpload} disabled={!file || uploading} className="min-w-32">
          {uploading ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Caricamento...
            </>
          ) : (
            'Carica PDF →'
          )}
        </Button>
      </div>
    </div>
  );
}
