/**
 * CatalogWizard - 3-Step Guided Wizard for Shared Game Content
 *
 * Issue #118: Guided Wizard for admin content management.
 * Steps: 1) Select Game  2) Upload PDFs  3) Review & Submit
 */

'use client';

import { useCallback, useRef, useState } from 'react';

import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  FileUp,
  Loader2,
  Search,
  XCircle,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type {
  BulkUploadPdfsResult,
  SharedGameDocumentsResult,
} from '@/lib/api/clients/adminClient';

type WizardStep = 1 | 2 | 3;

interface SelectedGame {
  id: string;
  title: string;
}

export function CatalogWizard() {
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [step, setStep] = useState<WizardStep>(1);
  const [selectedGame, setSelectedGame] = useState<SelectedGame | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [uploadResult, setUploadResult] = useState<BulkUploadPdfsResult | null>(null);
  const [existingDocs, setExistingDocs] = useState<SharedGameDocumentsResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Array<{ id: string; title: string }>>([]);

  const handleSearch = useCallback(async () => {
    if (!searchQuery.trim()) return;
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.sharedGames.search({ searchTerm: searchQuery, pageSize: 10 });
      setSearchResults(
        (result.items ?? []).map((g: { id: string; title: string }) => ({
          id: g.id,
          title: g.title,
        }))
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setIsLoading(false);
    }
  }, [searchQuery]);

  const handleSelectGame = useCallback(async (game: SelectedGame) => {
    setSelectedGame(game);
    setError(null);
    try {
      const docs = await api.admin.getSharedGameDocuments(game.id);
      setExistingDocs(docs);
    } catch {
      // Non-blocking
    }
    setStep(2);
  }, []);

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    const pdfFiles = files.filter(
      f => f.type === 'application/pdf' || f.name.toLowerCase().endsWith('.pdf')
    );
    setSelectedFiles(prev => [...prev, ...pdfFiles]);
    if (e.target) e.target.value = '';
  }, []);

  const handleRemoveFile = useCallback((index: number) => {
    setSelectedFiles(prev => prev.filter((_, i) => i !== index));
  }, []);

  const handleUpload = useCallback(async () => {
    if (!selectedGame || selectedFiles.length === 0) return;
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.admin.bulkUploadPdfs(selectedGame.id, selectedFiles);
      setUploadResult(result);
      setStep(3);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed');
    } finally {
      setIsLoading(false);
    }
  }, [selectedGame, selectedFiles]);

  const handleFinish = useCallback(() => {
    if (selectedGame) {
      router.push(`/admin/shared-games/${selectedGame.id}`);
    } else {
      router.push('/admin/shared-games/all');
    }
  }, [router, selectedGame]);

  const steps = [
    { number: 1, label: 'Select Game' },
    { number: 2, label: 'Upload PDFs' },
    { number: 3, label: 'Review' },
  ] as const;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center justify-center gap-4">
        {steps.map((s, i) => (
          <div key={s.number} className="flex items-center gap-2">
            <div
              className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium ${
                step >= s.number
                  ? 'bg-orange-500 text-white'
                  : 'bg-zinc-200 text-zinc-500 dark:bg-zinc-700 dark:text-zinc-400'
              }`}
            >
              {step > s.number ? <CheckCircle2 className="h-4 w-4" /> : s.number}
            </div>
            <span
              className={`text-sm ${
                step >= s.number
                  ? 'font-medium text-zinc-900 dark:text-zinc-100'
                  : 'text-zinc-500 dark:text-zinc-400'
              }`}
            >
              {s.label}
            </span>
            {i < steps.length - 1 && (
              <div
                className={`h-px w-12 ${
                  step > s.number ? 'bg-orange-500' : 'bg-zinc-200 dark:bg-zinc-700'
                }`}
              />
            )}
          </div>
        ))}
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {step === 1 && (
        <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
          <CardHeader>
            <CardTitle>Select a Game</CardTitle>
            <CardDescription>
              Search for an existing shared game to add documents to.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search games by title..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleSearch()}
              />
              <Button onClick={handleSearch} disabled={isLoading || !searchQuery.trim()}>
                {isLoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Search className="h-4 w-4" />
                )}
              </Button>
            </div>
            {searchResults.length > 0 && (
              <div className="space-y-2">
                {searchResults.map(game => (
                  <button
                    key={game.id}
                    type="button"
                    onClick={() => handleSelectGame(game)}
                    className="w-full rounded-lg border p-3 text-left transition-colors hover:bg-zinc-50 dark:hover:bg-zinc-800"
                  >
                    <span className="font-medium">{game.title}</span>
                  </button>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {step === 2 && selectedGame && (
        <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
          <CardHeader>
            <CardTitle>Upload PDFs for {selectedGame.title}</CardTitle>
            <CardDescription>
              Select one or more PDF files (rulebooks, errata, supplements).
              {existingDocs && existingDocs.totalCount > 0 && (
                <span className="ml-1 text-orange-600">
                  This game already has {existingDocs.totalCount} document(s).
                </span>
              )}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div
              className="flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-zinc-300 p-8 transition-colors hover:border-orange-400 dark:border-zinc-600"
              onClick={() => fileInputRef.current?.click()}
              role="button"
              tabIndex={0}
              onKeyDown={e => e.key === 'Enter' && fileInputRef.current?.click()}
            >
              <FileUp className="mb-2 h-8 w-8 text-zinc-400" />
              <span className="text-sm text-zinc-600 dark:text-zinc-400">
                Click to select PDF files
              </span>
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,application/pdf"
                multiple
                className="hidden"
                onChange={handleFileChange}
              />
            </div>
            {selectedFiles.length > 0 && (
              <div className="space-y-2">
                <h4 className="text-sm font-medium">Selected Files ({selectedFiles.length})</h4>
                {selectedFiles.map((file, index) => (
                  <div
                    key={`${file.name}-${index}`}
                    className="flex items-center justify-between rounded-lg border p-2"
                  >
                    <div>
                      <span className="text-sm font-medium">{file.name}</span>
                      <span className="ml-2 text-xs text-zinc-500">
                        {(file.size / 1024 / 1024).toFixed(1)} MB
                      </span>
                    </div>
                    <Button variant="ghost" size="sm" onClick={() => handleRemoveFile(index)}>
                      <XCircle className="h-4 w-4 text-red-500" />
                    </Button>
                  </div>
                ))}
              </div>
            )}
            <div className="flex justify-between pt-4">
              <Button variant="outline" onClick={() => setStep(1)}>
                <ArrowLeft className="mr-2 h-4 w-4" /> Back
              </Button>
              <Button onClick={handleUpload} disabled={isLoading || selectedFiles.length === 0}>
                {isLoading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Uploading...
                  </>
                ) : (
                  <>
                    Upload {selectedFiles.length} file(s) <ArrowRight className="ml-2 h-4 w-4" />
                  </>
                )}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {step === 3 && uploadResult && (
        <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
          <CardHeader>
            <CardTitle>Upload Complete</CardTitle>
            <CardDescription>
              {uploadResult.successCount} of {uploadResult.totalRequested} files uploaded
              successfully.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-3 gap-4">
              <div className="rounded-lg bg-green-50 p-3 text-center dark:bg-green-900/20">
                <div className="text-2xl font-bold text-green-600">{uploadResult.successCount}</div>
                <div className="text-xs text-green-700 dark:text-green-400">Succeeded</div>
              </div>
              <div className="rounded-lg bg-red-50 p-3 text-center dark:bg-red-900/20">
                <div className="text-2xl font-bold text-red-600">{uploadResult.failedCount}</div>
                <div className="text-xs text-red-700 dark:text-red-400">Failed</div>
              </div>
              <div className="rounded-lg bg-zinc-50 p-3 text-center dark:bg-zinc-800">
                <div className="text-2xl font-bold text-zinc-600 dark:text-zinc-300">
                  {uploadResult.totalRequested}
                </div>
                <div className="text-xs text-zinc-500">Total</div>
              </div>
            </div>
            {uploadResult.items.length > 0 && (
              <div className="space-y-2">
                <h4 className="text-sm font-medium">File Results</h4>
                {uploadResult.items.map((item, index) => (
                  <div
                    key={`${item.fileName}-${index}`}
                    className="flex items-center justify-between rounded-lg border p-2"
                  >
                    <span className="text-sm">{item.fileName}</span>
                    {item.success ? (
                      <CheckCircle2 className="h-4 w-4 text-green-500" />
                    ) : (
                      <span className="text-xs text-red-500">{item.error}</span>
                    )}
                  </div>
                ))}
              </div>
            )}
            <div className="flex justify-end pt-4">
              <Button onClick={handleFinish}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> View Game Details
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
