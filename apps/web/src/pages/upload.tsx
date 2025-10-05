import { useCallback, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { api } from '../lib/api';

interface PdfDocument {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
}

type ProcessingStatus = 'pending' | 'processing' | 'completed' | 'failed';

interface PdfTextResponse {
  id: string;
  fileName: string;
  extractedText?: string | null;
  processingStatus: ProcessingStatus;
  processedAt?: string | null;
  pageCount?: number | null;
  characterCount?: number | null;
  processingError?: string | null;
}

const POLLING_INTERVAL_MS = 2000;

interface RuleAtom {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
}

interface RuleSpec {
  gameId: string;
  version: string;
  createdAt: string;
  rules: RuleAtom[];
}

interface GameSummary {
  id: string;
  name: string;
  createdAt: string;
}

interface AuthUser {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
}

interface AuthResponse {
  user: AuthUser;
  expiresAt: string;
}

type WizardStep = 'upload' | 'parse' | 'review' | 'publish';

export default function UploadPage() {
  const [currentStep, setCurrentStep] = useState<WizardStep>('upload');
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [games, setGames] = useState<GameSummary[]>([]);
  const [loadingGames, setLoadingGames] = useState(true);
  const [selectedGameId, setSelectedGameId] = useState('');
  const [confirmedGameId, setConfirmedGameId] = useState<string | null>(null);
  const [newGameName, setNewGameName] = useState('');
  const [creatingGame, setCreatingGame] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [parsing, setParsing] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [message, setMessage] = useState('');
  const [documentId, setDocumentId] = useState<string>('');
  const [ruleSpec, setRuleSpec] = useState<RuleSpec | null>(null);
  const [pdfs, setPdfs] = useState<PdfDocument[]>([]);
  const [loadingPdfs, setLoadingPdfs] = useState(false);
  const [processingStatus, setProcessingStatus] = useState<ProcessingStatus | null>(null);
  const [processingError, setProcessingError] = useState<string | null>(null);
  const [isPolling, setIsPolling] = useState(false);
  const [processingMetadata, setProcessingMetadata] = useState<
    | {
        pageCount?: number | null;
        characterCount?: number | null;
      }
    | null
  >(null);
  const autoAdvanceRef = useRef(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const loadPdfs = async (gameId: string) => {
    if (!gameId) {
      return;
    }

    setLoadingPdfs(true);
    try {
      const response = await fetch(`${API_BASE}/games/${gameId}/pdfs`, {
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        setPdfs(data.pdfs || []);
      } else {
        console.error('Failed to load PDFs:', response.statusText);
      }
    } catch (error) {
      console.error('Failed to load PDFs:', error);
    } finally {
      setLoadingPdfs(false);
    }
  };

  const initialize = async () => {
    setLoadingGames(true);
    try {
      const me = await api.get<AuthResponse>('/auth/me');
      if (!me) {
        setAuthUser(null);
        setGames([]);
        setSelectedGameId('');
        setConfirmedGameId(null);
        return;
      }

      setAuthUser(me.user);
      const fetchedGames = (await api.get<GameSummary[]>('/games')) ?? [];
      setGames(fetchedGames);
      if (fetchedGames.length > 0) {
        setSelectedGameId(fetchedGames[0].id);
      }
    } catch (error) {
      console.error('Failed to load games', error);
      setMessage('❌ Unable to load games. Please refresh and try again.');
    } finally {
      setLoadingGames(false);
    }
  };

  useEffect(() => {
    void initialize();
  }, []);

  useEffect(() => {
    if (confirmedGameId) {
      void loadPdfs(confirmedGameId);
    } else {
      setPdfs([]);
    }
  }, [confirmedGameId]);

  useEffect(() => {
    if (currentStep !== 'parse' || !documentId) {
      return;
    }

    let cancelled = false;
    let timeoutId: ReturnType<typeof setTimeout> | null = null;

    const pollStatus = async () => {
      try {
        const response = await fetch(`${API_BASE}/pdfs/${documentId}/text`, {
          credentials: 'include',
        });

        if (!response.ok) {
          let errorMessage = response.statusText || 'Request failed';
          try {
            const errorBody = await response.json();
            if (errorBody && typeof errorBody.error === 'string') {
              errorMessage = errorBody.error;
            }
          } catch {
            // Ignore JSON parse errors for error responses
          }

          throw new Error(errorMessage);
        }

        const data = (await response.json()) as PdfTextResponse;
        if (cancelled) {
          return;
        }

        const status = data.processingStatus;
        setProcessingStatus(status);
        setProcessingMetadata({
          pageCount: data.pageCount ?? null,
          characterCount: data.characterCount ?? null,
        });

        if (status === 'failed') {
          const errorMessage = data.processingError || 'Unknown error';
          setProcessingError(errorMessage);
          setMessage(`❌ PDF processing failed: ${errorMessage}`);
          setIsPolling(false);
          return;
        }

        setProcessingError(null);

        if (status === 'completed') {
          setIsPolling(false);
          setMessage('✅ PDF processing completed. Loading RuleSpec…');
          return;
        }
      } catch (error) {
        if (cancelled) {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        setProcessingError(errorMessage);
        setMessage(`❌ Unable to poll PDF status: ${errorMessage}`);
        setIsPolling(false);
        return;
      }

      if (!cancelled) {
        timeoutId = setTimeout(pollStatus, POLLING_INTERVAL_MS);
      }
    };

    setIsPolling(true);
    pollStatus();

    return () => {
      cancelled = true;
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    };
  }, [API_BASE, currentStep, documentId]);

  const handleParse = useCallback(async () => {
    if (!confirmedGameId) {
      setMessage('Please confirm a game before parsing');
      return;
    }

    if (!documentId) {
      setMessage('No document to parse. Please upload a PDF first.');
      return;
    }

    autoAdvanceRef.current = true;
    setParsing(true);
    setMessage('');

    try {
      const fetchedRuleSpec = await api.get<RuleSpec>(`/games/${confirmedGameId}/rulespec`);

      if (!fetchedRuleSpec) {
        setMessage('❌ Failed to load RuleSpec: unauthorized');
        return;
      }

      setRuleSpec(fetchedRuleSpec);
      setMessage('✅ RuleSpec loaded successfully!');
      setCurrentStep('review');
    } catch (error) {
      setMessage(`❌ Failed to load RuleSpec: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setParsing(false);
    }
  }, [confirmedGameId, documentId]);

  useEffect(() => {
    if (currentStep !== 'parse') {
      return;
    }

    if (processingStatus === 'completed' && !autoAdvanceRef.current) {
      autoAdvanceRef.current = true;
      void handleParse();
    }
  }, [currentStep, handleParse, processingStatus]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setMessage('');
    }
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!file) {
      setMessage('Please select a PDF file');
      return;
    }

    if (!confirmedGameId) {
      setMessage('Please confirm a game before uploading');
      return;
    }

    setUploading(true);
    setMessage('');

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('gameId', confirmedGameId);

      const response = await fetch(`${API_BASE}/ingest/pdf`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        setDocumentId(data.documentId);
        setProcessingStatus('pending');
        setProcessingError(null);
        setProcessingMetadata(null);
        autoAdvanceRef.current = false;
        setMessage(`✅ PDF uploaded successfully! Document ID: ${data.documentId}`);
        setCurrentStep('parse');
      } else {
        const error = await response.json();
        setMessage(`❌ Upload failed: ${error.error || response.statusText}`);
      }
    } catch (error) {
      setMessage(`❌ Upload failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setUploading(false);
    }
  };

  const handlePublish = async () => {
    if (!ruleSpec || !confirmedGameId) {
      setMessage('No RuleSpec or game selected to publish');
      return;
    }

    setPublishing(true);
    setMessage('');

    try {
      const response = await fetch(`${API_BASE}/games/${confirmedGameId}/rulespec`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(ruleSpec)
      });

      if (response.ok) {
        setMessage('✅ RuleSpec published successfully!');
        setCurrentStep('publish');
      } else {
        const error = await response.json();
        setMessage(`❌ Publish failed: ${error.error || response.statusText}`);
      }
    } catch (error) {
      setMessage(`❌ Publish failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setPublishing(false);
    }
  };

  const resetWizard = () => {
    setCurrentStep('upload');
    setFile(null);
    setDocumentId('');
    setRuleSpec(null);
    setProcessingStatus(null);
    setProcessingError(null);
    setProcessingMetadata(null);
    setIsPolling(false);
    autoAdvanceRef.current = false;
    setMessage('');
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.value = '';
    }
  };

  const updateRuleAtom = (index: number, field: keyof RuleAtom, value: string) => {
    if (!ruleSpec) return;
    const updatedRules = [...ruleSpec.rules];
    updatedRules[index] = { ...updatedRules[index], [field]: value };
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  };

  const deleteRuleAtom = (index: number) => {
    if (!ruleSpec) return;
    const updatedRules = ruleSpec.rules.filter((_, i) => i !== index);
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  };

  const addRuleAtom = () => {
    if (!ruleSpec) return;
    const newRule: RuleAtom = {
      id: String(ruleSpec.rules.length + 1),
      text: '',
      section: null,
      page: null,
      line: null
    };
    setRuleSpec({ ...ruleSpec, rules: [...ruleSpec.rules, newRule] });
  };

  const confirmSelectedGame = () => {
    if (!selectedGameId) {
      setMessage('Please choose a game to confirm');
      return;
    }

    setConfirmedGameId(selectedGameId);
    setMessage('');
  };

  const handleCreateGame = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!authUser) {
      setMessage('You must be logged in to create a game');
      return;
    }

    const trimmedName = newGameName.trim();
    if (!trimmedName) {
      setMessage('Please enter a game name');
      return;
    }

    setCreatingGame(true);
    setMessage('');

    try {
      const created = await api.post<GameSummary>('/games', {
        name: trimmedName
      });

      const updatedGames = [...games.filter(game => game.id !== created.id), created].sort((a, b) =>
        a.name.localeCompare(b.name)
      );
      setGames(updatedGames);
      setSelectedGameId(created.id);
      setConfirmedGameId(created.id);
      setNewGameName('');
      setMessage(`✅ Game "${created.name}" created`);
    } catch (error) {
      setMessage(`❌ Failed to create game: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setCreatingGame(false);
    }
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) {
      return bytes + ' B';
    }
    if (bytes < 1024 * 1024) {
      return (bytes / 1024).toFixed(1) + ' KB';
    }
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const selectedGame = games.find(game => game.id === selectedGameId) ?? null;
  const confirmedGame = confirmedGameId ? games.find(game => game.id === confirmedGameId) ?? null : null;

  const progressPercent = processingStatus === 'completed'
    ? 100
    : processingStatus === 'processing'
      ? 70
      : processingStatus === 'pending'
        ? 35
        : processingStatus === 'failed'
          ? 100
          : 0;

  const statusLabel = processingStatus
    ? processingStatus.charAt(0).toUpperCase() + processingStatus.slice(1)
    : 'Waiting to start';

  const isParseButtonDisabled = parsing || processingStatus !== 'completed';

  const renderStepIndicator = () => {
    const steps: WizardStep[] = ['upload', 'parse', 'review', 'publish'];
    const stepLabels = {
      upload: '1. Upload',
      parse: '2. Parse',
      review: '3. Review',
      publish: '4. Publish'
    };

    return (
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '30px', padding: '20px 0' }}>
        {steps.map((step, index) => {
          const isActive = currentStep === step;
          const isPast = steps.indexOf(currentStep) > index;
          return (
            <div key={step} style={{ flex: 1, textAlign: 'center' }}>
              <div
                style={{
                  width: '40px',
                  height: '40px',
                  borderRadius: '50%',
                  backgroundColor: isActive ? '#0070f3' : isPast ? '#34a853' : '#ddd',
                  color: 'white',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  margin: '0 auto 8px',
                  fontWeight: 'bold'
                }}
              >
                {index + 1}
              </div>
              <div style={{ fontSize: '14px', color: isActive ? '#0070f3' : isPast ? '#34a853' : '#666' }}>
                {stepLabels[step]}
              </div>
            </div>
          );
        })}
      </div>
    );
  };

  return (
    <div style={{ padding: '40px', maxWidth: '900px', margin: '0 auto', fontFamily: 'system-ui, sans-serif' }}>
      <div style={{ marginBottom: '20px' }}>
        <Link href="/" style={{ color: '#0070f3', textDecoration: 'none' }}>
          ← Back to Home
        </Link>
      </div>

      <h1 style={{ marginBottom: '10px' }}>PDF Import Wizard</h1>
      <p style={{ color: '#666', marginBottom: '30px' }}>Upload, parse, review, and publish game rules</p>

      {renderStepIndicator()}

      {message && (
        <div
          style={{
            padding: '16px',
            backgroundColor: message.startsWith('✅') ? '#e8f5e9' : '#ffebee',
            borderRadius: '4px',
            marginBottom: '20px',
            fontSize: '14px',
          }}
        >
          {message}
        </div>
      )}

      {currentStep === 'upload' && (
        <div>
          <h2>Step 1: Upload PDF</h2>
          <div
            style={{
              marginTop: '20px',
              marginBottom: '24px',
              padding: '16px',
              border: '1px solid #e0e0e0',
              borderRadius: '6px',
              backgroundColor: '#f9fafb'
            }}
          >
            <h3 style={{ marginTop: 0, marginBottom: '12px' }}>Game selection</h3>
            {loadingGames ? (
              <p style={{ margin: 0 }}>Loading games…</p>
            ) : !authUser ? (
              <p style={{ margin: 0 }}>You need to be logged in to manage games.</p>
            ) : (
              <>
                {games.length > 0 ? (
                  <div style={{ marginBottom: '16px' }}>
                    <label htmlFor="gameSelect" style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>
                      Existing games
                    </label>
                    <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
                      <select
                        id="gameSelect"
                        value={selectedGameId}
                        onChange={(e) => {
                          setSelectedGameId(e.target.value);
                          setConfirmedGameId(null);
                        }}
                        style={{
                          flex: 1,
                          minWidth: '220px',
                          padding: '10px',
                          border: '1px solid #ccc',
                          borderRadius: '4px',
                          fontSize: '15px'
                        }}
                      >
                        {games.map((game) => (
                          <option key={game.id} value={game.id}>
                            {game.name}
                          </option>
                        ))}
                      </select>
                      <button
                        type="button"
                        onClick={confirmSelectedGame}
                        disabled={!selectedGameId || confirmedGameId === selectedGameId}
                        style={{
                          padding: '10px 18px',
                          backgroundColor:
                            !selectedGameId || confirmedGameId === selectedGameId ? '#ccc' : '#0070f3',
                          color: 'white',
                          border: 'none',
                          borderRadius: '4px',
                          cursor:
                            !selectedGameId || confirmedGameId === selectedGameId ? 'not-allowed' : 'pointer',
                          fontWeight: 500
                        }}
                      >
                        Confirm selection
                      </button>
                    </div>
                    <p style={{ marginTop: '8px', fontSize: '14px', color: '#555' }}>
                      {confirmedGame
                        ? `Confirmed game: ${confirmedGame.name} (${confirmedGame.id})`
                        : 'Confirm a game to enable uploads.'}
                    </p>
                  </div>
                ) : (
                  <p style={{ marginBottom: '16px' }}>You don&apos;t have any games yet. Create one to get started.</p>
                )}

                <form
                  onSubmit={handleCreateGame}
                  style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}
                >
                  <div style={{ flex: 1, minWidth: '220px' }}>
                    <label htmlFor="newGameName" style={{ display: 'block', marginBottom: '6px', fontWeight: 500 }}>
                      New game name
                    </label>
                    <input
                      id="newGameName"
                      value={newGameName}
                      onChange={(e) => setNewGameName(e.target.value)}
                      placeholder="e.g., Settlers of Catan"
                      style={{
                        width: '100%',
                        padding: '10px',
                        border: '1px solid #ccc',
                        borderRadius: '4px',
                        fontSize: '15px'
                      }}
                    />
                  </div>
                  <button
                    type="submit"
                    disabled={creatingGame}
                    style={{
                      padding: '10px 20px',
                      backgroundColor: creatingGame ? '#ccc' : '#34a853',
                      color: 'white',
                      border: 'none',
                      borderRadius: '4px',
                      cursor: creatingGame ? 'not-allowed' : 'pointer',
                      fontWeight: 500
                    }}
                  >
                    {creatingGame ? 'Creating…' : games.length > 0 ? 'Create another game' : 'Create first game'}
                  </button>
                </form>
              </>
            )}
          </div>

          <form onSubmit={handleUpload} style={{ marginTop: '20px' }}>
            <div style={{ marginBottom: '20px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: '500' }}>Confirmed game</label>
              <div
                style={{
                  padding: '12px',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  backgroundColor: confirmedGame ? '#fff' : '#f1f3f4',
                  color: confirmedGame ? '#202124' : '#5f6368'
                }}
              >
                {confirmedGame
                  ? `${confirmedGame.name} (${confirmedGame.id})`
                  : 'No game confirmed yet'}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label htmlFor="fileInput" style={{ display: 'block', marginBottom: '8px', fontWeight: '500' }}>
                PDF File:
              </label>
              <input
                id="fileInput"
                type="file"
                accept="application/pdf"
                onChange={handleFileChange}
                style={{
                  width: '100%',
                  padding: '12px',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '16px',
                }}
              />
              {file && (
                <p style={{ marginTop: '8px', fontSize: '14px', color: '#666' }}>
                  Selected: {file.name} ({formatFileSize(file.size)})
                </p>
              )}
            </div>

            <button
              type="submit"
              disabled={uploading || !file || !confirmedGameId}
              style={{
                padding: '12px 24px',
                backgroundColor: uploading || !file || !confirmedGameId ? '#ccc' : '#0070f3',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                cursor: uploading || !file || !confirmedGameId ? 'not-allowed' : 'pointer',
                fontWeight: '500',
              }}
            >
              {uploading ? 'Uploading...' : 'Upload & Continue'}
            </button>
            {!confirmedGameId && (
              <p style={{ marginTop: '8px', fontSize: '13px', color: '#d93025' }}>
                Confirm a game to enable uploads.
              </p>
            )}
          </form>
        </div>
      )}

      {currentStep === 'parse' && (
        <div>
          <h2>Step 2: Parse PDF</h2>
          <p style={{ marginTop: '16px', marginBottom: '24px', color: '#666' }}>
            Document ID: <strong>{documentId}</strong>
          </p>
          <div style={{ marginBottom: '24px' }}>
            <div
              style={{
                height: '12px',
                borderRadius: '6px',
                backgroundColor: '#eee',
                overflow: 'hidden'
              }}
            >
              <div
                style={{
                  width: `${progressPercent}%`,
                  height: '100%',
                  backgroundColor:
                    processingStatus === 'failed'
                      ? '#d93025'
                      : progressPercent === 100
                        ? '#34a853'
                        : '#0070f3',
                  transition: 'width 0.4s ease'
                }}
              />
            </div>
            <p style={{ marginTop: '12px', marginBottom: '8px', color: '#444' }}>
              Processing status: <strong>{statusLabel}</strong>
            </p>
            {processingMetadata && (processingMetadata.pageCount != null || processingMetadata.characterCount != null) && (
              <p style={{ margin: 0, fontSize: '14px', color: '#555' }}>
                {processingMetadata.pageCount != null && <>Pages detected: {processingMetadata.pageCount}</>}
                {processingMetadata.characterCount != null && (
                  <>
                    {processingMetadata.pageCount != null ? ' • ' : ''}
                    Characters: {processingMetadata.characterCount}
                  </>
                )}
              </p>
            )}
            {isPolling && processingStatus !== 'completed' && processingStatus !== 'failed' && (
              <p style={{ marginTop: '8px', fontSize: '13px', color: '#666' }}>Checking status…</p>
            )}
            {processingError && (
              <p style={{ marginTop: '8px', fontSize: '14px', color: '#d93025' }}>Error: {processingError}</p>
            )}
          </div>
          <p style={{ marginBottom: '24px', color: '#666' }}>
            The wizard will automatically load the latest RuleSpec once processing completes. You can also retry manually once the
            button is enabled.
          </p>
          <button
            onClick={handleParse}
            disabled={isParseButtonDisabled}
            style={{
              padding: '12px 24px',
              backgroundColor: isParseButtonDisabled ? '#ccc' : '#0070f3',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              fontSize: '16px',
              cursor: isParseButtonDisabled ? 'not-allowed' : 'pointer',
              fontWeight: '500',
              marginRight: '12px'
            }}
          >
            {parsing ? 'Loading RuleSpec…' : 'Load RuleSpec'}
          </button>
          <button
            onClick={resetWizard}
            style={{
              padding: '12px 24px',
              backgroundColor: '#666',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              fontSize: '16px',
              cursor: 'pointer',
              fontWeight: '500',
            }}
          >
            Start Over
          </button>
        </div>
      )}

      {currentStep === 'review' && ruleSpec && (
        <div>
          <h2>Step 3: Review & Edit Rules</h2>
          <div style={{ marginTop: '20px', marginBottom: '20px', padding: '16px', backgroundColor: '#f5f5f5', borderRadius: '4px' }}>
            <p><strong>Game ID:</strong> {ruleSpec.gameId}</p>
            <p><strong>Version:</strong> {ruleSpec.version}</p>
            <p><strong>Total Rules:</strong> {ruleSpec.rules.length}</p>
          </div>

          <div style={{ marginBottom: '20px' }}>
            {ruleSpec.rules.map((rule, index) => (
              <div key={index} style={{ marginBottom: '20px', padding: '16px', border: '1px solid #ddd', borderRadius: '4px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '12px' }}>
                  <h4 style={{ margin: 0 }}>Rule {index + 1}</h4>
                  <button
                    onClick={() => deleteRuleAtom(index)}
                    style={{
                      padding: '4px 12px',
                      backgroundColor: '#d93025',
                      color: 'white',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '12px',
                      cursor: 'pointer'
                    }}
                  >
                    Delete
                  </button>
                </div>
                <div style={{ marginBottom: '12px' }}>
                  <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: '500' }}>Text:</label>
                  <textarea
                    value={rule.text}
                    onChange={(e) => updateRuleAtom(index, 'text', e.target.value)}
                    style={{
                      width: '100%',
                      padding: '8px',
                      border: '1px solid #ddd',
                      borderRadius: '4px',
                      fontSize: '14px',
                      minHeight: '60px'
                    }}
                  />
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px' }}>
                  <div>
                    <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: '500' }}>Section:</label>
                    <input
                      value={rule.section || ''}
                      onChange={(e) => updateRuleAtom(index, 'section', e.target.value)}
                      style={{ width: '100%', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', fontSize: '14px' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: '500' }}>Page:</label>
                    <input
                      value={rule.page || ''}
                      onChange={(e) => updateRuleAtom(index, 'page', e.target.value)}
                      style={{ width: '100%', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', fontSize: '14px' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: '500' }}>Line:</label>
                    <input
                      value={rule.line || ''}
                      onChange={(e) => updateRuleAtom(index, 'line', e.target.value)}
                      style={{ width: '100%', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', fontSize: '14px' }}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>

          <button
            onClick={addRuleAtom}
            style={{
              padding: '8px 16px',
              backgroundColor: '#34a853',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              fontSize: '14px',
              cursor: 'pointer',
              marginBottom: '20px'
            }}
          >
            + Add Rule
          </button>

          <div style={{ marginTop: '20px' }}>
            <button
              onClick={handlePublish}
              disabled={publishing}
              style={{
                padding: '12px 24px',
                backgroundColor: publishing ? '#ccc' : '#0070f3',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                cursor: publishing ? 'not-allowed' : 'pointer',
                fontWeight: '500',
                marginRight: '12px'
              }}
            >
              {publishing ? 'Publishing...' : 'Publish RuleSpec'}
            </button>
            <button
              onClick={() => setCurrentStep('parse')}
              style={{
                padding: '12px 24px',
                backgroundColor: '#666',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                cursor: 'pointer',
                fontWeight: '500',
                marginRight: '12px'
              }}
            >
              ← Back
            </button>
            <button
              onClick={resetWizard}
              style={{
                padding: '12px 24px',
                backgroundColor: '#d93025',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                cursor: 'pointer',
                fontWeight: '500',
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {currentStep === 'publish' && (
        <div>
          <h2>Step 4: Published Successfully! ✅</h2>
          <p style={{ marginTop: '16px', marginBottom: '24px', fontSize: '16px' }}>
            Your RuleSpec for <strong>{ruleSpec?.gameId ?? confirmedGameId ?? 'unknown game'}</strong> has been
            published successfully!
          </p>
          <div style={{ marginTop: '20px' }}>
            <button
              onClick={resetWizard}
              style={{
                padding: '12px 24px',
                backgroundColor: '#0070f3',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                cursor: 'pointer',
                fontWeight: '500',
                marginRight: '12px'
              }}
            >
              Import Another PDF
            </button>
            <Link
              href={`/editor?gameId=${ruleSpec?.gameId ?? confirmedGameId ?? ''}`}
              style={{
                padding: '12px 24px',
                backgroundColor: '#34a853',
                color: 'white',
                textDecoration: 'none',
                borderRadius: '4px',
                fontSize: '16px',
                fontWeight: '500',
                display: 'inline-block'
              }}
            >
              Edit in RuleSpec Editor
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}