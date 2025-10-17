import Link from 'next/link';
import {
  type ChangeEvent,
  type FormEvent,
  useCallback,
  useEffect,
  useState
} from 'react';
import { api, ApiError } from '../lib/api';
import { categorizeError, type CategorizedError, extractCorrelationId } from '../lib/errorUtils';
import { retryWithBackoff, isRetryableError } from '../lib/retryUtils';
import { ErrorDisplay } from '../components/ErrorDisplay';
import { ProcessingProgress } from '../components/ProcessingProgress';

interface PdfDocument {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
  status?: string | null;
  logUrl?: string | null;
}

type ProcessingStatus = 'pending' | 'processing' | 'completed' | 'failed';

interface PdfProcessingResponse {
  id: string;
  fileName: string;
  processingStatus: ProcessingStatus;
  processingError?: string | null;
}

interface PdfListResponse {
  pdfs?: PdfDocument[];
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

const AUTHORIZED_ROLES = new Set(['admin', 'editor']);
const POLL_INTERVAL_MS = 2000;
const POLL_RETRY_MS = 4000;

// PDF-09: Validation constants
const MAX_PDF_SIZE_BYTES = 104857600; // 100 MB
const ALLOWED_MIME_TYPES = ['application/pdf'];
const PDF_MAGIC_BYTES = '%PDF-';

interface ValidationResult {
  isValid: boolean;
  errors: Record<string, string>;
}

// PDF-09: Client-side PDF validation helper
async function validatePdfFile(file: File): Promise<ValidationResult> {
  const errors: Record<string, string> = {};

  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    errors.fileType = `File must be a PDF (type: ${file.type})`;
  }

  // Size check
  if (file.size > MAX_PDF_SIZE_BYTES) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    const maxMB = (MAX_PDF_SIZE_BYTES / 1024 / 1024).toFixed(0);
    errors.fileSize = `File size (${sizeMB} MB) exceeds maximum of ${maxMB} MB`;
  }

  if (file.size === 0) {
    errors.fileSize = 'File is empty';
  }

  // Magic bytes check
  try {
    const header = await readFileHeader(file, 5);
    if (header !== PDF_MAGIC_BYTES) {
      errors.fileFormat = 'Invalid PDF file format';
    }
  } catch (error) {
    errors.fileFormat = 'Unable to read file header';
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors
  };
}

