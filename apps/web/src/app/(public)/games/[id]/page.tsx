/**
 * Game Detail Page - 4 Tabs (Issue #855)
 *
 * Full-page view with:
 * - Overview: Game info + BGG details (reuses GameDetailModal logic)
 * - Rules: Placeholder for GetRuleSpecsQuery (Issue #2027)
 * - Sessions: Game session history (uses api.sessions.getHistory)
 * - Notes: User notes (localStorage-based)
 *
 * DDD Integration:
 * - Frontend only (backend queries in separate issue)
 * - Consumes existing Game + Session APIs
 * - Designed for future GameManagement.Application queries
 */

'use client';

import Image from 'next/image';
import { PdfUploadForm } from '@/components/pdf/PdfUploadForm';
import { PdfViewerModal } from '@/components/pdf/PdfViewerModal';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { api, BggGameDetails, Game, GameSessionDto, PdfDocumentDto, RuleSpec } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { categorizeError } from '@/lib/errorUtils';
import { logger } from '@/lib/logger';
import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  Calendar,
  Clock,
  ExternalLink,
  PlayCircle,
  Star,
  StickyNote,
  TrendingUp,
  Users,
} from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

// LocalStorage key for notes
const NOTES_STORAGE_KEY = 'meepleai_game_notes';

interface GameNotes {
  [gameId: string]: string;
}

