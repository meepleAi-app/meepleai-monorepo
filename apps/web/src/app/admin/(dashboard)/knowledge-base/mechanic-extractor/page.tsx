'use client';

import { useEffect, useState, useCallback } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  BookOpenIcon,
  SparklesIcon,
  CheckIcon,
  XIcon,
  SaveIcon,
  Loader2Icon,
  FileTextIcon,
  ChevronRightIcon,
  ShieldCheckIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type {
  MechanicDraftDto,
  AiAssistResultDto,
} from '@/lib/api/schemas/mechanic-extractor.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });
const gamesClient = createSharedGamesClient({ httpClient });

const SECTIONS = [
  { id: 'summary', label: 'Summary', icon: FileTextIcon },
  { id: 'mechanics', label: 'Mechanics', icon: BookOpenIcon },
  { id: 'victory', label: 'Victory', icon: ShieldCheckIcon },
  { id: 'resources', label: 'Resources', icon: BookOpenIcon },
  { id: 'phases', label: 'Phases', icon: ChevronRightIcon },
  { id: 'questions', label: 'FAQ', icon: BookOpenIcon },
] as const;

type SectionId = (typeof SECTIONS)[number]['id'];

function _getNotesKey(section: SectionId): keyof MechanicDraftDto {
  return `${section}Notes` as keyof MechanicDraftDto;
}

function getDraftKey(section: SectionId): keyof MechanicDraftDto {
  return `${section}Draft` as keyof MechanicDraftDto;
}

