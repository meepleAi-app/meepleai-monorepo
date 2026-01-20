'use client';
/* eslint-disable security/detect-object-injection */

/**
 * Bulk Import Client Component - Issue #2372
 *
 * Client component for importing games in bulk:
 * - CSV file upload with preview and validation
 * - BGG ID import with data fetching
 * - Progress tracking and error handling
 */

import { useState, useCallback, useRef } from 'react';

import {
  ArrowLeft,
  Upload,
  FileSpreadsheet,
  FileJson,
  Globe,
  AlertCircle,
  CheckCircle2,
  XCircle,
  Loader2,
  Download,
  Trash2,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Separator } from '@/components/ui/navigation/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useApiClient } from '@/lib/api/context';
import type {
  CreateSharedGameRequest,
  BulkGameImportDto,
  BulkImportResult,
} from '@/lib/api/schemas/shared-games.schemas';
import { BulkGameImportDtoSchema } from '@/lib/api/schemas/shared-games.schemas';

// CSV column mapping for game data
interface CsvRow {
  title: string;
  yearPublished: string;
  description: string;
  minPlayers: string;
  maxPlayers: string;
  playingTimeMinutes: string;
  minAge: string;
  complexityRating?: string;
  averageRating?: string;
  imageUrl: string;
  thumbnailUrl: string;
  bggId?: string;
}

// Import item with status tracking
interface ImportItem {
  id: string;
  data: CsvRow;
  status: 'pending' | 'importing' | 'success' | 'error';
  error?: string;
  gameId?: string;
}

// BGG import item
interface BggImportItem {
  bggId: number;
  status: 'pending' | 'fetching' | 'ready' | 'importing' | 'success' | 'error';
  data?: Partial<CsvRow>;
  error?: string;
  gameId?: string;
}

// JSON import item
interface JsonImportItem {
  id: string;
  data: BulkGameImportDto;
  status: 'pending' | 'valid' | 'invalid';
  validationError?: string;
}

type ImportMode = 'csv' | 'bgg' | 'json';

// CSV template headers
const CSV_HEADERS = [
  'title',
  'yearPublished',
  'description',
  'minPlayers',
  'maxPlayers',
  'playingTimeMinutes',
  'minAge',
  'complexityRating',
  'averageRating',
  'imageUrl',
  'thumbnailUrl',
  'bggId',
];