export default function GameDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string | undefined;
  const gameId = typeof id === 'string' ? id : null;

  // State
  const [game, setGame] = useState<Game | null>(null);
  const [bggDetails, setBggDetails] = useState<BggGameDetails | null>(null);
  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [rules, setRules] = useState<RuleSpec[]>([]);
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(true);
  const [bggLoading, setBggLoading] = useState(false);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [rulesLoading, setRulesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bggError, setBggError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [rulesPagination, setRulesPagination] = useState<Record<string, number>>({});
  const [pdfs, setPdfs] = useState<PdfDocumentDto[]>([]);
  const [pdfsLoading, setPdfsLoading] = useState(false);
  const [showUploadForm, setShowUploadForm] = useState(false);
  const [selectedPdf, setSelectedPdf] = useState<{ url: string; name: string } | null>(null);
  const [pdfViewerOpen, setPdfViewerOpen] = useState(false);

  // Load game data
  useEffect(() => {
    if (!gameId) return;

    const loadGame = async () => {
      setLoading(true);
      setError(null);
      try {
        const gameData = await api.games.getById(gameId);
        setGame(gameData);
      } catch (err) {
        setError('Failed to load game');
        logger.error(
          'Failed to load game details',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadGame', { gameId, operation: 'fetch_game' })
        );
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [gameId]);

  // Load BGG details when game loads
  useEffect(() => {
    if (!game?.bggId) {
      setBggDetails(null);
      return;
    }

    const bggId = game.bggId;

    const loadBggDetails = async () => {
      setBggLoading(true);
      setBggError(null);
      try {
        const details = await api.bgg.getGameDetails(bggId);
        setBggDetails(details);
      } catch (err) {
        setBggError('Failed to load BGG details');
        logger.error(
          'Failed to load BGG details',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadBggDetails', {
            bggId,
            operation: 'fetch_bgg_details',
          })
        );
      } finally {
        setBggLoading(false);
      }
    };

    loadBggDetails();
  }, [game]);

  // Load sessions when tab is activated
  useEffect(() => {
    if (!gameId || activeTab !== 'sessions') return;

    const loadSessions = async () => {
      setSessionsLoading(true);
      try {
        const response = await api.sessions.getHistory({
          gameId,
          limit: 50,
        });
        setSessions(response.sessions);
      } catch (err) {
        logger.error(
          'Failed to load game sessions',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadSessions', {
            gameId,
            operation: 'fetch_sessions',
          })
        );
        setSessions([]);
      } finally {
        setSessionsLoading(false);
      }
    };

    loadSessions();
  }, [gameId, activeTab]);

  // Load rules when tab is activated (Issue #2027)
  useEffect(() => {
    if (!gameId || activeTab !== 'rules') return;

    const loadRules = async () => {
      setRulesLoading(true);
      try {
        const rulesData = await api.games.getRules(gameId);
        setRules(rulesData);
      } catch (err) {
        logger.error(
          'Failed to load game rules',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadRules', {
            gameId,
            operation: 'fetch_rules',
          })
        );
        setRules([]);
      } finally {
        setRulesLoading(false);
      }
    };

    loadRules();
  }, [gameId, activeTab]);

  // Load PDFs when Rules tab is activated
  useEffect(() => {
    if (!gameId || activeTab !== 'rules') return;

    const loadPdfs = async () => {
      setPdfsLoading(true);
      try {
        const pdfsData = await api.games.getDocuments(gameId);
        setPdfs(pdfsData);
      } catch (err) {
        logger.error(
          'Failed to load game PDFs',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadPdfs', {
            gameId,
            operation: 'fetch_pdfs',
          })
        );
        setPdfs([]);
      } finally {
        setPdfsLoading(false);
      }
    };

    loadPdfs();
  }, [gameId, activeTab]);

  // Load notes from localStorage
  useEffect(() => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      if (savedNotes) {
        const allNotes: GameNotes = JSON.parse(savedNotes);
        // eslint-disable-next-line security/detect-object-injection
        setNotes(allNotes[gameId] || '');
      }
    } catch (err) {
      logger.error(
        'Failed to load game notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'loadNotes', { gameId, operation: 'load_notes' })
      );
    }
  }, [gameId]);

  // Save notes to localStorage
  const handleSaveNotes = () => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      const allNotes: GameNotes = savedNotes ? JSON.parse(savedNotes) : {};
      // eslint-disable-next-line security/detect-object-injection
      allNotes[gameId] = notes;
      localStorage.setItem(NOTES_STORAGE_KEY, JSON.stringify(allNotes));
      alert('Notes saved successfully!');
    } catch (err) {
      logger.error(
        'Failed to save game notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'handleSaveNotes', { gameId, operation: 'save_notes' })
      );
      alert('Failed to save notes');
    }
  };

  // Handle PDF upload success
  const handleUploadSuccess = async (documentId: string) => {
    setShowUploadForm(false);
    // Reload PDFs to show the newly uploaded one
    if (gameId) {
      try {
        const pdfsData = await api.games.getDocuments(gameId);
        setPdfs(pdfsData);
      } catch (err) {
        logger.error(
          'Failed to reload PDFs after upload',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'handleUploadSuccess', { gameId, documentId })
        );
      }
    }
  };

  // Handle PDF upload error
  const handleUploadError = (error: ReturnType<typeof categorizeError>) => {
    logger.error(
      'PDF upload failed',
      new Error(error.message),
      createErrorContext('GameDetailPage', 'handleUploadError', { gameId, error })
    );
    alert(`Upload failed: ${error.message}`);
  };

  // Handle PDF view
  const handleViewPdf = (pdf: PdfDocumentDto) => {
    const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    setSelectedPdf({
      url: `${API_BASE}/api/v1/pdfs/${pdf.id}/download`,
      name: pdf.fileName,
    });
    setPdfViewerOpen(true);
  };

  if (loading) {
    return (
      <div className="container mx-auto p-6 max-w-6xl">
        <Skeleton className="h-8 w-64 mb-6" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (error || !game) {
    return (
      <div className="container mx-auto p-6 max-w-6xl">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error || 'Game not found'}</AlertDescription>
        </Alert>
        <Button asChild className="mt-4">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Games
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-6 max-w-6xl">
      {/* Header */}
      <div className="mb-6">
        <Button asChild variant="ghost" className="mb-4">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Games
          </Link>
        </Button>

        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold mb-2">{game.title}</h1>
            {game.publisher && <p className="text-muted-foreground">{game.publisher}</p>}
          </div>
          {game.bggId && <Badge variant="secondary">BGG #{game.bggId}</Badge>}
        </div>

        {/* Basic Info */}
        <div className="flex flex-wrap gap-4 text-sm mt-4">
          {(game.minPlayers !== null || game.maxPlayers !== null) && (
            <div className="flex items-center gap-2">
              <Users className="h-4 w-4 text-muted-foreground" />
              <span>
                {game.minPlayers === game.maxPlayers
                  ? `${game.minPlayers} players`
                  : `${game.minPlayers || '?'}-${game.maxPlayers || '?'} players`}
              </span>
            </div>
          )}

          {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
            <div className="flex items-center gap-2">
              <Clock className="h-4 w-4 text-muted-foreground" />
              <span>
                {game.minPlayTimeMinutes === game.maxPlayTimeMinutes
                  ? `${game.minPlayTimeMinutes} min`
                  : `${game.minPlayTimeMinutes || '?'}-${game.maxPlayTimeMinutes || '?'} min`}
              </span>
            </div>
          )}

          {game.yearPublished && (
            <div className="flex items-center gap-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <span>{game.yearPublished}</span>
            </div>
          )}
        </div>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="overview">
            <Star className="mr-2 h-4 w-4" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="rules">
            <BookOpen className="mr-2 h-4 w-4" />
            Rules
          </TabsTrigger>
          <TabsTrigger value="sessions">
            <PlayCircle className="mr-2 h-4 w-4" />
            Sessions
          </TabsTrigger>
          <TabsTrigger value="notes">
            <StickyNote className="mr-2 h-4 w-4" />
            Notes
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-4">
          {game.bggId && (
            <>
              {bggLoading && (
                <div className="space-y-3">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-3/4" />
                </div>
              )}

              {bggError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{bggError}</AlertDescription>
                </Alert>
              )}

              {bggDetails && (
                <div className="space-y-4">
                  {/* BGG Image */}
                  {bggDetails.imageUrl && (
                    <div className="flex justify-center">
                      <div className="relative max-h-96 w-full rounded-md overflow-hidden">
                        <Image
                          src={bggDetails.imageUrl}
                          alt={bggDetails.name}
                          width={800}
                          height={384}
                          className="object-contain mx-auto"
                          sizes="(max-width: 768px) 100vw, 800px"
                        />
                      </div>
                    </div>
                  )}

                  {/* BGG Ratings */}
                  <Card>
                    <CardHeader>
                      <CardTitle>Ratings & Stats</CardTitle>
                    </CardHeader>
                    <CardContent className="grid grid-cols-2 gap-4 text-sm">
                      {bggDetails.averageRating && (
                        <div className="flex items-center gap-2">
                          <Star className="h-4 w-4 text-yellow-500" />
                          <span>Rating: {bggDetails.averageRating.toFixed(2)}</span>
                        </div>
                      )}

                      {bggDetails.averageWeight && (
                        <div className="flex items-center gap-2">
                          <TrendingUp className="h-4 w-4 text-muted-foreground" />
                          <span>Complexity: {bggDetails.averageWeight.toFixed(2)}/5</span>
                        </div>
                      )}

                      {bggDetails.usersRated && (
                        <div className="text-muted-foreground col-span-2">
                          {bggDetails.usersRated.toLocaleString()} ratings
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  {/* Description */}
                  {bggDetails.description && (
                    <Card>
                      <CardHeader>
                        <CardTitle>Description</CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div
                          className="prose prose-sm max-w-none text-muted-foreground"
                          dangerouslySetInnerHTML={{ __html: bggDetails.description }}
                        />
                      </CardContent>
                    </Card>
                  )}

                  {/* Categories & Mechanics */}
                  {(bggDetails.categories.length > 0 || bggDetails.mechanics.length > 0) && (
                    <Card>
                      <CardHeader>
                        <CardTitle>Categories & Mechanics</CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-3">
                        {bggDetails.categories.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-2 text-sm">Categories</h4>
                            <div className="flex flex-wrap gap-2">
                              {bggDetails.categories.map(category => (
                                <Badge key={category} variant="secondary">
                                  {category}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}

                        {bggDetails.mechanics.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-2 text-sm">Mechanics</h4>
                            <div className="flex flex-wrap gap-2">
                              {bggDetails.mechanics.map(mechanic => (
                                <Badge key={mechanic} variant="outline">
                                  {mechanic}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  )}

                  {/* Designers & Publishers */}
                  {(bggDetails.designers.length > 0 || bggDetails.publishers.length > 0) && (
                    <Card>
                      <CardHeader>
                        <CardTitle>Credits</CardTitle>
                      </CardHeader>
                      <CardContent className="grid grid-cols-2 gap-4 text-sm">
                        {bggDetails.designers.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-1">Designers</h4>
                            <ul className="list-disc list-inside text-muted-foreground">
                              {bggDetails.designers.slice(0, 5).map(designer => (
                                <li key={designer}>{designer}</li>
                              ))}
                            </ul>
                          </div>
                        )}

                        {bggDetails.publishers.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-1">Publishers</h4>
                            <ul className="list-disc list-inside text-muted-foreground">
                              {bggDetails.publishers.slice(0, 5).map(publisher => (
                                <li key={publisher}>{publisher}</li>
                              ))}
                            </ul>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  )}

                  {/* BGG Link */}
                  <Button variant="outline" asChild className="w-full">
                    <a
                      href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      View on BoardGameGeek
                      <ExternalLink className="ml-2 h-4 w-4" />
                    </a>
                  </Button>
                </div>
              )}
            </>
          )}

          {!game.bggId && (
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>No BoardGameGeek data available for this game.</AlertDescription>
            </Alert>
          )}
        </TabsContent>

        {/* Rules Tab */}
        <TabsContent value="rules" className="space-y-4">
          {/* PDF Rulebooks Section */}
          <Card>
            <CardHeader>
              <div className="flex justify-between items-center">
                <div>
                  <CardTitle>Rulebook PDFs</CardTitle>
                  <CardDescription>Upload and view game rulebooks</CardDescription>
                </div>
                <Button
                  onClick={() => setShowUploadForm(!showUploadForm)}
                  variant={showUploadForm ? 'outline' : 'default'}
                >
                  {showUploadForm ? 'Cancel' : 'Upload Rulebook'}
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {showUploadForm && gameId && game && (
                <div className="mb-4">
                  <PdfUploadForm
                    gameId={gameId}
                    gameName={game.title}
                    onUploadSuccess={handleUploadSuccess}
                    onUploadError={handleUploadError}
                  />
                </div>
              )}

              {pdfsLoading && (
                <div className="space-y-3">
                  <Skeleton className="h-16 w-full" />
                  <Skeleton className="h-16 w-full" />
                </div>
              )}

              {!pdfsLoading && pdfs.length === 0 && !showUploadForm && (
                <Alert>
                  <BookOpen className="h-4 w-4" />
                  <AlertDescription>
                    No rulebook PDFs uploaded yet. Click "Upload Rulebook" to add one.
                  </AlertDescription>
                </Alert>
              )}

              {!pdfsLoading && pdfs.length > 0 && (
                <div className="space-y-3">
                  {pdfs.map(pdf => (
                    <Card key={pdf.id} className="border-2">
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-lg">{pdf.fileName}</CardTitle>
                            <CardDescription>
                              Uploaded: {new Date(pdf.uploadedAt).toLocaleDateString()} •{' '}
                              {pdf.pageCount} pages
                            </CardDescription>
                          </div>
                          <Button onClick={() => handleViewPdf(pdf)} variant="outline">
                            View PDF
                          </Button>
                        </div>
                      </CardHeader>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Rule Specifications Section */}
          <Card>
            <CardHeader>
              <CardTitle>Rule Specifications</CardTitle>
              <CardDescription>Extracted rules and versions (Issue #2027)</CardDescription>
            </CardHeader>
            <CardContent>
              {rulesLoading && (
                <div className="space-y-3">
                  <Skeleton className="h-32 w-full" />
                  <Skeleton className="h-32 w-full" />
                </div>
              )}

              {!rulesLoading && rules.length === 0 && (
                <Alert>
                  <BookOpen className="h-4 w-4" />
                  <AlertDescription>
                    No rule specifications available for this game yet.
                  </AlertDescription>
                </Alert>
              )}

              {!rulesLoading && rules.length > 0 && (
                <div className="space-y-6">
                  {rules.map((ruleSpec, index) => {
                    const currentPage = rulesPagination[ruleSpec.id] || 1;
                    const itemsPerPage = 10;
                    const totalPages = Math.ceil(ruleSpec.atoms.length / itemsPerPage);
                    const startIndex = (currentPage - 1) * itemsPerPage;
                    const paginatedAtoms = ruleSpec.atoms.slice(
                      startIndex,
                      startIndex + itemsPerPage
                    );

                    const handlePageChange = (newPage: number) => {
                      setRulesPagination(prev => ({
                        ...prev,
                        [ruleSpec.id]: newPage,
                      }));
                    };

                    return (
                      <Card key={ruleSpec.id} className="border-2">
                        <CardHeader>
                          <div className="flex items-start justify-between">
                            <div>
                              <CardTitle className="flex items-center gap-2">
                                <Badge variant={index === 0 ? 'default' : 'secondary'}>
                                  Version {ruleSpec.version}
                                </Badge>
                                {index === 0 && <Badge variant="outline">Latest</Badge>}
                              </CardTitle>
                              <CardDescription className="mt-2">
                                Created: {new Date(ruleSpec.createdAt).toLocaleString()} •{' '}
                                {ruleSpec.atoms.length} rule{ruleSpec.atoms.length !== 1 ? 's' : ''}
                              </CardDescription>
                            </div>
                          </div>
                        </CardHeader>

                        <CardContent className="space-y-3">
                          {paginatedAtoms.length === 0 && (
                            <p className="text-sm text-muted-foreground">
                              No rules in this version
                            </p>
                          )}

                          {paginatedAtoms.map((atom, atomIndex) => (
                            <div key={atom.id} className="border-l-2 border-primary/20 pl-4 py-2">
                              <div className="flex items-start gap-2">
                                <Badge variant="outline" className="text-xs shrink-0">
                                  #{startIndex + atomIndex + 1}
                                </Badge>
                                <div className="flex-1">
                                  <p className="text-sm">{atom.text}</p>
                                  <div className="flex gap-3 mt-1 text-xs text-muted-foreground">
                                    {atom.section && <span>Section: {atom.section}</span>}
                                    {atom.page && <span>Page: {atom.page}</span>}
                                    {atom.line && <span>Line: {atom.line}</span>}
                                  </div>
                                </div>
                              </div>
                            </div>
                          ))}

                          {totalPages > 1 && (
                            <>
                              <Separator className="my-4" />
                              <div className="flex items-center justify-between">
                                <p className="text-sm text-muted-foreground">
                                  Showing {startIndex + 1}-
                                  {Math.min(startIndex + itemsPerPage, ruleSpec.atoms.length)} of{' '}
                                  {ruleSpec.atoms.length} rules
                                </p>
                                <div className="flex gap-2">
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => handlePageChange(Math.max(1, currentPage - 1))}
                                    disabled={currentPage === 1}
                                  >
                                    Previous
                                  </Button>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() =>
                                      handlePageChange(Math.min(totalPages, currentPage + 1))
                                    }
                                    disabled={currentPage === totalPages}
                                  >
                                    Next
                                  </Button>
                                </div>
                              </div>
                            </>
                          )}
                        </CardContent>
                      </Card>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Sessions Tab */}
        <TabsContent value="sessions" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Play Sessions</CardTitle>
              <CardDescription>Game session history and statistics</CardDescription>
            </CardHeader>
            <CardContent>
              {sessionsLoading && (
                <div className="space-y-3">
                  <Skeleton className="h-16 w-full" />
                  <Skeleton className="h-16 w-full" />
                  <Skeleton className="h-16 w-full" />
                </div>
              )}

              {!sessionsLoading && sessions.length === 0 && (
                <Alert>
                  <PlayCircle className="h-4 w-4" />
                  <AlertDescription>
                    No play sessions recorded yet. Start a new session to track your games!
                  </AlertDescription>
                </Alert>
              )}

              {!sessionsLoading && sessions.length > 0 && (
                <div className="space-y-3">
                  {sessions.map(session => (
                    <Card key={session.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <Badge
                              variant={session.status === 'Completed' ? 'default' : 'secondary'}
                            >
                              {session.status}
                            </Badge>
                            <CardDescription className="mt-2">
                              {new Date(session.startedAt).toLocaleDateString()} •{' '}
                              {session.durationMinutes} min
                            </CardDescription>
                          </div>
                          {session.winnerName && (
                            <Badge variant="outline">Winner: {session.winnerName}</Badge>
                          )}
                        </div>
                      </CardHeader>
                      <CardContent>
                        <div className="flex items-center gap-2 text-sm">
                          <Users className="h-4 w-4 text-muted-foreground" />
                          <span>
                            {session.playerCount} players:{' '}
                            {session.players.map(p => p.playerName).join(', ')}
                          </span>
                        </div>
                        {session.notes && (
                          <p className="text-sm text-muted-foreground mt-2">
                            Notes: {session.notes}
                          </p>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}

              <Separator className="my-4" />

              <Button asChild className="w-full">
                <Link href="/sessions">View All Sessions</Link>
              </Button>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Notes Tab */}
        <TabsContent value="notes">
          <Card>
            <CardHeader>
              <CardTitle>Your Notes</CardTitle>
              <CardDescription>Personal notes about {game.title}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <Textarea
                placeholder="Write your notes about strategies, house rules, or game impressions..."
                value={notes}
                onChange={e => setNotes(e.target.value)}
                rows={10}
                className="resize-none"
              />
              <div className="flex justify-between items-center">
                <p className="text-xs text-muted-foreground">
                  Notes are saved locally in your browser
                </p>
                <Button onClick={handleSaveNotes}>
                  <StickyNote className="mr-2 h-4 w-4" />
                  Save Notes
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* PDF Viewer Modal */}
      {selectedPdf && (
        <PdfViewerModal
          open={pdfViewerOpen}
          onOpenChange={setPdfViewerOpen}
          pdfUrl={selectedPdf.url}
          documentName={selectedPdf.name}
        />
      )}
    </div>
  );
}
