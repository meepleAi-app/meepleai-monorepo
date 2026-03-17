/**
 * Agent Creation Sheet - Single-page wizard for creating an AI agent
 * Issue #4776: AgentCreationSheet wizard
 *
 * Responsive layout:
 * - Mobile (0-640px): Bottom sheet, 90vh
 * - Tablet/Desktop: Right drawer, 480px max-width
 *
 * Sections: Game → Knowledge Base → Agent Config → Costs & Slots
 * On submit: calls useCreateAgentFlow → redirect to /chat/{threadId}
 */

'use client';

import { useState, useCallback, useRef } from 'react';

import {
  X,
  Loader2,
  Upload,
  FileText,
  Trash2,
  CheckCircle2,
  AlertCircle,
  BookOpen,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useHasAvailableSlots } from '@/hooks/queries/useAgentSlots';
import { useCreateAgentFlow, type CreateAgentFlowResult } from '@/hooks/queries/useCreateAgentFlow';
import { api } from '@/lib/api';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { CostPreview } from './CostPreview';
import { GameSelector } from './GameSelector';
import { ModelSelector } from './ModelSelector';
import { StrategySelector } from './StrategySelector';
import { TypologySelector } from './TypologySelector';

// --- Types ---

interface UploadedFile {
  id: string;
  name: string;
  size: number;
  uploadProgress: number;
  status: 'uploading' | 'processing' | 'completed' | 'error';
  documentId?: string;
  error?: string;
}

export interface AgentCreationSheetProps {
  isOpen: boolean;
  onClose: () => void;
  /** Pre-selected game ID (e.g., from MeepleCard quick action) */
  initialGameId?: string;
  /** Pre-selected game title for header display */
  initialGameTitle?: string;
  /** Pre-selected document IDs from SearchAgentSheet KB selection */
  initialDocumentIds?: string[];
  /** Human-readable summary of the pre-selected documents */
  initialDocumentSummary?: string;
  /** When true, skip the GameSelector section and show a read-only badge */
  skipGameSelection?: boolean;
  /** When true + initialDocumentIds present, skip PDF upload and show KB summary */
  skipKBUpload?: boolean;
}

// --- Component ---