export default function MechanicExtractorPage() {
  const queryClient = useQueryClient();

  // Game+PDF selection state
  const [selectedGameId, setSelectedGameId] = useState('');
  const [selectedPdfId, setSelectedPdfId] = useState('');
  const [gameTitle, setGameTitle] = useState('');

  // Editor state
  const [activeSection, setActiveSection] = useState<SectionId>('summary');
  const [notes, setNotes] = useState<Record<SectionId, string>>({
    summary: '',
    mechanics: '',
    victory: '',
    resources: '',
    phases: '',
    questions: '',
  });
  const [aiResult, setAiResult] = useState<AiAssistResultDto | null>(null);
  const [autoSaveTimer, setAutoSaveTimer] = useState<ReturnType<typeof setTimeout> | null>(null);

  // PDF viewer ref
  const pdfUrl = selectedPdfId ? `/api/v1/documents/${selectedPdfId}/download` : '';

  // Fetch shared games for selection
  const { data: gamesData } = useQuery({
    queryKey: ['shared-games', 'all'],
    queryFn: () => gamesClient.getAll({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  });

  // Fetch PDFs for selected game
  const { data: pdfsData } = useQuery({
    queryKey: ['admin', 'pdfs', { gameId: selectedGameId }],
    queryFn: () =>
      adminClient.getAllPdfs({
        gameId: selectedGameId,
        status: 'Completed',
        page: 1,
        pageSize: 50,
      }),
    enabled: !!selectedGameId,
    staleTime: 30_000,
  });

  // Load existing draft
  const { data: existingDraft, isLoading: isDraftLoading } = useQuery({
    queryKey: ['admin', 'mechanic-draft', selectedGameId, selectedPdfId],
    queryFn: () => adminClient.getMechanicDraft(selectedGameId, selectedPdfId),
    enabled: !!selectedGameId && !!selectedPdfId,
  });

  // Populate notes from existing draft
  useEffect(() => {
    if (existingDraft) {
      setNotes({
        summary: existingDraft.summaryNotes,
        mechanics: existingDraft.mechanicsNotes,
        victory: existingDraft.victoryNotes,
        resources: existingDraft.resourcesNotes,
        phases: existingDraft.phasesNotes,
        questions: existingDraft.questionsNotes,
      });
      setGameTitle(existingDraft.gameTitle);
    }
  }, [existingDraft]);

  // Save draft mutation
  const saveMutation = useMutation({
    mutationFn: () =>
      adminClient.saveMechanicDraft({
        sharedGameId: selectedGameId,
        pdfDocumentId: selectedPdfId,
        gameTitle,
        userId: '', // Will be filled by backend from session
        summaryNotes: notes.summary,
        mechanicsNotes: notes.mechanics,
        victoryNotes: notes.victory,
        resourcesNotes: notes.resources,
        phasesNotes: notes.phases,
        questionsNotes: notes.questions,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['admin', 'mechanic-draft', selectedGameId, selectedPdfId],
      });
    },
  });

  // AI assist mutation
  const aiAssistMutation = useMutation({
    mutationFn: (section: SectionId) =>
      adminClient.aiAssistMechanicDraft({
        draftId: existingDraft?.id ?? '',
        section,
        humanNotes: notes[section],
        gameTitle,
      }),
    onSuccess: result => {
      setAiResult(result);
    },
  });

  // Accept draft mutation
  const acceptMutation = useMutation({
    mutationFn: ({ section, draft }: { section: string; draft: string }) =>
      adminClient.acceptMechanicDraft({
        draftId: existingDraft?.id ?? '',
        section,
        acceptedDraft: draft,
      }),
    onSuccess: () => {
      setAiResult(null);
      queryClient.invalidateQueries({
        queryKey: ['admin', 'mechanic-draft', selectedGameId, selectedPdfId],
      });
    },
  });

  // Finalize mutation
  const finalizeMutation = useMutation({
    mutationFn: () =>
      adminClient.finalizeMechanicAnalysis({
        draftId: existingDraft?.id ?? '',
        userId: '',
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['admin', 'mechanic-draft'],
      });
    },
  });

  // Auto-save on notes change (debounced)
  const handleNotesChange = useCallback(
    (section: SectionId, value: string) => {
      setNotes(prev => ({ ...prev, [section]: value }));

      if (autoSaveTimer) clearTimeout(autoSaveTimer);
      const timer = setTimeout(() => {
        if (selectedGameId && selectedPdfId) {
          saveMutation.mutate();
        }
      }, 2000);
      setAutoSaveTimer(timer);
    },
    [selectedGameId, selectedPdfId, autoSaveTimer, saveMutation]
  );

  // Handle game selection
  const handleGameSelect = (gameId: string) => {
    setSelectedGameId(gameId);
    setSelectedPdfId('');
    const game = gamesData?.items?.find((g: { id: string; title: string }) => g.id === gameId);
    if (game) setGameTitle(game.title);
  };

  const canRequestAi = !!existingDraft && notes[activeSection].length >= 10;
  const canFinalize =
    existingDraft &&
    existingDraft.summaryDraft &&
    existingDraft.mechanicsDraft &&
    existingDraft.status !== 'Activated';

  const currentDraft = existingDraft ? (existingDraft[getDraftKey(activeSection)] as string) : '';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Mechanic Extractor
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Extract game mechanics from rulebook PDFs using a copyright-compliant human+AI workflow
        </p>
        <Badge
          variant="outline"
          className="mt-2 border-amber-300 bg-amber-50 text-amber-800 dark:bg-amber-950/30 dark:text-amber-300"
        >
          <ShieldCheckIcon className="mr-1 h-3 w-3" />
          Variant C: AI reads notes only, never the PDF
        </Badge>
      </div>

      {/* Game + PDF Selection */}
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
        <CardContent className="pt-6">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-foreground">
                Select Game
              </label>
              <Select value={selectedGameId} onValueChange={handleGameSelect}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a game..." />
                </SelectTrigger>
                <SelectContent>
                  {gamesData?.items?.map((game: { id: string; title: string }) => (
                    <SelectItem key={game.id} value={game.id}>
                      {game.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-foreground">Select PDF</label>
              <Select
                value={selectedPdfId}
                onValueChange={setSelectedPdfId}
                disabled={!selectedGameId}
              >
                <SelectTrigger>
                  <SelectValue
                    placeholder={selectedGameId ? 'Choose a PDF...' : 'Select a game first'}
                  />
                </SelectTrigger>
                <SelectContent>
                  {pdfsData?.items?.map((pdf: { id: string; fileName: string }) => (
                    <SelectItem key={pdf.id} value={pdf.id}>
                      {pdf.fileName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-end gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => saveMutation.mutate()}
                disabled={!selectedGameId || !selectedPdfId || saveMutation.isPending}
              >
                {saveMutation.isPending ? (
                  <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <SaveIcon className="mr-1 h-4 w-4" />
                )}
                Save
              </Button>
              {existingDraft && (
                <Badge variant="outline" className="text-xs">
                  {existingDraft.status}
                </Badge>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Main Editor Area — Split Panel */}
      {selectedGameId && selectedPdfId && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {/* Left: PDF Viewer */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
            <CardContent className="pt-6">
              <h2 className="mb-3 text-lg font-medium font-quicksand">Rulebook PDF</h2>
              {pdfUrl ? (
                <div
                  className="rounded-lg border border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 overflow-hidden"
                  style={{ height: '600px' }}
                >
                  <iframe src={pdfUrl} className="h-full w-full" title="Rulebook PDF" />
                </div>
              ) : (
                <div className="flex h-[600px] items-center justify-center rounded-lg border border-dashed border-slate-300 dark:border-zinc-600">
                  <p className="text-muted-foreground">Select a PDF to view</p>
                </div>
              )}
              <p className="mt-2 text-xs text-muted-foreground">
                Read the PDF and write your notes in the editor. The AI will only see your notes.
              </p>
            </CardContent>
          </Card>

          {/* Right: Mechanic Editor */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60">
            <CardContent className="pt-6">
              {isDraftLoading ? (
                <div className="space-y-3">
                  <Skeleton className="h-8 w-48" />
                  <Skeleton className="h-[500px]" />
                </div>
              ) : (
                <>
                  {/* Section Tabs */}
                  <div className="mb-4 flex flex-wrap gap-1">
                    {SECTIONS.map(section => {
                      const Icon = section.icon;
                      const hasNotes = notes[section.id].length > 0;
                      const hasDraft = existingDraft
                        ? !!(existingDraft[getDraftKey(section.id)] as string)
                        : false;

                      return (
                        <button
                          key={section.id}
                          onClick={() => {
                            setActiveSection(section.id);
                            setAiResult(null);
                          }}
                          className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                            activeSection === section.id
                              ? 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300'
                              : 'text-muted-foreground hover:bg-slate-100 dark:hover:bg-zinc-700'
                          }`}
                        >
                          <Icon className="h-3.5 w-3.5" />
                          {section.label}
                          {hasDraft && <CheckIcon className="h-3 w-3 text-green-600" />}
                          {hasNotes && !hasDraft && (
                            <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                          )}
                        </button>
                      );
                    })}
                  </div>

                  {/* Notes Textarea */}
                  <div className="mb-3">
                    <label className="mb-1.5 block text-sm font-medium text-foreground">
                      Your Notes — {SECTIONS.find(s => s.id === activeSection)?.label}
                    </label>
                    <textarea
                      value={notes[activeSection]}
                      onChange={e => handleNotesChange(activeSection, e.target.value)}
                      placeholder={`Write your notes about ${activeSection} here. Describe what you read in the rulebook using your own words...`}
                      className="h-40 w-full resize-y rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-amber-400 focus:outline-none focus:ring-1 focus:ring-amber-400"
                    />
                    <p className="mt-1 text-xs text-muted-foreground">
                      {notes[activeSection].length} characters
                      {notes[activeSection].length < 10 && ' (min 10 for AI assist)'}
                    </p>
                  </div>

                  {/* AI Assist Button */}
                  <div className="mb-3 flex items-center gap-2">
                    <Button
                      size="sm"
                      onClick={() => aiAssistMutation.mutate(activeSection)}
                      disabled={!canRequestAi || aiAssistMutation.isPending}
                      className="bg-amber-500 hover:bg-amber-600 text-white"
                    >
                      {aiAssistMutation.isPending ? (
                        <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
                      ) : (
                        <SparklesIcon className="mr-1 h-4 w-4" />
                      )}
                      AI Assist
                    </Button>
                    {!existingDraft && (
                      <span className="text-xs text-muted-foreground">
                        Save draft first to enable AI assist
                      </span>
                    )}
                  </div>

                  {/* AI Result Preview */}
                  {aiResult && aiResult.section === activeSection && (
                    <div className="mb-3 rounded-lg border-2 border-amber-300 bg-amber-50/50 p-3 dark:border-amber-700 dark:bg-amber-950/20">
                      <div className="mb-2 flex items-center justify-between">
                        <span className="text-sm font-medium text-amber-800 dark:text-amber-300">
                          AI-Generated Draft
                        </span>
                        <div className="flex gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() =>
                              acceptMutation.mutate({
                                section: activeSection,
                                draft: aiResult.generatedDraft,
                              })
                            }
                            disabled={acceptMutation.isPending}
                            className="border-green-300 text-green-700 hover:bg-green-50"
                          >
                            <CheckIcon className="mr-1 h-3 w-3" />
                            Accept
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setAiResult(null)}
                            className="border-red-300 text-red-700 hover:bg-red-50"
                          >
                            <XIcon className="mr-1 h-3 w-3" />
                            Reject
                          </Button>
                        </div>
                      </div>
                      <pre className="max-h-48 overflow-y-auto whitespace-pre-wrap text-sm text-foreground">
                        {aiResult.generatedDraft}
                      </pre>
                    </div>
                  )}

                  {/* Accepted Draft Display */}
                  {currentDraft && (
                    <div className="rounded-lg border border-green-200 bg-green-50/50 p-3 dark:border-green-800 dark:bg-green-950/20">
                      <div className="mb-2 flex items-center gap-2">
                        <CheckIcon className="h-4 w-4 text-green-600" />
                        <span className="text-sm font-medium text-green-800 dark:text-green-300">
                          Accepted Draft
                        </span>
                      </div>
                      <pre className="max-h-32 overflow-y-auto whitespace-pre-wrap text-sm text-foreground">
                        {currentDraft}
                      </pre>
                    </div>
                  )}

                  {/* Finalize Button */}
                  {canFinalize && (
                    <div className="mt-4 border-t border-slate-200 pt-4 dark:border-zinc-700">
                      <Button
                        onClick={() => finalizeMutation.mutate()}
                        disabled={finalizeMutation.isPending}
                        className="w-full bg-green-600 hover:bg-green-700 text-white"
                      >
                        {finalizeMutation.isPending ? (
                          <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <ShieldCheckIcon className="mr-2 h-4 w-4" />
                        )}
                        Activate in Knowledge Base
                      </Button>
                      <p className="mt-1 text-center text-xs text-muted-foreground">
                        Creates a RulebookAnalysis entry with GenerationSource.Manual
                      </p>
                    </div>
                  )}

                  {existingDraft?.status === 'Activated' && (
                    <div className="mt-4 rounded-lg border border-green-300 bg-green-50 p-3 text-center dark:border-green-800 dark:bg-green-950/30">
                      <CheckIcon className="mx-auto h-6 w-6 text-green-600" />
                      <p className="mt-1 text-sm font-medium text-green-800 dark:text-green-300">
                        This analysis has been activated in the Knowledge Base
                      </p>
                    </div>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
