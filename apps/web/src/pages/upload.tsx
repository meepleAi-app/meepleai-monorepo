import { useCallback, useEffect, useState } from 'react';
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

interface PdfProcessingResponse {
  id: string;
  fileName: string;
  extractedText?: string | null;
  processingStatus: ProcessingStatus;
  processedAt?: string | null;
  pageCount?: number | null;
  characterCount?: number | null;
  processingError?: string | null;
}

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
  const [pollingError, setPollingError] = useState<string | null>(null);
  const [autoAdvanceTriggered, setAutoAdvanceTriggered] = useState(false);

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
        setMessage(`✅ PDF uploaded successfully! Document ID: ${data.documentId}`);
        setProcessingStatus('pending');
        setProcessingError(null);
        setPollingError(null);
        setAutoAdvanceTriggered(false);
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

  const handleParse = useCallback(async () => {
    if (!confirmedGameId) {
      setMessage('Please confirm a game before parsing');
      return;
    }

    if (!documentId) {
      setMessage('Please upload a PDF before parsing');
      return;
    }

    setParsing(true);
    setMessage('');
    setRuleSpec(null);

    try {
      const fetchedRuleSpec = await api.get<RuleSpec>(`/games/${confirmedGameId}/rulespec`);

      if (!fetchedRuleSpec) {
        setMessage('❌ Parse failed: Unable to load RuleSpec.');
        return;
      }

      setRuleSpec(fetchedRuleSpec);
      const response = await fetch(`${API_BASE}/ingest/pdf/${documentId}/rulespec`, {
        method: 'POST',
        credentials: 'include'
      });

      if (!response.ok) {
        let errorMessage = response.statusText;
        try {
          const errorBody = await response.json();
          if (errorBody?.error) {
            errorMessage = errorBody.error;
          }
        } catch (jsonError) {
          console.warn('Failed to parse error response', jsonError);
        }
        throw new Error(errorMessage);
      }

      const spec = (await response.json()) as RuleSpec;

      setRuleSpec(spec);
      setMessage('✅ PDF parsed successfully!');
      setCurrentStep('review');
    } catch (error) {
      setMessage(`❌ Parse failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setParsing(false);
    }
  }, [confirmedGameId]);

  useEffect(() => {
    if (currentStep !== 'parse' || !documentId) {
      return;
    }

    let cancelled = false;
    let timeout: ReturnType<typeof setTimeout> | undefined;

    const pollStatus = async () => {
      try {
        const response = await fetch(`${API_BASE}/pdfs/${documentId}/text`, {
          credentials: 'include'
        });

        if (!response.ok) {
          const errorBody = await response.json().catch(() => null);
          if (!cancelled) {
            setPollingError(errorBody?.error ?? response.statusText);
          }
          if (!cancelled) {
            timeout = setTimeout(pollStatus, 4000);
          }
          return;
        }

        const data: PdfProcessingResponse = await response.json();
        if (cancelled) {
          return;
        }

        setProcessingStatus(data.processingStatus);
        setProcessingError(data.processingError ?? null);
        setPollingError(null);

        if (data.processingStatus === 'failed') {
          setMessage(`❌ Parse failed: ${data.processingError ?? 'Processing failed. Please try again.'}`);
          return;
        }

        if (data.processingStatus === 'completed') {
          return;
        }

        timeout = setTimeout(pollStatus, 2000);
      } catch (error) {
        if (cancelled) {
          return;
        }
        setPollingError(error instanceof Error ? error.message : 'Unknown error');
        timeout = setTimeout(pollStatus, 4000);
      }
    };

    pollStatus();

    return () => {
      cancelled = true;
      if (timeout) {
        clearTimeout(timeout);
      }
    };
  }, [API_BASE, currentStep, documentId]);

  useEffect(() => {
    if (currentStep !== 'parse') {
      return;
    }

    if (processingStatus === 'completed' && !autoAdvanceTriggered) {
      setAutoAdvanceTriggered(true);
      void handleParse();
    }
  }, [autoAdvanceTriggered, currentStep, handleParse, processingStatus]);

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
    setMessage('');
    setProcessingStatus(null);
    setProcessingError(null);
    setPollingError(null);
    setAutoAdvanceTriggered(false);
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
  const statusLabels: Record<ProcessingStatus, string> = {
    pending: 'Pending',
    processing: 'Processing',
    completed: 'Completed',
    failed: 'Failed'
  };
  const statusProgress: Record<ProcessingStatus, number> = {
    pending: 20,
    processing: 65,
    completed: 100,
    failed: 100
  };
  const effectiveProcessingStatus: ProcessingStatus = processingStatus ?? 'pending';
  const processingProgress = statusProgress[effectiveProcessingStatus];

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

      {isUnauthorizedRole ? (
        renderUnauthorizedState()
      ) : (
        <>
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
              role="progressbar"
              aria-label="PDF processing progress"
              aria-valuenow={processingProgress}
              aria-valuemin={0}
              aria-valuemax={100}
              style={{
                width: '100%',
                backgroundColor: '#e5e7eb',
                borderRadius: '999px',
                height: '12px',
                overflow: 'hidden',
                marginBottom: '12px'
              }}
            >
              <div
                style={{
                  width: `${processingProgress}%`,
                  transition: 'width 0.6s ease',
                  backgroundColor:
                    effectiveProcessingStatus === 'completed'
                      ? '#34a853'
                      : effectiveProcessingStatus === 'failed'
                        ? '#d93025'
                        : '#0070f3',
                  height: '100%'
                }}
              />
            </div>
            <p style={{ marginBottom: '8px', color: '#444' }}>
              Processing status: <strong>{statusLabels[effectiveProcessingStatus]}</strong>
            </p>
            {pollingError && (
              <p style={{ color: '#d93025', marginBottom: '8px' }}>
                Status refresh failed: {pollingError}
              </p>
            )}
            {processingError && effectiveProcessingStatus === 'failed' && (
              <p style={{ color: '#d93025', marginBottom: '8px' }}>
                Processing error: {processingError}
              </p>
            )}
            <p style={{ marginBottom: '0', color: '#666' }}>
              The wizard will automatically continue once processing is completed.
            </p>
          </div>
          <button
            onClick={handleParse}
            disabled={parsing || !documentId}
            style={{
              padding: '12px 24px',
              backgroundColor: parsing || !documentId ? '#ccc' : '#0070f3',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              fontSize: '16px',
              cursor: parsing || !documentId ? 'not-allowed' : 'pointer',
              fontWeight: '500',
              marginRight: '12px'
            }}
          >
            {effectiveProcessingStatus !== 'completed'
              ? 'Waiting for processing...'
              : parsing
                ? 'Loading rules...'
                : 'Continue to review'}
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
        </>
      )}
    </div>
  );
}