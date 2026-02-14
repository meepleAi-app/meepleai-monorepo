'use client';

/**
 * Bulk Import JSON Upload - Client Component
 * Issue #4355: Frontend - Bulk Import Upload UI
 * Epic #4136: Admin Game Import
 *
 * Features:
 * - Drag-drop or file select for JSON files
 * - Textarea for direct JSON paste
 * - Client-side validation (JSON format, max 10MB)
 * - JSON format example tooltip
 * - API integration via useMutation
 * - Result display with enqueued/skipped/failed breakdown
 */

import { useState, useCallback, useRef, type ChangeEvent, type DragEvent } from 'react';
import type { JSX } from 'react';

import { useMutation } from '@tanstack/react-query';
import {
  Upload,
  FileJson,
  AlertCircle,
  CheckCircle,
  Info,
  Trash2,
  ClipboardPaste,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from '@/components/ui/overlays/tooltip';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { BulkImportFromJsonResult } from '@/lib/api/schemas/admin.schemas';
import { cn } from '@/lib/utils';

// Validation constants
const MAX_JSON_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
const MAX_ENTRIES = 500;

const JSON_FORMAT_EXAMPLE = `[
  { "bggId": 174430, "name": "Gloomhaven" },
  { "bggId": 167791, "name": "Terraforming Mars" },
  { "bggId": 169786, "name": "Scythe" }
]`;

interface ValidationError {
  field: string;
  message: string;
}

function validateJsonContent(content: string): { isValid: boolean; errors: ValidationError[]; entryCount: number } {
  const errors: ValidationError[] = [];
  let entryCount = 0;

  if (!content.trim()) {
    errors.push({ field: 'content', message: 'JSON content is empty' });
    return { isValid: false, errors, entryCount };
  }

  // Size check
  const sizeBytes = new TextEncoder().encode(content).length;
  if (sizeBytes > MAX_JSON_SIZE_BYTES) {
    const sizeMB = (sizeBytes / 1024 / 1024).toFixed(1);
    errors.push({ field: 'size', message: `Content size (${sizeMB} MB) exceeds maximum (10 MB)` });
  }

  // JSON parse check
  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    errors.push({ field: 'format', message: 'Invalid JSON format. Please check syntax.' });
    return { isValid: false, errors, entryCount };
  }

  // Must be array
  if (!Array.isArray(parsed)) {
    errors.push({ field: 'structure', message: 'JSON must be an array of objects' });
    return { isValid: false, errors, entryCount };
  }

  entryCount = parsed.length;

  // Empty array
  if (parsed.length === 0) {
    errors.push({ field: 'content', message: 'JSON array is empty. Add at least one game entry.' });
    return { isValid: false, errors, entryCount };
  }

  // Max entries
  if (parsed.length > MAX_ENTRIES) {
    errors.push({ field: 'count', message: `Too many entries (${parsed.length}). Maximum is ${MAX_ENTRIES}.` });
  }

  // Validate each entry has required fields
  const invalidEntries: number[] = [];
  for (let i = 0; i < Math.min(parsed.length, 10); i++) {
    const entry = parsed[i];
    if (typeof entry !== 'object' || entry === null) {
      invalidEntries.push(i);
      continue;
    }
    const obj = entry as Record<string, unknown>;
    if (typeof obj.bggId !== 'number' || !Number.isInteger(obj.bggId) || obj.bggId <= 0) {
      invalidEntries.push(i);
      continue;
    }
    if (typeof obj.name !== 'string' || obj.name.trim() === '') {
      invalidEntries.push(i);
    }
  }

  if (invalidEntries.length > 0) {
    errors.push({
      field: 'entries',
      message: `Invalid entries at index${invalidEntries.length > 1 ? 'es' : ''}: ${invalidEntries.join(', ')}. Each entry must have a positive integer "bggId" and non-empty "name".`,
    });
  }

  return { isValid: errors.length === 0, errors, entryCount };
}