export function AgentCreationSheet({
  isOpen,
  onClose,
  initialGameId,
  initialGameTitle,
  initialDocumentIds,
  initialDocumentSummary,
  skipGameSelection = false,
  skipKBUpload = false,
}: AgentCreationSheetProps) {
  const router = useRouter();

  // --- Form State ---
  const [selectedGameId, setSelectedGameId] = useState<string | undefined>(initialGameId);
  const [selectedGame, setSelectedGame] = useState<UserLibraryEntry | null>(null);
  const [agentName, setAgentName] = useState('');
  const [selectedTypologyId, setSelectedTypologyId] = useState<string | undefined>();
  const [selectedStrategy, setSelectedStrategy] = useState<string | undefined>();
  const [selectedModelId, setSelectedModelId] = useState<string | undefined>();
  const [addToCollection, setAddToCollection] = useState(false);

  // --- PDF Upload State ---
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // --- Section Collapse State ---
  const [expandedSections, setExpandedSections] = useState({
    game: true,
    knowledge: true,
    config: true,
    costs: true,
  });

  // --- Hooks ---
  const { hasAvailableSlots, slotsData } = useHasAvailableSlots();

  const createAgentFlow = useCreateAgentFlow({
    onSuccess: (result: CreateAgentFlowResult) => {
      // Auto-redirect to chat
      router.push(`/chat/${result.threadId}`);
      handleClose();
    },
  });

  // Derived state
  const gameTitle = selectedGame?.gameTitle ?? initialGameTitle ?? '';
  const isGameInCollection = selectedGame != null;
  const isCreating = createAgentFlow.isPending;

  // Default agent name based on game
  const effectiveAgentName = agentName || (gameTitle ? `Esperto di ${gameTitle}` : '');

  // Validation
  const canCreate = selectedGameId != null && hasAvailableSlots && !isCreating;

  // --- Handlers ---

  const handleClose = useCallback(() => {
    if (isCreating) return;
    onClose();
    // Reset form on close
    setSelectedGameId(initialGameId);
    setSelectedGame(null);
    setAgentName('');
    setSelectedTypologyId(undefined);
    setSelectedStrategy(undefined);
    setSelectedModelId(undefined);
    setAddToCollection(false);
    setUploadedFiles([]);
  }, [isCreating, onClose, initialGameId]);

  const handleGameChange = useCallback((gameId: string, game: UserLibraryEntry | null) => {
    setSelectedGameId(gameId);
    setSelectedGame(game);
    setAddToCollection(game == null);
    // Reset agent name when game changes
    setAgentName('');
  }, []);

  const handleCreate = useCallback(() => {
    if (!selectedGameId || !canCreate) return;

    createAgentFlow.mutate({
      gameId: selectedGameId,
      addToCollection: addToCollection || !isGameInCollection,
      agentType: selectedTypologyId ?? 'default',
      agentName: effectiveAgentName || undefined,
      strategyName: selectedStrategy,
      documentIds: initialDocumentIds,
    });
  }, [
    selectedGameId,
    canCreate,
    createAgentFlow,
    addToCollection,
    isGameInCollection,
    selectedTypologyId,
    effectiveAgentName,
    selectedStrategy,
    initialDocumentIds,
  ]);

  const toggleSection = useCallback((section: keyof typeof expandedSections) => {
    setExpandedSections(prev => ({ ...prev, [section]: !prev[section] }));
  }, []);

  // --- PDF Upload Handlers ---

  const handleFileUpload = useCallback(
    async (files: FileList | File[]) => {
      if (!selectedGameId) return;

      const fileArray = Array.from(files).filter(f => f.type === 'application/pdf');
      if (fileArray.length === 0) return;

      for (const file of fileArray) {
        const fileId = `${Date.now()}-${file.name}`;
        const newFile: UploadedFile = {
          id: fileId,
          name: file.name,
          size: file.size,
          uploadProgress: 0,
          status: 'uploading',
        };

        setUploadedFiles(prev => [...prev, newFile]);

        try {
          const result = await api.pdf.uploadPdf(selectedGameId, file, percent => {
            setUploadedFiles(prev =>
              prev.map(f =>
                f.id === fileId
                  ? {
                      ...f,
                      uploadProgress: percent,
                      status: percent < 100 ? 'uploading' : 'processing',
                    }
                  : f
              )
            );
          });

          setUploadedFiles(prev =>
            prev.map(f =>
              f.id === fileId
                ? { ...f, documentId: result.documentId, status: 'completed', uploadProgress: 100 }
                : f
            )
          );
        } catch (err) {
          setUploadedFiles(prev =>
            prev.map(f =>
              f.id === fileId
                ? {
                    ...f,
                    status: 'error',
                    error: err instanceof Error ? err.message : 'Upload failed',
                  }
                : f
            )
          );
        }
      }
    },
    [selectedGameId]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      if (e.dataTransfer.files.length > 0) {
        handleFileUpload(e.dataTransfer.files);
      }
    },
    [handleFileUpload]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const removeFile = useCallback((fileId: string) => {
    setUploadedFiles(prev => prev.filter(f => f.id !== fileId));
  }, []);

  // --- Render ---

  return (
    <Sheet open={isOpen} onOpenChange={open => !open && handleClose()}>
      <SheetContent side="right" className="w-full sm:max-w-[480px] flex flex-col p-0">
        {/* Header */}
        <SheetHeader className="border-b px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <SheetTitle className="text-lg font-semibold">Crea Agente</SheetTitle>
              {gameTitle && <p className="text-sm text-muted-foreground mt-0.5">{gameTitle}</p>}
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={handleClose}
              disabled={isCreating}
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </SheetHeader>

        {/* Content - Scrollable */}
        <div className="flex-1 overflow-y-auto">
          <div className="px-6 py-4 space-y-1">
            {/* Section 1: Game */}
            <SectionHeader
              title="Gioco"
              icon="🎲"
              expanded={expandedSections.game}
              onToggle={() => toggleSection('game')}
            />
            {expandedSections.game && (
              <div className="pb-4 space-y-3">
                {skipGameSelection ? (
                  /* Read-only badge when game is pre-selected from wizard */
                  <div className="flex items-center gap-2 p-2 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
                    <CheckCircle2 className="h-4 w-4 text-blue-600" />
                    <span className="text-sm font-medium text-blue-700 dark:text-blue-300 truncate">
                      {gameTitle}
                    </span>
                  </div>
                ) : (
                  <>
                    <GameSelector
                      value={selectedGameId}
                      onChange={handleGameChange}
                      disabled={isCreating}
                    />
                    {selectedGameId && !isGameInCollection && (
                      <div className="flex items-center gap-2 p-2 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg">
                        <BookOpen className="h-4 w-4 text-amber-600" />
                        <span className="text-xs text-amber-700 dark:text-amber-300">
                          Il gioco verrà aggiunto alla tua collezione
                        </span>
                      </div>
                    )}
                    {selectedGameId && isGameInCollection && (
                      <div className="flex items-center gap-2 p-2 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
                        <CheckCircle2 className="h-4 w-4 text-green-600" />
                        <span className="text-xs text-green-700 dark:text-green-300">
                          In collezione
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {/* Section 2: Knowledge Base */}
            <SectionHeader
              title="Knowledge Base"
              icon="📚"
              expanded={expandedSections.knowledge}
              onToggle={() => toggleSection('knowledge')}
            />
            {expandedSections.knowledge && (
              <div className="pb-4 space-y-3">
                {skipKBUpload && initialDocumentIds && initialDocumentIds.length > 0 ? (
                  /* Read-only KB summary when pre-selected from SearchAgentSheet */
                  <div className="flex items-start gap-2 p-3 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
                    <CheckCircle2 className="h-4 w-4 text-green-600 shrink-0 mt-0.5" />
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-green-700 dark:text-green-300">
                        {initialDocumentIds.length} documento
                        {initialDocumentIds.length !== 1 ? 'i' : ''} selezionato
                        {initialDocumentIds.length !== 1 ? 'i' : ''}
                      </p>
                      {initialDocumentSummary && (
                        <p className="text-xs text-green-600 dark:text-green-400 mt-0.5">
                          {initialDocumentSummary}
                        </p>
                      )}
                    </div>
                  </div>
                ) : (
                  <>
                    {/* Drop Zone */}
                    <div
                      className={`
                    relative border-2 border-dashed rounded-lg p-6 text-center transition-colors cursor-pointer
                    ${
                      isDragOver
                        ? 'border-primary bg-primary/5'
                        : 'border-muted-foreground/25 hover:border-muted-foreground/50'
                    }
                    ${!selectedGameId ? 'opacity-50 pointer-events-none' : ''}
                  `}
                      onDrop={handleDrop}
                      onDragOver={handleDragOver}
                      onDragLeave={handleDragLeave}
                      onClick={() => selectedGameId && fileInputRef.current?.click()}
                      role="button"
                      tabIndex={0}
                      aria-label="Upload PDF files"
                    >
                      <input
                        ref={fileInputRef}
                        type="file"
                        accept=".pdf"
                        multiple
                        className="hidden"
                        onChange={e => e.target.files && handleFileUpload(e.target.files)}
                      />
                      <Upload className="h-8 w-8 mx-auto text-muted-foreground/50 mb-2" />
                      <p className="text-sm font-medium">Trascina PDF qui o clicca per caricare</p>
                      <p className="text-xs text-muted-foreground mt-1">
                        Regolamenti, FAQ, guide — max 50MB per file
                      </p>
                    </div>

                    {!selectedGameId && (
                      <p className="text-xs text-muted-foreground">
                        Seleziona prima un gioco per caricare documenti.
                      </p>
                    )}

                    {/* Uploaded Files List */}
                    {uploadedFiles.length > 0 && (
                      <div className="space-y-2">
                        {uploadedFiles.map(file => (
                          <div
                            key={file.id}
                            className="flex items-center gap-3 p-2 bg-muted/50 rounded-lg"
                          >
                            <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                            <div className="flex-1 min-w-0">
                              <p className="text-sm font-medium truncate">{file.name}</p>
                              <div className="flex items-center gap-2">
                                {file.status === 'uploading' && (
                                  <>
                                    <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden">
                                      <div
                                        className="h-full bg-primary rounded-full transition-all"
                                        style={{ width: `${file.uploadProgress}%` }}
                                      />
                                    </div>
                                    <span className="text-xs text-muted-foreground">
                                      {file.uploadProgress}%
                                    </span>
                                  </>
                                )}
                                {file.status === 'processing' && (
                                  <span className="text-xs text-amber-600 flex items-center gap-1">
                                    <Loader2 className="h-3 w-3 animate-spin" />
                                    Elaborazione...
                                  </span>
                                )}
                                {file.status === 'completed' && (
                                  <span className="text-xs text-green-600 flex items-center gap-1">
                                    <CheckCircle2 className="h-3 w-3" />
                                    Completato
                                  </span>
                                )}
                                {file.status === 'error' && (
                                  <span className="text-xs text-red-600 flex items-center gap-1">
                                    <AlertCircle className="h-3 w-3" />
                                    {file.error || 'Errore'}
                                  </span>
                                )}
                              </div>
                            </div>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-6 w-6 shrink-0"
                              onClick={() => removeFile(file.id)}
                              aria-label={`Rimuovi ${file.name}`}
                            >
                              <Trash2 className="h-3 w-3" />
                            </Button>
                          </div>
                        ))}
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {/* Section 3: Agent Config */}
            <SectionHeader
              title="Configura Agente"
              icon="🤖"
              expanded={expandedSections.config}
              onToggle={() => toggleSection('config')}
            />
            {expandedSections.config && (
              <div className="pb-4 space-y-4">
                {/* Agent Name */}
                <div className="space-y-2">
                  <label htmlFor="agent-name" className="block text-sm font-medium">
                    Nome Agente
                  </label>
                  <Input
                    id="agent-name"
                    value={agentName}
                    onChange={e => setAgentName(e.target.value)}
                    placeholder={gameTitle ? `Esperto di ${gameTitle}` : 'Nome agente...'}
                    disabled={isCreating}
                    maxLength={100}
                  />
                  <p className="text-xs text-muted-foreground">
                    Lascia vuoto per usare il nome predefinito.
                  </p>
                </div>

                {/* Typology */}
                <TypologySelector
                  value={selectedTypologyId}
                  onChange={setSelectedTypologyId}
                  disabled={isCreating}
                />

                {/* Strategy */}
                <StrategySelector
                  value={selectedStrategy}
                  onChange={setSelectedStrategy}
                  disabled={isCreating}
                />

                {/* Model */}
                <ModelSelector
                  value={selectedModelId}
                  onChange={modelId => setSelectedModelId(modelId)}
                  disabled={isCreating}
                />
              </div>
            )}

            {/* Section 4: Costs & Slots */}
            <SectionHeader
              title="Costi & Slot"
              icon="💰"
              expanded={expandedSections.costs}
              onToggle={() => toggleSection('costs')}
            />
            {expandedSections.costs && (
              <div className="pb-4 space-y-4">
                {/* Slot Usage */}
                {slotsData && (
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-sm">
                      <span className="font-medium">Agent Slot</span>
                      <span className="text-muted-foreground">
                        {slotsData.used} / {slotsData.total} usati
                      </span>
                    </div>
                    <div className="h-2 bg-muted rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all ${
                          slotsData.available === 0
                            ? 'bg-red-500'
                            : slotsData.available <= 1
                              ? 'bg-amber-500'
                              : 'bg-green-500'
                        }`}
                        style={{
                          width: `${Math.round((slotsData.used / slotsData.total) * 100)}%`,
                        }}
                      />
                    </div>
                    {slotsData.available === 0 && (
                      <div className="flex items-center gap-2 p-2 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
                        <AlertCircle className="h-4 w-4 text-red-500" />
                        <span className="text-xs text-red-700 dark:text-red-300">
                          Nessuno slot disponibile. Elimina un agente o effettua l&apos;upgrade.
                        </span>
                      </div>
                    )}
                    {slotsData.available === 1 && (
                      <p className="text-xs text-amber-600">Ultimo slot disponibile!</p>
                    )}
                  </div>
                )}

                {/* Cost Preview */}
                <CostPreview typologyId={selectedTypologyId} />
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <SheetFooter className="border-t px-6 py-4">
          <div className="w-full flex gap-3 justify-end">
            <Button variant="outline" onClick={handleClose} disabled={isCreating}>
              Annulla
            </Button>
            <Button onClick={handleCreate} disabled={!canCreate}>
              {isCreating ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin mr-2" />
                  Creazione...
                </>
              ) : (
                'Crea e Inizia Chat'
              )}
            </Button>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

// --- Section Header Sub-Component ---

function SectionHeader({
  title,
  icon,
  expanded,
  onToggle,
}: {
  title: string;
  icon: string;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      className="w-full flex items-center justify-between py-2 group"
      onClick={onToggle}
      aria-expanded={expanded}
    >
      <div className="flex items-center gap-2">
        <span className="text-base">{icon}</span>
        <h3 className="text-sm font-semibold">{title}</h3>
      </div>
      {expanded ? (
        <ChevronUp className="h-4 w-4 text-muted-foreground group-hover:text-foreground transition-colors" />
      ) : (
        <ChevronDown className="h-4 w-4 text-muted-foreground group-hover:text-foreground transition-colors" />
      )}
    </button>
  );
}