// Helper to read file header
async function readFileHeader(file: File, bytesToRead: number): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      if (e.target?.result) {
        const bytes = new Uint8Array(e.target.result as ArrayBuffer);
        const header = String.fromCharCode(...Array.from(bytes));
        resolve(header);
      } else {
        reject(new Error('Failed to read file'));
      }
    };
    reader.onerror = () => reject(new Error('FileReader error'));
    reader.readAsArrayBuffer(file.slice(0, bytesToRead));
  });
}

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
  const [documentId, setDocumentId] = useState('');
  const [ruleSpec, setRuleSpec] = useState<RuleSpec | null>(null);
  const [pdfs, setPdfs] = useState<PdfDocument[]>([]);
  const [loadingPdfs, setLoadingPdfs] = useState(false);
  const [pdfsError, setPdfsError] = useState<string | null>(null);
  const [processingStatus, setProcessingStatus] = useState<ProcessingStatus | null>(null);
  const [processingError, setProcessingError] = useState<string | null>(null);
  const [pollingError, setPollingError] = useState<string | null>(null);
  const [autoAdvanceTriggered, setAutoAdvanceTriggered] = useState(false);
  const [retryingPdfId, setRetryingPdfId] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<CategorizedError | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [validating, setValidating] = useState(false);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [showProcessingProgress, setShowProcessingProgress] = useState(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const loadPdfs = useCallback(
    async (gameId: string) => {
      if (!gameId) {
        return;
      }

      setLoadingPdfs(true);
      setPdfsError(null);

      try {
        const response = await fetch(`${API_BASE}/api/v1/games/${gameId}/pdfs`, {
          credentials: 'include'
        });

        if (!response.ok) {
          console.error('Failed to load PDFs:', response.statusText);
          setPdfsError('Unable to load uploaded PDFs. Please try again.');
          return;
        }

        const data: PdfListResponse = await response.json();
        setPdfs(data.pdfs ?? []);
      } catch (error) {
        console.error('Failed to load PDFs:', error);
        setPdfsError('Unable to load uploaded PDFs. Please try again.');
      } finally {
        setLoadingPdfs(false);
      }
    },
    [API_BASE]
  );

  const initialize = useCallback(async () => {
    setLoadingGames(true);
    try {
      const me = await api.get<AuthResponse>('/api/v1/auth/me');
      if (!me) {
        setAuthUser(null);
        setGames([]);
        setSelectedGameId('');
        setConfirmedGameId(null);
        return;
      }

      setAuthUser(me.user);
      const fetchedGames = (await api.get<GameSummary[]>('/api/v1/games')) ?? [];
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
  }, []);

  useEffect(() => {
    void initialize();
  }, [initialize]);

  useEffect(() => {
    if (confirmedGameId) {
      void loadPdfs(confirmedGameId);
    } else {
      setPdfs([]);
      setPdfsError(null);
    }
  }, [confirmedGameId, loadPdfs]);

  useEffect(() => {
    if (currentStep !== 'parse' || !documentId) {
      return;
    }

    let cancelled = false;
    let timeout: ReturnType<typeof setTimeout> | undefined;

    const pollStatus = async () => {
      try {
        const response = await fetch(`${API_BASE}/api/v1/pdfs/${documentId}/text`, {
          credentials: 'include'
        });

        if (!response.ok) {
          const errorBody = await response.json().catch(() => null);
          if (!cancelled) {
            setPollingError(errorBody?.error ?? response.statusText);
            timeout = setTimeout(pollStatus, POLL_RETRY_MS);
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

        if (data.processingStatus !== 'completed') {
          timeout = setTimeout(pollStatus, POLL_INTERVAL_MS);
        }
      } catch (error) {
        if (cancelled) {
          return;
        }
        setPollingError(error instanceof Error ? error.message : 'Unknown error');
        timeout = setTimeout(pollStatus, POLL_RETRY_MS);
      }
    };

    void pollStatus();

    return () => {
      cancelled = true;
      if (timeout) {
        clearTimeout(timeout);
      }
    };
  }, [API_BASE, currentStep, documentId]);

  const handleParse = useCallback(async () => {
    if (!confirmedGameId) {
      setMessage('Please confirm a game before parsing');
      return;
    }

    if (!documentId) {
      setMessage('Please upload a PDF before parsing');
      return;
    }

    setAutoAdvanceTriggered(true);
    setParsing(true);
    setMessage('');
    setRuleSpec(null);

    try {
      const fetchedRuleSpec = await api.get<RuleSpec>(`/api/v1/games/${confirmedGameId}/rulespec`);

      if (!fetchedRuleSpec) {
        setMessage('❌ Parse failed: Unable to load RuleSpec.');
        return;
      }

      setRuleSpec(fetchedRuleSpec);
      setMessage('✅ PDF parsed successfully!');
      setCurrentStep('review');

      await loadPdfs(confirmedGameId);
    } catch (error) {
      setMessage(`❌ Parse failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setParsing(false);
    }
  }, [confirmedGameId, documentId, loadPdfs]);

  useEffect(() => {
    if (
      currentStep === 'parse' &&
      documentId &&
      processingStatus === 'completed' &&
      !autoAdvanceTriggered
    ) {
      void handleParse();
    }
  }, [autoAdvanceTriggered, currentStep, documentId, handleParse, processingStatus]);

  const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = event.target.files?.[0];
    if (!selectedFile) {
      setFile(null);
      setValidationErrors({});
      return;
    }

    // PDF-09: Validate file before accepting
    setValidating(true);
    setMessage('');
    setValidationErrors({});

    try {
      const validation = await validatePdfFile(selectedFile);

      if (!validation.isValid) {
        setValidationErrors(validation.errors);
        setFile(null);
        // Reset file input
        event.target.value = '';
      } else {
        setFile(selectedFile);
        setValidationErrors({});
      }
    } catch (error) {
      console.error('Validation error:', error);
      setMessage('Failed to validate file. Please try again.');
      setFile(null);
      event.target.value = '';
    } finally {
      setValidating(false);
    }
  };

  const handleUpload = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

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
    setUploadError(null);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('gameId', confirmedGameId);

      // Use retry logic with exponential backoff for transient errors
      const response = await retryWithBackoff(
        async () => {
          const res = await fetch(`${API_BASE}/api/v1/ingest/pdf`, {
            method: 'POST',
            body: formData,
            credentials: 'include'
          });

          if (!res.ok) {
            const correlationId = extractCorrelationId(res);
            const errorBody = await res.json().catch(() => ({}));
            const errorMessage = errorBody.error ?? res.statusText;

            const apiError = new ApiError(errorMessage, res.status, correlationId, res);
            throw apiError;
          }

          return res;
        },
        {
          maxAttempts: 3,
          shouldRetry: (error) => isRetryableError(error),
          onRetry: (error, attempt, delayMs) => {
            setRetryCount(attempt);
            setMessage(`⏳ Upload failed. Retrying (attempt ${attempt}/3) in ${Math.round(delayMs / 1000)}s...`);
          }
        }
      );

      const data = (await response.json()) as { documentId: string };
      setDocumentId(data.documentId);
      setProcessingStatus('pending');
      setProcessingError(null);
      setPollingError(null);
      setAutoAdvanceTriggered(false);
      setRuleSpec(null);
      setUploadError(null);
      setRetryCount(0);
      setShowProcessingProgress(true);
      setMessage(`✅ PDF uploaded successfully! Document ID: ${data.documentId}`);
      setCurrentStep('parse');
    } catch (error) {
      // Categorize the error and display user-friendly message
      const correlationId = error instanceof ApiError ? error.correlationId : undefined;
      const response = error instanceof ApiError ? error.response : undefined;

      const categorized = categorizeError(error, response, correlationId);
      setUploadError(categorized);
      setMessage(''); // Clear generic message, use ErrorDisplay component instead
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
      const response = await fetch(`${API_BASE}/api/v1/games/${confirmedGameId}/rulespec`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(ruleSpec)
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        setMessage(`❌ Publish failed: ${error.error ?? response.statusText}`);
        return;
      }

      setMessage('✅ RuleSpec published successfully!');
      setCurrentStep('publish');
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
    setPollingError(null);
    setAutoAdvanceTriggered(false);
    setRetryingPdfId(null);
    setUploadError(null);
    setRetryCount(0);
    setValidating(false);
    setValidationErrors({});
    setShowProcessingProgress(false);
    setMessage('');
    const fileInput = document.getElementById('fileInput') as HTMLInputElement | null;
    if (fileInput) {
      fileInput.value = '';
    }
  };

  const updateRuleAtom = (index: number, field: keyof RuleAtom, value: string) => {
    if (!ruleSpec) {
      return;
    }
    const updatedRules = [...ruleSpec.rules];
    updatedRules[index] = { ...updatedRules[index], [field]: value };
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  };

  const deleteRuleAtom = (index: number) => {
    if (!ruleSpec) {
      return;
    }
    const updatedRules = ruleSpec.rules.filter((_, i) => i !== index);
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  };

  const addRuleAtom = () => {
    if (!ruleSpec) {
      return;
    }
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

  const handleOpenLog = (pdf: PdfDocument) => {
    const logUrl = pdf.logUrl || `${API_BASE}/logs/${pdf.id}`;
    if (typeof window !== 'undefined') {
      window.open(logUrl, '_blank', 'noopener,noreferrer');
    }
  };

  const handleRetryParsing = async (pdf: PdfDocument) => {
    if (!confirmedGameId) {
      setMessage('Please confirm a game before retrying the parse');
      return;
    }

    setRetryingPdfId(pdf.id);
    setMessage('');
    try {
      const response = await fetch(`${API_BASE}/api/v1/ingest/pdf/${pdf.id}/retry`, {
        method: 'POST',
        credentials: 'include'
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        setMessage(`❌ Failed to re-trigger parse: ${error.error ?? response.statusText}`);
        return;
      }

      setMessage(`✅ Parse re-triggered for ${pdf.fileName}`);
      await loadPdfs(confirmedGameId);
    } catch (error) {
      setMessage(`❌ Failed to re-trigger parse: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setRetryingPdfId(null);
    }
  };

  const handleProcessingComplete = useCallback(async () => {
    // Refresh PDF list and trigger parse
    if (confirmedGameId) {
      await loadPdfs(confirmedGameId);
    }
    // The auto-advance effect will trigger handleParse when status is completed
  }, [confirmedGameId, loadPdfs]);

  const handleProcessingError = useCallback((error: string) => {
    setMessage(`❌ Processing failed: ${error}`);
    setProcessingError(error);
  }, []);

  const handleCreateGame = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

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
      const created = await api.post<GameSummary>('/api/v1/games', {
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
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

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
  const normalizedRole = authUser?.role?.toLowerCase().trim();
  const isUnauthorizedRole = Boolean(authUser && (!normalizedRole || !AUTHORIZED_ROLES.has(normalizedRole)));

  const renderUnauthorizedState = () => (
    <div
      style={{
        padding: '24px',
        border: '1px solid #e0e0e0',
        borderRadius: '6px',
        backgroundColor: '#fff4f4'
      }}
    >
      <h2 style={{ marginTop: 0 }}>Access restricted</h2>
      <p style={{ marginBottom: '12px' }}>
        You need an Editor or Admin role to manage PDF ingestion. Please contact an administrator to request
        access.
      </p>
      <p style={{ marginBottom: 0 }}>You can still view published rules from the home page.</p>
    </div>
  );

  const renderStepIndicator = () => {
    const steps: WizardStep[] = ['upload', 'parse', 'review', 'publish'];
    const stepLabels: Record<WizardStep, string> = {
      upload: '1. Upload',
      parse: '2. Parse',
      review: '3. Review',
      publish: '4. Publish'
    };
    const currentStepIndex = steps.indexOf(currentStep);
    const currentStepLabel = stepLabels[currentStep];

    return (
      <div
        role="navigation"
        aria-label={`Wizard progress: ${currentStepLabel} (Step ${currentStepIndex + 1} of ${steps.length})`}
        style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '30px', padding: '20px 0' }}
      >
        {steps.map((step, index) => {
          const isActive = currentStep === step;
          const isPast = steps.indexOf(currentStep) > index;
          return (
            <div
              key={step}
              aria-current={isActive ? 'step' : undefined}
              style={{ flex: 1, textAlign: 'center' }}
            >
              <div
                aria-hidden="true"
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

      <main id="main-content">
        <h1 style={{ marginBottom: '10px' }}>PDF Import Wizard</h1>
        <p style={{ color: '#666', marginBottom: '30px' }}>Upload, parse, review, and publish game rules</p>

      {isUnauthorizedRole ? (
        renderUnauthorizedState()
      ) : (
        <>
          {renderStepIndicator()}

          {uploadError && (
            <ErrorDisplay
              error={uploadError}
              onRetry={uploadError.canRetry ? () => {
                setUploadError(null);
                const form = document.querySelector('form') as HTMLFormElement | null;
                if (form) {
                  void handleUpload({ preventDefault: () => {} } as FormEvent<HTMLFormElement>);
                }
              } : undefined}
              onDismiss={() => {
                setUploadError(null);
                setMessage('');
              }}
              showTechnicalDetails={true}
            />
          )}

          {!uploadError && message && (
            <div
              style={{
                padding: '16px',
                backgroundColor: message.startsWith('✅') ? '#e8f5e9' : message.startsWith('⏳') ? '#fff9e6' : '#ffebee',
                borderRadius: '4px',
                marginBottom: '20px',
                fontSize: '14px'
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
                            onChange={event => {
                              setSelectedGameId(event.target.value);
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
                            {games.map(game => (
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
                      <p style={{ marginBottom: '16px' }}>
                        You don&apos;t have any games yet. Create one to get started.
                      </p>
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
                          onChange={event => setNewGameName(event.target.value)}
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
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Confirmed game</label>
                  <div
                    style={{
                      padding: '12px',
                      border: '1px solid #ddd',
                      borderRadius: '4px',
                      backgroundColor: confirmedGame ? '#fff' : '#f1f3f4',
                      color: confirmedGame ? '#202124' : '#64748b'
                    }}
                  >
                    {confirmedGame
                      ? `${confirmedGame.name} (${confirmedGame.id})`
                      : 'No game confirmed yet'}
                  </div>
                </div>

                <div style={{ marginBottom: '20px' }}>
                  <label htmlFor="fileInput" style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>
                    PDF File
                  </label>
                  <input
                    id="fileInput"
                    type="file"
                    accept="application/pdf"
                    onChange={handleFileChange}
                    disabled={validating}
                    style={{
                      width: '100%',
                      padding: '12px',
                      border: `1px solid ${Object.keys(validationErrors).length > 0 ? '#d93025' : '#ddd'}`,
                      borderRadius: '4px',
                      fontSize: '16px'
                    }}
                  />
                  {validating && (
                    <p style={{ marginTop: '8px', fontSize: '14px', color: '#666' }}>
                      Validating file...
                    </p>
                  )}
                  {file && Object.keys(validationErrors).length === 0 && (
                    <p style={{ marginTop: '8px', fontSize: '14px', color: '#34a853' }}>
                      Selected: {file.name} ({formatFileSize(file.size)})
                    </p>
                  )}
                  {Object.keys(validationErrors).length > 0 && (
                    <div style={{
                      marginTop: '8px',
                      padding: '12px',
                      backgroundColor: '#ffebee',
                      borderRadius: '4px',
                      border: '1px solid #d93025'
                    }}>
                      <p style={{ margin: '0 0 8px 0', fontWeight: 600, color: '#d93025' }}>
                        Validation Failed:
                      </p>
                      <ul style={{ margin: 0, paddingLeft: '20px' }}>
                        {Object.entries(validationErrors).map(([key, message]) => (
                          <li key={key} style={{ color: '#d93025', fontSize: '14px' }}>
                            {message}
                          </li>
                        ))}
                      </ul>
                    </div>
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
                    fontWeight: 500
                  }}
                >
                  {uploading ? 'Uploading…' : 'Upload & Continue'}
                </button>
                {!confirmedGameId && (
                  <p style={{ marginTop: '8px', fontSize: '13px', color: '#d93025' }}>
                    Confirm a game to enable uploads.
                  </p>
                )}
              </form>

              <div
                style={{
                  marginTop: '32px',
                  padding: '16px',
                  border: '1px solid #e0e0e0',
                  borderRadius: '6px',
                  backgroundColor: '#fff'
                }}
              >
                <h3 style={{ marginTop: 0, marginBottom: '12px' }}>Uploaded PDFs</h3>
                {!confirmedGameId ? (
                  <p style={{ margin: 0, color: '#64748b' }}>
                    Confirm a game to review its uploaded PDFs.
                  </p>
                ) : loadingPdfs ? (
                  <div role="status" aria-live="polite" style={{ display: 'grid', gap: '12px' }}>
                    {Array.from({ length: Math.max(1, Math.min(3, Math.max(pdfs.length, 1))) }).map((_, index) => (
                      <div
                        key={`pdf-skeleton-${index}`}
                        style={{
                          height: '48px',
                          borderRadius: '4px',
                          background: 'linear-gradient(90deg, #f1f3f4 25%, #e0e0e0 37%, #f1f3f4 63%)',
                          backgroundSize: '200% 100%'
                        }}
                      />
                    ))}
                  </div>
                ) : pdfsError ? (
                  <p style={{ margin: 0, color: '#d93025' }}>{pdfsError}</p>
                ) : pdfs.length === 0 ? (
                  <p style={{ margin: 0, color: '#64748b' }}>No PDFs uploaded yet for this game.</p>
                ) : (
                  <div style={{ overflowX: 'auto' }}>
                    <table aria-label="Uploaded PDFs" style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
                      <thead>
                        <tr style={{ textAlign: 'left', borderBottom: '1px solid #e0e0e0' }}>
                          <th style={{ padding: '8px 12px', fontWeight: 600 }}>File name</th>
                          <th style={{ padding: '8px 12px', fontWeight: 600 }}>Size</th>
                          <th style={{ padding: '8px 12px', fontWeight: 600 }}>Uploaded</th>
                          <th style={{ padding: '8px 12px', fontWeight: 600 }}>Status</th>
                          <th style={{ padding: '8px 12px', fontWeight: 600 }}>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pdfs.map(pdf => (
                          <tr key={pdf.id} style={{ borderBottom: '1px solid #f1f3f4' }}>
                            <td style={{ padding: '10px 12px', fontWeight: 500 }}>{pdf.fileName}</td>
                            <td style={{ padding: '10px 12px' }}>{formatFileSize(pdf.fileSizeBytes)}</td>
                            <td style={{ padding: '10px 12px' }}>{formatDate(pdf.uploadedAt)}</td>
                            <td style={{ padding: '10px 12px' }}>{pdf.status ?? 'Pending'}</td>
                            <td style={{ padding: '10px 12px' }}>
                              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                <button
                                  type="button"
                                  onClick={() => handleOpenLog(pdf)}
                                  style={{
                                    padding: '6px 12px',
                                    borderRadius: '4px',
                                    border: '1px solid #0070f3',
                                    backgroundColor: 'white',
                                    color: '#0070f3',
                                    cursor: 'pointer',
                                    fontWeight: 500
                                  }}
                                >
                                  Open log
                                </button>
                                <button
                                  type="button"
                                  onClick={() => handleRetryParsing(pdf)}
                                  disabled={retryingPdfId === pdf.id}
                                  style={{
                                    padding: '6px 12px',
                                    borderRadius: '4px',
                                    border: 'none',
                                    backgroundColor: retryingPdfId === pdf.id ? '#ccc' : '#34a853',
                                    color: 'white',
                                    cursor: retryingPdfId === pdf.id ? 'not-allowed' : 'pointer',
                                    fontWeight: 500
                                  }}
                                >
                                  {retryingPdfId === pdf.id ? 'Retrying…' : 'Retry parsing'}
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}

          {currentStep === 'parse' && (
            <div>
              <h2>Step 2: Parse PDF</h2>
              <p style={{ marginTop: '16px', marginBottom: '24px', color: '#666' }}>
                Document ID: <strong>{documentId}</strong>
              </p>

              {/* PDF-08: New ProcessingProgress Component */}
              {showProcessingProgress && documentId && (
                <div style={{ marginBottom: '24px' }}>
                  <ProcessingProgress
                    pdfId={documentId}
                    onComplete={handleProcessingComplete}
                    onError={handleProcessingError}
                  />
                </div>
              )}

              {/* Fallback: Old progress bar for backward compatibility */}
              {!showProcessingProgress && (
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
                  <p style={{ marginBottom: 0, color: '#666' }}>
                    The wizard will automatically continue once processing is completed.
                  </p>
                </div>
              )}

              <button
                onClick={() => void handleParse()}
                disabled={parsing || effectiveProcessingStatus !== 'completed'}
                style={{
                  padding: '12px 24px',
                  backgroundColor: parsing || effectiveProcessingStatus !== 'completed' ? '#ccc' : '#0070f3',
                  color: 'white',
                  border: 'none',
                  borderRadius: '4px',
                  fontSize: '16px',
                  cursor: parsing || effectiveProcessingStatus !== 'completed' ? 'not-allowed' : 'pointer',
                  fontWeight: 500,
                  marginRight: '12px'
                }}
              >
                {parsing ? 'Loading rules…' : 'Parse PDF'}
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
                  fontWeight: 500
                }}
              >
                Start Over
              </button>
            </div>
          )}

          {currentStep === 'review' && ruleSpec && (
            <div>
              <h2>Step 3: Review &amp; Edit Rules</h2>
              <div style={{ marginTop: '20px', marginBottom: '20px', padding: '16px', backgroundColor: '#f5f5f5', borderRadius: '4px' }}>
                <p>
                  <strong>Game ID:</strong> {ruleSpec.gameId}
                </p>
                <p>
                  <strong>Version:</strong> {ruleSpec.version}
                </p>
                <p>
                  <strong>Total Rules:</strong> {ruleSpec.rules.length}
                </p>
              </div>

              <div style={{ marginBottom: '20px' }}>
                {ruleSpec.rules.map((rule, index) => (
                  <div key={rule.id ?? index} style={{ marginBottom: '20px', padding: '16px', border: '1px solid #ddd', borderRadius: '4px' }}>
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
                      <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Text</label>
                      <textarea
                        value={rule.text}
                        onChange={event => updateRuleAtom(index, 'text', event.target.value)}
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
                        <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Section</label>
                        <input
                          value={rule.section ?? ''}
                          onChange={event => updateRuleAtom(index, 'section', event.target.value)}
                          style={{ width: '100%', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', fontSize: '14px' }}
                        />
                      </div>
                      <div>
                        <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Page</label>
                        <input
                          value={rule.page ?? ''}
                          onChange={event => updateRuleAtom(index, 'page', event.target.value)}
                          style={{ width: '100%', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', fontSize: '14px' }}
                        />
                      </div>
                      <div>
                        <label style={{ display: 'block', marginBottom: '4px', fontSize: '14px', fontWeight: 500 }}>Line</label>
                        <input
                          value={rule.line ?? ''}
                          onChange={event => updateRuleAtom(index, 'line', event.target.value)}
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
                    fontWeight: 500,
                    marginRight: '12px'
                  }}
                >
                  {publishing ? 'Publishing…' : 'Publish RuleSpec'}
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
                    fontWeight: 500,
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
                    fontWeight: 500
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
                Your RuleSpec for <strong>{ruleSpec?.gameId ?? confirmedGameId ?? 'unknown game'}</strong> has been published
                successfully!
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
                    fontWeight: 500,
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
                    fontWeight: 500,
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
      </main>
    </div>
  );
}