export function BulkImportJsonUploader(): JSX.Element {
  const [jsonContent, setJsonContent] = useState('');
  const [fileName, setFileName] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [result, setResult] = useState<BulkImportFromJsonResult | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { mutate: submitImport, isPending, error: apiError, reset: resetMutation } = useMutation({
    mutationFn: (content: string) => api.admin.bulkImportGames(content),
    onSuccess: (data) => {
      setResult(data);
    },
  });

  // Read file content
  const processFile = useCallback((file: File) => {
    if (file.size > MAX_JSON_SIZE_BYTES) {
      const sizeMB = (file.size / 1024 / 1024).toFixed(1);
      setValidationErrors([{ field: 'size', message: `File size (${sizeMB} MB) exceeds maximum (10 MB)` }]);
      return;
    }

    if (!file.name.endsWith('.json') && file.type !== 'application/json') {
      setValidationErrors([{ field: 'format', message: 'Please select a .json file' }]);
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      setJsonContent(content);
      setFileName(file.name);
      setValidationErrors([]);
      setResult(null);
      resetMutation();
    };
    reader.onerror = () => {
      setValidationErrors([{ field: 'read', message: 'Failed to read file' }]);
    };
    reader.readAsText(file);
  }, [resetMutation]);

  // File input handler
  const handleFileChange = useCallback((e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      processFile(file);
    }
  }, [processFile]);

  // Drag-drop handlers
  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
  }, []);

  const handleDrop = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);

    const file = e.dataTransfer.files[0];
    if (file) {
      processFile(file);
    }
  }, [processFile]);

  // Submit handler
  const handleSubmit = useCallback(() => {
    const validation = validateJsonContent(jsonContent);
    if (!validation.isValid) {
      setValidationErrors(validation.errors);
      return;
    }
    setValidationErrors([]);
    submitImport(jsonContent);
  }, [jsonContent, submitImport]);

  // Reset handler
  const handleReset = useCallback(() => {
    setJsonContent('');
    setFileName(null);
    setValidationErrors([]);
    setResult(null);
    resetMutation();
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, [resetMutation]);

  // Handle paste from clipboard
  const handlePasteFromClipboard = useCallback(async () => {
    try {
      const text = await navigator.clipboard.readText();
      if (text) {
        setJsonContent(text);
        setFileName(null);
        setValidationErrors([]);
        setResult(null);
        resetMutation();
      }
    } catch {
      setValidationErrors([{ field: 'clipboard', message: 'Unable to read clipboard. Please paste manually.' }]);
    }
  }, [resetMutation]);

  const { entryCount } = jsonContent ? validateJsonContent(jsonContent) : { entryCount: 0 };
  const hasContent = jsonContent.trim().length > 0;

  return (
    <TooltipProvider>
      <div className="space-y-6" data-testid="bulk-import-uploader">
        {/* Header */}
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Bulk Import Games from JSON</h2>
          <p className="text-muted-foreground mt-1">
            Upload a JSON file or paste JSON content to bulk import games into the catalog.
          </p>
        </div>

        {/* JSON Format Example */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <Info className="h-4 w-4 text-muted-foreground" />
              <CardTitle className="text-sm font-medium">Expected JSON Format</CardTitle>
            </div>
          </CardHeader>
          <CardContent>
            <pre className="rounded-md bg-muted p-3 text-xs font-mono overflow-x-auto" data-testid="json-format-example">
              {JSON_FORMAT_EXAMPLE}
            </pre>
            <p className="text-xs text-muted-foreground mt-2">
              Each entry requires a positive integer <code className="text-xs bg-muted px-1 rounded">bggId</code> (BoardGameGeek ID) and a non-empty <code className="text-xs bg-muted px-1 rounded">name</code>. Maximum {MAX_ENTRIES} entries per import.
            </p>
          </CardContent>
        </Card>

        {/* Upload Area */}
        {!result && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Upload JSON</CardTitle>
              <CardDescription>Drag and drop a .json file, select from your computer, or paste JSON content directly.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Drag & Drop Zone */}
              <div
                className={cn(
                  'relative rounded-lg border-2 border-dashed p-8 text-center transition-colors cursor-pointer',
                  isDragOver
                    ? 'border-primary bg-primary/5'
                    : 'border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/50'
                )}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
                data-testid="drop-zone"
                role="button"
                tabIndex={0}
                aria-label="Drop JSON file here or click to select"
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') fileInputRef.current?.click(); }}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".json,application/json"
                  onChange={handleFileChange}
                  className="hidden"
                  data-testid="file-input"
                  aria-label="Select JSON file"
                />
                <FileJson className="mx-auto h-10 w-10 text-muted-foreground mb-3" />
                <p className="text-sm font-medium">
                  {isDragOver ? 'Drop JSON file here' : 'Drag & drop a .json file here'}
                </p>
                <p className="text-xs text-muted-foreground mt-1">
                  or click to select from your computer
                </p>
                {fileName && (
                  <div className="mt-3 flex items-center justify-center gap-2">
                    <Badge variant="secondary">
                      <FileJson className="h-3 w-3 mr-1" />
                      {fileName}
                    </Badge>
                  </div>
                )}
              </div>

              {/* Divider */}
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <span className="w-full border-t" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-card px-2 text-muted-foreground">or paste JSON</span>
                </div>
              </div>

              {/* Textarea + Paste Button */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <label htmlFor="json-input" className="text-sm font-medium">
                    JSON Content
                  </label>
                  <div className="flex items-center gap-2">
                    {hasContent && (
                      <Badge variant="outline" className="text-xs">
                        {entryCount} {entryCount === 1 ? 'entry' : 'entries'}
                      </Badge>
                    )}
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={handlePasteFromClipboard}
                          disabled={isPending}
                          className="h-7 text-xs"
                        >
                          <ClipboardPaste className="h-3 w-3 mr-1" />
                          Paste
                        </Button>
                      </TooltipTrigger>
                      <TooltipContent>Paste JSON from clipboard</TooltipContent>
                    </Tooltip>
                  </div>
                </div>
                <Textarea
                  id="json-input"
                  value={jsonContent}
                  onChange={(e) => {
                    setJsonContent(e.target.value);
                    setFileName(null);
                    setValidationErrors([]);
                    setResult(null);
                    resetMutation();
                  }}
                  placeholder={JSON_FORMAT_EXAMPLE}
                  className="min-h-[200px] font-mono text-xs"
                  disabled={isPending}
                  data-testid="json-textarea"
                />
              </div>

              {/* Validation Errors */}
              {validationErrors.length > 0 && (
                <Alert variant="destructive" data-testid="validation-errors">
                  <AlertCircle className="h-4 w-4" />
                  <AlertTitle>Validation Failed</AlertTitle>
                  <AlertDescription>
                    <ul className="mt-2 space-y-1 text-sm">
                      {validationErrors.map((err) => (
                        <li key={err.field}>{err.message}</li>
                      ))}
                    </ul>
                  </AlertDescription>
                </Alert>
              )}

              {/* API Error */}
              {apiError && (
                <Alert variant="destructive" data-testid="api-error">
                  <AlertCircle className="h-4 w-4" />
                  <AlertTitle>Import Failed</AlertTitle>
                  <AlertDescription className="text-sm">
                    {apiError instanceof Error ? apiError.message : 'An unexpected error occurred'}
                  </AlertDescription>
                </Alert>
              )}

              {/* Action Buttons */}
              <div className="flex items-center gap-3">
                <Button
                  onClick={handleSubmit}
                  disabled={!hasContent || isPending}
                  data-testid="submit-import"
                >
                  {isPending ? (
                    <>Importing...</>
                  ) : (
                    <>
                      <Upload className="mr-2 h-4 w-4" />
                      Import Games
                    </>
                  )}
                </Button>
                {hasContent && (
                  <Button variant="outline" onClick={handleReset} disabled={isPending} data-testid="reset-button">
                    <Trash2 className="mr-2 h-4 w-4" />
                    Clear
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        )}

        {/* Result Display */}
        {result && (
          <Card data-testid="import-result">
            <CardHeader>
              <div className="flex items-center gap-2">
                <CheckCircle className="h-5 w-5 text-green-500" />
                <CardTitle className="text-base">Import Complete</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Summary Stats */}
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <div className="rounded-lg border p-3 text-center">
                  <p className="text-2xl font-bold">{result.total}</p>
                  <p className="text-xs text-muted-foreground">Total</p>
                </div>
                <div className="rounded-lg border p-3 text-center border-green-500/30 bg-green-500/5">
                  <p className="text-2xl font-bold text-green-600 dark:text-green-400">{result.enqueued}</p>
                  <p className="text-xs text-muted-foreground">Enqueued</p>
                </div>
                <div className="rounded-lg border p-3 text-center border-yellow-500/30 bg-yellow-500/5">
                  <p className="text-2xl font-bold text-yellow-600 dark:text-yellow-400">{result.skipped}</p>
                  <p className="text-xs text-muted-foreground">Skipped</p>
                </div>
                <div className="rounded-lg border p-3 text-center border-red-500/30 bg-red-500/5">
                  <p className="text-2xl font-bold text-red-600 dark:text-red-400">{result.failed}</p>
                  <p className="text-xs text-muted-foreground">Failed</p>
                </div>
              </div>

              {/* Error Details */}
              {result.errors.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-sm font-medium">Errors & Skipped Items</h4>
                  <div className="max-h-60 overflow-y-auto rounded-md border">
                    <table className="w-full text-sm">
                      <thead className="bg-muted/50 sticky top-0">
                        <tr>
                          <th className="px-3 py-2 text-left font-medium">BGG ID</th>
                          <th className="px-3 py-2 text-left font-medium">Game</th>
                          <th className="px-3 py-2 text-left font-medium">Type</th>
                          <th className="px-3 py-2 text-left font-medium">Reason</th>
                        </tr>
                      </thead>
                      <tbody>
                        {result.errors.map((err, idx) => (
                          <tr key={idx} className="border-t">
                            <td className="px-3 py-2 text-muted-foreground">{err.bggId ?? '-'}</td>
                            <td className="px-3 py-2">{err.gameName ?? '-'}</td>
                            <td className="px-3 py-2">
                              <Badge variant={err.errorType === 'Duplicate' ? 'secondary' : 'destructive'} className="text-xs">
                                {err.errorType}
                              </Badge>
                            </td>
                            <td className="px-3 py-2 text-muted-foreground">{err.reason}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Import Another */}
              <Button variant="outline" onClick={handleReset} data-testid="import-another">
                Import Another Batch
              </Button>
            </CardContent>
          </Card>
        )}
      </div>
    </TooltipProvider>
  );
}