export function ImportClient() {
  const router = useRouter();
  const { sharedGames } = useApiClient();
  const { user, loading: authLoading } = useAuthUser();
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Early return if no user
  if (!user) return null;

  // Import mode
  const [mode, setMode] = useState<ImportMode>('csv');

  // CSV Import state
  const [csvItems, setCsvItems] = useState<ImportItem[]>([]);
  const [csvFileName, setCsvFileName] = useState<string>('');
  const [csvParseError, setCsvParseError] = useState<string | null>(null);

  // BGG Import state
  const [bggInput, setBggInput] = useState<string>('');
  const [bggItems, setBggItems] = useState<BggImportItem[]>([]);

  // JSON Import state
  const [jsonItems, setJsonItems] = useState<JsonImportItem[]>([]);
  const [jsonParseError, setJsonParseError] = useState<string | null>(null);
  const [jsonInputSource, setJsonInputSource] = useState<'file' | 'textarea'>('file');
  const [jsonTextInput, setJsonTextInput] = useState<string>('');
  const [jsonFileName, setJsonFileName] = useState<string>('');
  const [jsonImportResult, setJsonImportResult] = useState<BulkImportResult | null>(null);
  const jsonFileInputRef = useRef<HTMLInputElement>(null);

  // Import progress
  const [importing, setImporting] = useState(false);
  const [importProgress, setImportProgress] = useState(0);
  const [importTotal, setImportTotal] = useState(0);

  // Navigation
  const handleBack = () => {
    router.push('/admin/shared-games');
  };

  // ========== CSV Import Functions ==========

  const parseCsv = useCallback((content: string): CsvRow[] => {
    const lines = content.split('\n').filter(line => line.trim() !== '');
    if (lines.length < 2) {
      throw new Error(
        'Il file CSV deve contenere almeno una riga di intestazione e una riga di dati'
      );
    }

    // Parse header row
    const headerLine = lines[0];
    const headers = headerLine.split(',').map(h => h.trim().replace(/^["']|["']$/g, ''));

    // Validate required headers
    const requiredHeaders = [
      'title',
      'yearPublished',
      'description',
      'minPlayers',
      'maxPlayers',
      'playingTimeMinutes',
      'minAge',
      'imageUrl',
      'thumbnailUrl',
    ];
    const missingHeaders = requiredHeaders.filter(h => !headers.includes(h));
    if (missingHeaders.length > 0) {
      throw new Error(`Intestazioni mancanti: ${missingHeaders.join(', ')}`);
    }

    // Parse data rows
    const rows: CsvRow[] = [];
    for (let i = 1; i < lines.length; i++) {
      const line = lines[i];
      const values = parseCsvLine(line);

      if (values.length !== headers.length) {
        console.warn(`Riga ${i + 1}: numero di colonne non corrispondente, verrà ignorata`);
        continue;
      }

      const row: Record<string, string> = {};
      headers.forEach((header, index) => {
        row[header] = values[index] || '';
      });

      rows.push(row as unknown as CsvRow);
    }

    return rows;
  }, []);

  // Parse a single CSV line (handles quoted values with commas)
  const parseCsvLine = (line: string): string[] => {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i++) {
      const char = line[i];

      if (char === '"') {
        inQuotes = !inQuotes;
      } else if (char === ',' && !inQuotes) {
        values.push(current.trim().replace(/^["']|["']$/g, ''));
        current = '';
      } else {
        current += char;
      }
    }

    values.push(current.trim().replace(/^["']|["']$/g, ''));
    return values;
  };

  const handleFileSelect = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) return;

      if (!file.name.endsWith('.csv')) {
        setCsvParseError('Seleziona un file CSV (.csv)');
        return;
      }

      setCsvFileName(file.name);
      setCsvParseError(null);

      const reader = new FileReader();
      reader.onload = e => {
        try {
          const content = e.target?.result as string;
          const rows = parseCsv(content);

          const items: ImportItem[] = rows.map((row, index) => ({
            id: `csv-${index}-${Date.now()}`,
            data: row,
            status: 'pending',
          }));

          setCsvItems(items);
          toast.success(`${items.length} giochi trovati nel file CSV`);
        } catch (error) {
          setCsvParseError(
            error instanceof Error ? error.message : 'Errore durante il parsing del CSV'
          );
          setCsvItems([]);
        }
      };

      reader.onerror = () => {
        setCsvParseError('Errore durante la lettura del file');
      };

      reader.readAsText(file);
    },
    [parseCsv]
  );

  const clearCsvImport = useCallback(() => {
    setCsvItems([]);
    setCsvFileName('');
    setCsvParseError(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, []);

  const removeCsvItem = useCallback((id: string) => {
    setCsvItems(prev => prev.filter(item => item.id !== id));
  }, []);

  // ========== BGG Import Functions ==========

  const parseBggIds = useCallback((input: string): number[] => {
    const ids: number[] = [];
    // Support comma-separated, space-separated, or newline-separated
    const parts = input.split(/[,\s\n]+/).filter(p => p.trim() !== '');

    for (const part of parts) {
      const num = parseInt(part.trim(), 10);
      if (!isNaN(num) && num > 0) {
        ids.push(num);
      }
    }

    return [...new Set(ids)]; // Remove duplicates
  }, []);

  const addBggIds = useCallback(() => {
    const ids = parseBggIds(bggInput);
    if (ids.length === 0) {
      toast.error('Inserisci almeno un ID BGG valido');
      return;
    }

    // Filter out already added IDs
    const existingIds = new Set(bggItems.map(item => item.bggId));
    const newIds = ids.filter(id => !existingIds.has(id));

    if (newIds.length === 0) {
      toast.warning('Tutti gli ID sono già stati aggiunti');
      return;
    }

    const newItems: BggImportItem[] = newIds.map(id => ({
      bggId: id,
      status: 'pending',
    }));

    setBggItems(prev => [...prev, ...newItems]);
    setBggInput('');
    toast.success(`${newIds.length} ID BGG aggiunti`);
  }, [bggInput, bggItems, parseBggIds]);

  const removeBggItem = useCallback((bggId: number) => {
    setBggItems(prev => prev.filter(item => item.bggId !== bggId));
  }, []);

  const clearBggImport = useCallback(() => {
    setBggItems([]);
    setBggInput('');
  }, []);

  // Fetch BGG data (placeholder - would need BGG API integration)
  const fetchBggData = useCallback(async (bggId: number): Promise<Partial<CsvRow>> => {
    // TODO: Integrate with actual BGG API or backend endpoint
    // For now, return placeholder data that will need to be filled in
    return {
      title: `Game from BGG #${bggId}`,
      yearPublished: new Date().getFullYear().toString(),
      description: `Imported from BoardGameGeek ID: ${bggId}. Please update the description.`,
      minPlayers: '2',
      maxPlayers: '4',
      playingTimeMinutes: '60',
      minAge: '10',
      imageUrl: 'https://via.placeholder.com/300x300?text=Update+Image',
      thumbnailUrl: 'https://via.placeholder.com/150x150?text=Update+Thumbnail',
      bggId: bggId.toString(),
    };
  }, []);

  const fetchAllBggData = useCallback(async () => {
    const pendingItems = bggItems.filter(item => item.status === 'pending');
    if (pendingItems.length === 0) {
      toast.warning('Nessun ID BGG da recuperare');
      return;
    }

    for (const item of pendingItems) {
      setBggItems(prev =>
        prev.map(i => (i.bggId === item.bggId ? { ...i, status: 'fetching' } : i))
      );

      try {
        const data = await fetchBggData(item.bggId);
        setBggItems(prev =>
          prev.map(i => (i.bggId === item.bggId ? { ...i, status: 'ready', data } : i))
        );
      } catch (error) {
        setBggItems(prev =>
          prev.map(i =>
            i.bggId === item.bggId
              ? {
                  ...i,
                  status: 'error',
                  error: error instanceof Error ? error.message : 'Errore recupero dati',
                }
              : i
          )
        );
      }
    }

    toast.success('Dati BGG recuperati');
  }, [bggItems, fetchBggData]);

  // ========== JSON Import Functions ==========

  const parseJsonInput = useCallback(
    (content: string): JsonImportItem[] => {
      let parsed: unknown;

      try {
        parsed = JSON.parse(content);
      } catch {
        throw new Error('JSON non valido. Verifica la sintassi del file.');
      }

      // Support both [...] and { "games": [...] } formats
      let gamesArray: unknown[];
      if (Array.isArray(parsed)) {
        gamesArray = parsed;
      } else if (
        typeof parsed === 'object' &&
        parsed !== null &&
        'games' in parsed &&
        Array.isArray((parsed as { games: unknown[] }).games)
      ) {
        gamesArray = (parsed as { games: unknown[] }).games;
      } else {
        throw new Error(
          'Formato non valido. Atteso un array di giochi o un oggetto con proprietà "games".'
        );
      }

      if (gamesArray.length === 0) {
        throw new Error('Il file JSON non contiene giochi.');
      }

      if (gamesArray.length > 100) {
        throw new Error('Massimo 100 giochi per importazione. Il file ne contiene ' + gamesArray.length + '.');
      }

      const items: JsonImportItem[] = gamesArray.map((item, index) => {
        const id = `json-${index}-${Date.now()}`;

        // Validate with Zod schema
        const validation = BulkGameImportDtoSchema.safeParse(item);

        if (!validation.success) {
          const errorMessages = validation.error.errors
            .map(e => `${e.path.join('.')}: ${e.message}`)
            .join(', ');
          return {
            id,
            data: item as BulkGameImportDto,
            status: 'invalid' as const,
            validationError: errorMessages,
          };
        }

        // Check that at least bggId or title is present
        const data = validation.data;
        if (!data.bggId && !data.title) {
          return {
            id,
            data,
            status: 'invalid' as const,
            validationError: 'Richiesto almeno bggId o title',
          };
        }

        return {
          id,
          data,
          status: 'valid' as const,
        };
      });

      return items;
    },
    []
  );

  const handleJsonFileSelect = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) return;

      if (!file.name.endsWith('.json')) {
        setJsonParseError('Seleziona un file JSON (.json)');
        return;
      }

      setJsonFileName(file.name);
      setJsonParseError(null);
      setJsonImportResult(null);

      const reader = new FileReader();
      reader.onload = e => {
        try {
          const content = e.target?.result as string;
          const items = parseJsonInput(content);
          setJsonItems(items);

          const validCount = items.filter(i => i.status === 'valid').length;
          const invalidCount = items.filter(i => i.status === 'invalid').length;

          if (invalidCount > 0) {
            toast.warning(`${items.length} giochi trovati: ${validCount} validi, ${invalidCount} con errori`);
          } else {
            toast.success(`${items.length} giochi trovati nel file JSON`);
          }
        } catch (error) {
          setJsonParseError(
            error instanceof Error ? error.message : 'Errore durante il parsing del JSON'
          );
          setJsonItems([]);
        }
      };

      reader.onerror = () => {
        setJsonParseError('Errore durante la lettura del file');
      };

      reader.readAsText(file);
    },
    [parseJsonInput]
  );

  const parseJsonTextarea = useCallback(() => {
    if (!jsonTextInput.trim()) {
      toast.error('Inserisci del JSON da analizzare');
      return;
    }

    setJsonParseError(null);
    setJsonImportResult(null);

    try {
      const items = parseJsonInput(jsonTextInput);
      setJsonItems(items);

      const validCount = items.filter(i => i.status === 'valid').length;
      const invalidCount = items.filter(i => i.status === 'invalid').length;

      if (invalidCount > 0) {
        toast.warning(`${items.length} giochi trovati: ${validCount} validi, ${invalidCount} con errori`);
      } else {
        toast.success(`${items.length} giochi trovati`);
      }
    } catch (error) {
      setJsonParseError(
        error instanceof Error ? error.message : 'Errore durante il parsing del JSON'
      );
      setJsonItems([]);
    }
  }, [jsonTextInput, parseJsonInput]);

  const startJsonImport = useCallback(async () => {
    const validItems = jsonItems.filter(item => item.status === 'valid');
    if (validItems.length === 0) {
      toast.warning('Nessun gioco valido da importare');
      return;
    }

    setImporting(true);
    setJsonImportResult(null);

    try {
      const gamesToImport: BulkGameImportDto[] = validItems.map(item => item.data);
      const result = await sharedGames.bulkImport(gamesToImport);

      setJsonImportResult(result);

      if (result.failureCount === 0) {
        toast.success(`Importazione completata: ${result.successCount} giochi importati`);
      } else {
        toast.warning(
          `Importazione completata: ${result.successCount} successi, ${result.failureCount} errori`
        );
      }

      // Clear items on success
      if (result.successCount > 0 && result.failureCount === 0) {
        setJsonItems([]);
        setJsonTextInput('');
      }
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Errore durante l'importazione");
    } finally {
      setImporting(false);
    }
  }, [jsonItems, sharedGames]);

  const downloadJsonTemplate = useCallback(() => {
    const template = [
      {
        bggId: 13,
        title: 'Catan',
        yearPublished: 1995,
        description: 'I coloni di Catan è un gioco da tavolo...',
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        minAge: 10,
        complexityRating: 2.3,
        averageRating: 7.2,
        imageUrl: 'https://example.com/catan.jpg',
        thumbnailUrl: 'https://example.com/catan_thumb.jpg',
      },
    ];

    const content = JSON.stringify(template, null, 2);
    const blob = new Blob([content], { type: 'application/json;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'shared_games_template.json';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }, []);

  const clearJsonImport = useCallback(() => {
    setJsonItems([]);
    setJsonFileName('');
    setJsonParseError(null);
    setJsonTextInput('');
    setJsonImportResult(null);
    if (jsonFileInputRef.current) {
      jsonFileInputRef.current.value = '';
    }
  }, []);

  const removeJsonItem = useCallback((id: string) => {
    setJsonItems(prev => prev.filter(item => item.id !== id));
  }, []);

  // ========== Import Execution ==========

  const convertToCreateRequest = (row: CsvRow): CreateSharedGameRequest => {
    return {
      title: row.title,
      yearPublished: parseInt(row.yearPublished, 10),
      description: row.description,
      minPlayers: parseInt(row.minPlayers, 10),
      maxPlayers: parseInt(row.maxPlayers, 10),
      playingTimeMinutes: parseInt(row.playingTimeMinutes, 10),
      minAge: parseInt(row.minAge, 10),
      complexityRating: row.complexityRating ? parseFloat(row.complexityRating) : null,
      averageRating: row.averageRating ? parseFloat(row.averageRating) : null,
      imageUrl: row.imageUrl,
      thumbnailUrl: row.thumbnailUrl,
      bggId: row.bggId ? parseInt(row.bggId, 10) : null,
    };
  };

  const validateRow = (row: CsvRow): string | null => {
    if (!row.title || row.title.trim() === '') return 'Titolo mancante';
    if (!row.yearPublished || isNaN(parseInt(row.yearPublished, 10)))
      return 'Anno pubblicazione non valido';
    if (!row.description || row.description.trim() === '') return 'Descrizione mancante';
    if (!row.minPlayers || isNaN(parseInt(row.minPlayers, 10))) return 'Giocatori min non valido';
    if (!row.maxPlayers || isNaN(parseInt(row.maxPlayers, 10))) return 'Giocatori max non valido';
    if (!row.playingTimeMinutes || isNaN(parseInt(row.playingTimeMinutes, 10)))
      return 'Tempo di gioco non valido';
    if (!row.minAge || isNaN(parseInt(row.minAge, 10))) return 'Età minima non valida';
    if (!row.imageUrl) return 'URL immagine mancante';
    if (!row.thumbnailUrl) return 'URL thumbnail mancante';
    return null;
  };

  const startCsvImport = useCallback(async () => {
    const pendingItems = csvItems.filter(item => item.status === 'pending');
    if (pendingItems.length === 0) {
      toast.warning('Nessun gioco da importare');
      return;
    }

    setImporting(true);
    setImportProgress(0);
    setImportTotal(pendingItems.length);

    let successCount = 0;
    let errorCount = 0;

    for (let i = 0; i < pendingItems.length; i++) {
      const item = pendingItems[i];

      // Validate
      const validationError = validateRow(item.data);
      if (validationError) {
        setCsvItems(prev =>
          prev.map(it =>
            it.id === item.id ? { ...it, status: 'error', error: validationError } : it
          )
        );
        errorCount++;
        setImportProgress(i + 1);
        continue;
      }

      // Set importing status
      setCsvItems(prev =>
        prev.map(it => (it.id === item.id ? { ...it, status: 'importing' } : it))
      );

      try {
        const request = convertToCreateRequest(item.data);
        const gameId = await sharedGames.create(request);

        setCsvItems(prev =>
          prev.map(it => (it.id === item.id ? { ...it, status: 'success', gameId } : it))
        );
        successCount++;
      } catch (error) {
        setCsvItems(prev =>
          prev.map(it =>
            it.id === item.id
              ? {
                  ...it,
                  status: 'error',
                  error: error instanceof Error ? error.message : 'Errore importazione',
                }
              : it
          )
        );
        errorCount++;
      }

      setImportProgress(i + 1);
      // Small delay to prevent rate limiting
      await new Promise(resolve => setTimeout(resolve, 100));
    }

    setImporting(false);
    toast.success(`Importazione completata: ${successCount} successi, ${errorCount} errori`);
  }, [csvItems, sharedGames]);

  const startBggImport = useCallback(async () => {
    const readyItems = bggItems.filter(item => item.status === 'ready' && item.data);
    if (readyItems.length === 0) {
      toast.warning("Nessun gioco pronto per l'importazione. Recupera prima i dati BGG.");
      return;
    }

    setImporting(true);
    setImportProgress(0);
    setImportTotal(readyItems.length);

    let successCount = 0;
    let errorCount = 0;

    for (let i = 0; i < readyItems.length; i++) {
      const item = readyItems[i];

      setBggItems(prev =>
        prev.map(it => (it.bggId === item.bggId ? { ...it, status: 'importing' } : it))
      );

      try {
        const request = convertToCreateRequest(item.data as CsvRow);
        const gameId = await sharedGames.create(request);

        setBggItems(prev =>
          prev.map(it => (it.bggId === item.bggId ? { ...it, status: 'success', gameId } : it))
        );
        successCount++;
      } catch (error) {
        setBggItems(prev =>
          prev.map(it =>
            it.bggId === item.bggId
              ? {
                  ...it,
                  status: 'error',
                  error: error instanceof Error ? error.message : 'Errore importazione',
                }
              : it
          )
        );
        errorCount++;
      }

      setImportProgress(i + 1);
      await new Promise(resolve => setTimeout(resolve, 100));
    }

    setImporting(false);
    toast.success(`Importazione completata: ${successCount} successi, ${errorCount} errori`);
  }, [bggItems, sharedGames]);

  // ========== CSV Template Download ==========

  const downloadCsvTemplate = useCallback(() => {
    const header = CSV_HEADERS.join(',');
    const exampleRow = [
      '"Catan"',
      '1995',
      '"I coloni di Catan è un gioco da tavolo..."',
      '3',
      '4',
      '90',
      '10',
      '2.3',
      '7.2',
      '"https://example.com/catan.jpg"',
      '"https://example.com/catan_thumb.jpg"',
      '13',
    ].join(',');

    const content = `${header}\n${exampleRow}`;
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'shared_games_template.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }, []);

  // ========== Render Helpers ==========

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'pending':
        return <Badge variant="secondary">In attesa</Badge>;
      case 'importing':
      case 'fetching':
        return <Loader2 className="h-4 w-4 animate-spin text-primary" />;
      case 'ready':
        return <Badge variant="outline">Pronto</Badge>;
      case 'success':
        return <CheckCircle2 className="h-4 w-4 text-green-600" />;
      case 'error':
        return <XCircle className="h-4 w-4 text-destructive" />;
      default:
        return null;
    }
  };

  const csvPendingCount = csvItems.filter(i => i.status === 'pending').length;
  const csvSuccessCount = csvItems.filter(i => i.status === 'success').length;
  const csvErrorCount = csvItems.filter(i => i.status === 'error').length;

  const bggPendingCount = bggItems.filter(i => i.status === 'pending').length;
  const bggReadyCount = bggItems.filter(i => i.status === 'ready').length;
  const bggSuccessCount = bggItems.filter(i => i.status === 'success').length;
  const bggErrorCount = bggItems.filter(i => i.status === 'error').length;

  const jsonValidCount = jsonItems.filter(i => i.status === 'valid').length;
  const jsonInvalidCount = jsonItems.filter(i => i.status === 'invalid').length;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button variant="ghost" size="sm" onClick={handleBack} className="mb-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-center gap-3">
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Upload className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight">Importazione Giochi</h1>
              <p className="text-muted-foreground">Importa giochi da file CSV o da BoardGameGeek</p>
            </div>
          </div>
        </div>

        {/* Import Progress */}
        {importing && (
          <Card className="mb-6">
            <CardContent className="pt-6">
              <div className="flex items-center gap-4 mb-2">
                <Loader2 className="h-5 w-5 animate-spin text-primary" />
                <span className="font-medium">
                  Importazione in corso: {importProgress} / {importTotal}
                </span>
              </div>
              <Progress value={(importProgress / importTotal) * 100} />
            </CardContent>
          </Card>
        )}

        {/* Import Tabs */}
        <Tabs value={mode} onValueChange={v => setMode(v as ImportMode)}>
          <TabsList className="grid w-full grid-cols-3 mb-6">
            <TabsTrigger value="csv" className="flex items-center gap-2">
              <FileSpreadsheet className="h-4 w-4" />
              Importa da CSV
            </TabsTrigger>
            <TabsTrigger value="bgg" className="flex items-center gap-2">
              <Globe className="h-4 w-4" />
              Importa da BGG
            </TabsTrigger>
            <TabsTrigger value="json" className="flex items-center gap-2">
              <FileJson className="h-4 w-4" />
              Importa da JSON
            </TabsTrigger>
          </TabsList>

          {/* CSV Import Tab */}
          <TabsContent value="csv" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Carica File CSV</CardTitle>
                <CardDescription>
                  Carica un file CSV con i dati dei giochi da importare. Scarica il template per
                  vedere il formato richiesto.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center gap-4">
                  <div className="flex-1">
                    <Label htmlFor="csv-file">File CSV</Label>
                    <Input
                      ref={fileInputRef}
                      id="csv-file"
                      type="file"
                      accept=".csv"
                      onChange={handleFileSelect}
                      disabled={importing}
                      className="mt-1"
                    />
                    {csvFileName && (
                      <p className="text-sm text-muted-foreground mt-1">
                        File selezionato: {csvFileName}
                      </p>
                    )}
                  </div>
                  <Button variant="outline" onClick={downloadCsvTemplate} className="mt-6">
                    <Download className="h-4 w-4 mr-2" />
                    Scarica Template
                  </Button>
                </div>

                {csvParseError && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore</AlertTitle>
                    <AlertDescription>{csvParseError}</AlertDescription>
                  </Alert>
                )}

                {csvItems.length > 0 && (
                  <div className="flex items-center gap-4">
                    <Badge variant="secondary">{csvPendingCount} in attesa</Badge>
                    <Badge variant="default" className="bg-green-600">
                      {csvSuccessCount} importati
                    </Badge>
                    {csvErrorCount > 0 && (
                      <Badge variant="destructive">{csvErrorCount} errori</Badge>
                    )}
                    <div className="flex-1" />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={clearCsvImport}
                      disabled={importing}
                    >
                      <Trash2 className="h-4 w-4 mr-2" />
                      Cancella
                    </Button>
                    <Button onClick={startCsvImport} disabled={importing || csvPendingCount === 0}>
                      {importing ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                          Importazione...
                        </>
                      ) : (
                        <>
                          <Upload className="h-4 w-4 mr-2" />
                          Importa {csvPendingCount} giochi
                        </>
                      )}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* CSV Preview Table */}
            {csvItems.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle>Anteprima Importazione</CardTitle>
                  <CardDescription>
                    Verifica i dati prima di procedere con l&apos;importazione
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <ScrollArea className="h-[400px]">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="w-[50px]">Stato</TableHead>
                          <TableHead>Titolo</TableHead>
                          <TableHead>Anno</TableHead>
                          <TableHead>Giocatori</TableHead>
                          <TableHead>Tempo</TableHead>
                          <TableHead>BGG ID</TableHead>
                          <TableHead className="w-[100px]">Azioni</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {csvItems.map(item => (
                          <TableRow key={item.id}>
                            <TableCell>{getStatusIcon(item.status)}</TableCell>
                            <TableCell className="font-medium">
                              {item.data.title}
                              {item.error && (
                                <p className="text-xs text-destructive mt-1">{item.error}</p>
                              )}
                            </TableCell>
                            <TableCell>{item.data.yearPublished}</TableCell>
                            <TableCell>
                              {item.data.minPlayers}-{item.data.maxPlayers}
                            </TableCell>
                            <TableCell>{item.data.playingTimeMinutes} min</TableCell>
                            <TableCell>{item.data.bggId || '-'}</TableCell>
                            <TableCell>
                              {item.status === 'pending' && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => removeCsvItem(item.id)}
                                  disabled={importing}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </ScrollArea>
                </CardContent>
              </Card>
            )}
          </TabsContent>

          {/* BGG Import Tab */}
          <TabsContent value="bgg" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Importa da BoardGameGeek</CardTitle>
                <CardDescription>
                  Inserisci gli ID dei giochi da BoardGameGeek (separati da virgola, spazio o nuova
                  riga). I dati verranno recuperati automaticamente.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <Label htmlFor="bgg-ids">ID BoardGameGeek</Label>
                  <Textarea
                    id="bgg-ids"
                    placeholder="Es: 13, 822, 167791, 174430"
                    value={bggInput}
                    onChange={e => setBggInput(e.target.value)}
                    disabled={importing}
                    className="mt-1"
                    rows={3}
                  />
                </div>

                <div className="flex items-center gap-4">
                  <Button onClick={addBggIds} disabled={importing || !bggInput.trim()}>
                    Aggiungi ID
                  </Button>

                  {bggItems.length > 0 && (
                    <>
                      <Separator orientation="vertical" className="h-8" />
                      <Badge variant="secondary">{bggPendingCount} da recuperare</Badge>
                      <Badge variant="outline">{bggReadyCount} pronti</Badge>
                      <Badge variant="default" className="bg-green-600">
                        {bggSuccessCount} importati
                      </Badge>
                      {bggErrorCount > 0 && (
                        <Badge variant="destructive">{bggErrorCount} errori</Badge>
                      )}
                      <div className="flex-1" />
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={clearBggImport}
                        disabled={importing}
                      >
                        <Trash2 className="h-4 w-4 mr-2" />
                        Cancella
                      </Button>
                    </>
                  )}
                </div>

                {bggItems.length > 0 && (
                  <div className="flex items-center gap-4 pt-4 border-t">
                    <Button
                      variant="outline"
                      onClick={fetchAllBggData}
                      disabled={importing || bggPendingCount === 0}
                    >
                      <Globe className="h-4 w-4 mr-2" />
                      Recupera dati BGG ({bggPendingCount})
                    </Button>
                    <Button onClick={startBggImport} disabled={importing || bggReadyCount === 0}>
                      {importing ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                          Importazione...
                        </>
                      ) : (
                        <>
                          <Upload className="h-4 w-4 mr-2" />
                          Importa {bggReadyCount} giochi
                        </>
                      )}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* BGG Items Table */}
            {bggItems.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle>Giochi da BoardGameGeek</CardTitle>
                  <CardDescription>
                    Recupera i dati prima di procedere con l&apos;importazione
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <ScrollArea className="h-[400px]">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="w-[50px]">Stato</TableHead>
                          <TableHead>BGG ID</TableHead>
                          <TableHead>Titolo</TableHead>
                          <TableHead>Anno</TableHead>
                          <TableHead>Giocatori</TableHead>
                          <TableHead className="w-[100px]">Azioni</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {bggItems.map(item => (
                          <TableRow key={item.bggId}>
                            <TableCell>{getStatusIcon(item.status)}</TableCell>
                            <TableCell className="font-mono">{item.bggId}</TableCell>
                            <TableCell>
                              {item.data?.title || '-'}
                              {item.error && (
                                <p className="text-xs text-destructive mt-1">{item.error}</p>
                              )}
                            </TableCell>
                            <TableCell>{item.data?.yearPublished || '-'}</TableCell>
                            <TableCell>
                              {item.data ? `${item.data.minPlayers}-${item.data.maxPlayers}` : '-'}
                            </TableCell>
                            <TableCell>
                              {(item.status === 'pending' || item.status === 'ready') && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => removeBggItem(item.bggId)}
                                  disabled={importing}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </ScrollArea>
                </CardContent>
              </Card>
            )}

            {/* BGG API Note */}
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Nota sull&apos;API BoardGameGeek</AlertTitle>
              <AlertDescription>
                L&apos;integrazione con l&apos;API di BoardGameGeek recupera dati di base. Potrebbe
                essere necessario completare manualmente alcuni campi come descrizione, immagini e
                regole dopo l&apos;importazione.
              </AlertDescription>
            </Alert>
          </TabsContent>

          {/* JSON Import Tab */}
          <TabsContent value="json" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Importa da File JSON</CardTitle>
                <CardDescription>
                  Carica un file JSON o incolla direttamente il contenuto JSON. Massimo 100 giochi
                  per importazione. Ogni gioco deve avere almeno bggId o title.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Input Source Toggle */}
                <div className="flex gap-2 mb-4">
                  <Button
                    variant={jsonInputSource === 'file' ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setJsonInputSource('file')}
                  >
                    <Upload className="h-4 w-4 mr-2" />
                    Carica File
                  </Button>
                  <Button
                    variant={jsonInputSource === 'textarea' ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setJsonInputSource('textarea')}
                  >
                    Incolla JSON
                  </Button>
                </div>

                {jsonInputSource === 'file' ? (
                  <div className="flex items-center gap-4">
                    <div className="flex-1">
                      <Label htmlFor="json-file">File JSON</Label>
                      <Input
                        ref={jsonFileInputRef}
                        id="json-file"
                        type="file"
                        accept=".json"
                        onChange={handleJsonFileSelect}
                        disabled={importing}
                        className="mt-1"
                      />
                      {jsonFileName && (
                        <p className="text-sm text-muted-foreground mt-1">
                          File selezionato: {jsonFileName}
                        </p>
                      )}
                    </div>
                    <Button variant="outline" onClick={downloadJsonTemplate} className="mt-6">
                      <Download className="h-4 w-4 mr-2" />
                      Scarica Template
                    </Button>
                  </div>
                ) : (
                  <div className="space-y-2">
                    <Label htmlFor="json-textarea">Contenuto JSON</Label>
                    <Textarea
                      id="json-textarea"
                      placeholder='[{"bggId": 13, "title": "Catan", ...}]'
                      value={jsonTextInput}
                      onChange={e => setJsonTextInput(e.target.value)}
                      disabled={importing}
                      className="font-mono text-sm"
                      rows={8}
                    />
                    <div className="flex gap-2">
                      <Button
                        onClick={parseJsonTextarea}
                        disabled={importing || !jsonTextInput.trim()}
                      >
                        Analizza JSON
                      </Button>
                      <Button variant="outline" onClick={downloadJsonTemplate}>
                        <Download className="h-4 w-4 mr-2" />
                        Scarica Template
                      </Button>
                    </div>
                  </div>
                )}

                {jsonParseError && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore</AlertTitle>
                    <AlertDescription>{jsonParseError}</AlertDescription>
                  </Alert>
                )}

                {/* Import Result */}
                {jsonImportResult && (
                  <Alert variant={jsonImportResult.failureCount === 0 ? 'default' : 'destructive'}>
                    <CheckCircle2 className="h-4 w-4" />
                    <AlertTitle>Risultato Importazione</AlertTitle>
                    <AlertDescription>
                      <p>
                        Successi: {jsonImportResult.successCount} | Errori:{' '}
                        {jsonImportResult.failureCount}
                      </p>
                      {jsonImportResult.errors.length > 0 && (
                        <ul className="mt-2 list-disc list-inside text-sm">
                          {jsonImportResult.errors.slice(0, 5).map((err, idx) => (
                            <li key={idx}>{err}</li>
                          ))}
                          {jsonImportResult.errors.length > 5 && (
                            <li>...e altri {jsonImportResult.errors.length - 5} errori</li>
                          )}
                        </ul>
                      )}
                    </AlertDescription>
                  </Alert>
                )}

                {jsonItems.length > 0 && (
                  <div className="flex items-center gap-4">
                    <Badge variant="default" className="bg-green-600">
                      {jsonValidCount} validi
                    </Badge>
                    {jsonInvalidCount > 0 && (
                      <Badge variant="destructive">{jsonInvalidCount} con errori</Badge>
                    )}
                    <div className="flex-1" />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={clearJsonImport}
                      disabled={importing}
                    >
                      <Trash2 className="h-4 w-4 mr-2" />
                      Cancella
                    </Button>
                    <Button onClick={startJsonImport} disabled={importing || jsonValidCount === 0}>
                      {importing ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                          Importazione...
                        </>
                      ) : (
                        <>
                          <Upload className="h-4 w-4 mr-2" />
                          Importa {jsonValidCount} giochi
                        </>
                      )}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* JSON Preview Table */}
            {jsonItems.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle>Anteprima Importazione JSON</CardTitle>
                  <CardDescription>
                    Verifica i dati prima di procedere con l&apos;importazione
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <ScrollArea className="h-[400px]">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="w-[50px]">Stato</TableHead>
                          <TableHead>Titolo</TableHead>
                          <TableHead>BGG ID</TableHead>
                          <TableHead>Anno</TableHead>
                          <TableHead>Giocatori</TableHead>
                          <TableHead>Tempo</TableHead>
                          <TableHead className="w-[100px]">Azioni</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {jsonItems.map(item => (
                          <TableRow key={item.id}>
                            <TableCell>
                              {item.status === 'valid' ? (
                                <CheckCircle2 className="h-4 w-4 text-green-600" />
                              ) : (
                                <XCircle className="h-4 w-4 text-destructive" />
                              )}
                            </TableCell>
                            <TableCell className="font-medium">
                              {item.data.title || '-'}
                              {item.validationError && (
                                <p className="text-xs text-destructive mt-1">
                                  {item.validationError}
                                </p>
                              )}
                            </TableCell>
                            <TableCell className="font-mono">{item.data.bggId || '-'}</TableCell>
                            <TableCell>{item.data.yearPublished || '-'}</TableCell>
                            <TableCell>
                              {item.data.minPlayers && item.data.maxPlayers
                                ? `${item.data.minPlayers}-${item.data.maxPlayers}`
                                : '-'}
                            </TableCell>
                            <TableCell>
                              {item.data.playingTimeMinutes
                                ? `${item.data.playingTimeMinutes} min`
                                : '-'}
                            </TableCell>
                            <TableCell>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => removeJsonItem(item.id)}
                                disabled={importing}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </ScrollArea>
                </CardContent>
              </Card>
            )}

            {/* JSON Format Note */}
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Formato JSON Supportato</AlertTitle>
              <AlertDescription>
                <p className="mb-2">
                  Il file JSON deve contenere un array di oggetti gioco o un oggetto con proprietà
                  &quot;games&quot;:
                </p>
                <pre className="text-xs bg-muted p-2 rounded overflow-x-auto">
                  {`[{"bggId": 13, "title": "Catan", "yearPublished": 1995, ...}]`}
                </pre>
                <p className="mt-2 text-sm">
                  Campi supportati: bggId, title, yearPublished, description, minPlayers, maxPlayers,
                  playingTimeMinutes, minAge, complexityRating, averageRating, imageUrl, thumbnailUrl
                </p>
              </AlertDescription>
            </Alert>
          </TabsContent>
        </Tabs>
      </div>
    </AdminAuthGuard>
  );
}
