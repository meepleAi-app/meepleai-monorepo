import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

// Type definitions
type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

type Game = {
  id: string;
  name: string;
};

type Snippet = {
  text: string;
  source: string;
  page: number;
  line: number;
};

type SetupGuideStep = {
  stepNumber: number;
  title: string;
  instruction: string;
  references: Snippet[];
  isOptional: boolean;
};

type SetupGuideResponse = {
  gameTitle: string;
  steps: SetupGuideStep[];
  estimatedSetupTimeMinutes: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  confidence: number | null;
};

// Component for viewing citations in a modal
function CitationModal({
  snippets,
  onClose
}: {
  snippets: Snippet[];
  onClose: () => void;
}) {
  return (
    <div
      style={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 1000,
        padding: 24
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white",
          borderRadius: 8,
          maxWidth: 800,
          maxHeight: "80vh",
          overflow: "auto",
          padding: 24,
          boxShadow: "0 4px 24px rgba(0, 0, 0, 0.15)"
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
          <h3 style={{ margin: 0, fontSize: 18, fontWeight: 600 }}>References</h3>
          <button
            onClick={onClose}
            style={{
              background: "transparent",
              border: "none",
              fontSize: 24,
              cursor: "pointer",
              color: "#94a3b8",
              padding: 0,
              width: 32,
              height: 32,
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
            title="Close"
          >
            ×
          </button>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {snippets.map((snippet, idx) => (
            <div
              key={idx}
              style={{
                padding: 12,
                border: "1px solid #dadce0",
                borderRadius: 6,
                background: "#f8f9fa"
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                <span style={{ fontWeight: 500, fontSize: 14, color: "#202124" }}>
                  {snippet.source}
                </span>
                <span style={{ fontSize: 12, color: "#94a3b8" }}>
                  Page {snippet.page}
                </span>
              </div>
              <div style={{ fontSize: 13, color: "#94a3b8", lineHeight: 1.5 }}>
                {snippet.text}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// Component for individual setup step
function SetupStepCard({
  step,
  isCompleted,
  onToggleComplete,
  onViewReferences
}: {
  step: SetupGuideStep;
  isCompleted: boolean;
  onToggleComplete: () => void;
  onViewReferences: () => void;
}) {
  return (
    <div
      style={{
        border: "1px solid #dadce0",
        borderRadius: 8,
        padding: 20,
        background: isCompleted ? "#f0f7f4" : "white",
        transition: "all 0.2s ease",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)"
      }}
    >
      <div style={{ display: "flex", alignItems: "start", gap: 16 }}>
        {/* Checkbox */}
        <input
          type="checkbox"
          checked={isCompleted}
          onChange={onToggleComplete}
          style={{
            width: 20,
            height: 20,
            marginTop: 2,
            cursor: "pointer",
            accentColor: "#34a853"
          }}
          aria-label={`Mark step ${step.stepNumber} as ${isCompleted ? "incomplete" : "complete"}`}
        />

        {/* Content */}
        <div style={{ flex: 1 }}>
          {/* Header */}
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
            <span style={{ fontWeight: 600, fontSize: 16, color: "#202124" }}>
              {step.stepNumber}. {step.title}
            </span>
            {step.isOptional && (
              <span
                style={{
                  fontSize: 11,
                  fontWeight: 500,
                  padding: "2px 8px",
                  background: "#e8eaed",
                  color: "#94a3b8",
                  borderRadius: 12
                }}
              >
                OPTIONAL
              </span>
            )}
          </div>

          {/* Instruction */}
          <div
            style={{
              fontSize: 14,
              color: "#94a3b8",
              lineHeight: 1.6,
              marginBottom: 12,
              textDecoration: isCompleted ? "line-through" : "none",
              opacity: isCompleted ? 0.7 : 1
            }}
          >
            {step.instruction}
          </div>

          {/* References */}
          {step.references.length > 0 && (
            <button
              onClick={onViewReferences}
              style={{
                background: "transparent",
                border: "1px solid #1a73e8",
                color: "#1a73e8",
                padding: "6px 12px",
                borderRadius: 4,
                fontSize: 12,
                fontWeight: 500,
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                gap: 6
              }}
            >
              <span>📖</span>
              <span>View {step.references.length} Reference{step.references.length > 1 ? "s" : ""}</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// Main Setup Page Component
export default function SetupPage() {
  // Authentication
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);

  // Game selection
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  // Setup guide data
  const [setupGuide, setSetupGuide] = useState<SetupGuideResponse | null>(null);
  const [completedSteps, setCompletedSteps] = useState<Set<number>>(new Set());

  // UI state
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [selectedStepReferences, setSelectedStepReferences] = useState<Snippet[] | null>(null);

  // Loading states
  const [isLoadingGames, setIsLoadingGames] = useState(false);
  const [isLoadingGuide, setIsLoadingGuide] = useState(false);

  // Load current user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  // Load games after user is authenticated
  useEffect(() => {
    if (authUser) {
      void loadGames();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authUser]);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/api/v1/auth/me");
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  };

  const loadGames = async () => {
    setIsLoadingGames(true);
    setErrorMessage("");
    try {
      const gamesList = await api.get<Game[]>("/api/v1/games");
      setGames(gamesList ?? []);

      // Auto-select first game if available
      if (gamesList && gamesList.length > 0 && !selectedGameId) {
        setSelectedGameId(gamesList[0].id);
      }
    } catch (err) {
      console.error("Error loading games:", err);
      setErrorMessage("Error loading games.");
      setGames([]);
    } finally {
      setIsLoadingGames(false);
    }
  };

  const loadSetupGuide = async (e?: FormEvent) => {
    e?.preventDefault();

    if (!selectedGameId) {
      setErrorMessage("Please select a game first.");
      return;
    }

    setIsLoadingGuide(true);
    setErrorMessage("");
    setSetupGuide(null);
    setCompletedSteps(new Set());

    try {
      const guide = await api.post<SetupGuideResponse>("/api/v1/agents/setup", {
        gameId: selectedGameId,
        chatId: null
      });

      if (guide) {
        setSetupGuide(guide);
      }
    } catch (err) {
      console.error("Error loading setup guide:", err);
      setErrorMessage("Error generating setup guide. Please try again.");
    } finally {
      setIsLoadingGuide(false);
    }
  };

  const toggleStepCompletion = (stepNumber: number) => {
    setCompletedSteps((prev) => {
      const next = new Set(prev);
      if (next.has(stepNumber)) {
        next.delete(stepNumber);
      } else {
        next.add(stepNumber);
      }
      return next;
    });
  };

  const resetProgress = () => {
    if (confirm("Are you sure you want to reset all progress?")) {
      setCompletedSteps(new Set());
    }
  };

  const progressPercentage = setupGuide
    ? Math.round((completedSteps.size / setupGuide.steps.length) * 100)
    : 0;

  // Render login required state
  if (!authUser) {
    return (
      <main style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <Link href="/" style={{ color: "#0070f3", textDecoration: "none" }}>
          ← Back to Home
        </Link>
        <div
          style={{
            marginTop: 24,
            padding: 32,
            textAlign: "center",
            border: "1px solid #dadce0",
            borderRadius: 8
          }}
        >
          <h2>Login Required</h2>
          <p>You must be logged in to use the setup guide.</p>
          <Link
            href="/"
            style={{
              display: "inline-block",
              marginTop: 16,
              padding: "8px 16px",
              background: "#0070f3",
              color: "white",
              textDecoration: "none",
              borderRadius: 4
            }}
          >
            Go to Login
          </Link>
        </div>
      </main>
    );
  }

  // Main setup guide interface
  return (
    <main
      style={{
        minHeight: "100vh",
        background: "#f8f9fa",
        fontFamily: "sans-serif"
      }}
    >
      {/* Header */}
      <div
        style={{
          background: "white",
          borderBottom: "1px solid #dadce0",
          padding: "16px 24px",
          position: "sticky",
          top: 0,
          zIndex: 100
        }}
      >
        <div style={{ maxWidth: 900, margin: "0 auto", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600 }}>Game Setup Guide</h1>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500
            }}
          >
            Home
          </Link>
        </div>
      </div>

      {/* Content */}
      <div style={{ maxWidth: 900, margin: "0 auto", padding: 24 }}>
        {/* Game Selection Card */}
        <div
          style={{
            background: "white",
            border: "1px solid #dadce0",
            borderRadius: 8,
            padding: 24,
            marginBottom: 24
          }}
        >
          <h2 style={{ margin: "0 0 16px 0", fontSize: 18, fontWeight: 600 }}>Select Game</h2>

          <form onSubmit={loadSetupGuide} style={{ display: "flex", gap: 12, alignItems: "end" }}>
            <div style={{ flex: 1 }}>
              <label
                htmlFor="gameSelect"
                style={{ display: "block", marginBottom: 8, fontWeight: 500, fontSize: 14, color: "#94a3b8" }}
              >
                Game:
              </label>
              <select
                id="gameSelect"
                value={selectedGameId ?? ""}
                onChange={(e) => setSelectedGameId(e.target.value || null)}
                disabled={isLoadingGames || isLoadingGuide}
                style={{
                  width: "100%",
                  padding: 12,
                  fontSize: 14,
                  border: "1px solid #dadce0",
                  borderRadius: 4,
                  background: "white"
                }}
              >
                <option value="">Select a game...</option>
                {games.map((game) => (
                  <option key={game.id} value={game.id}>
                    {game.name}
                  </option>
                ))}
              </select>
            </div>

            <button
              type="submit"
              disabled={!selectedGameId || isLoadingGuide}
              style={{
                padding: "12px 24px",
                background: !selectedGameId || isLoadingGuide ? "#dadce0" : "#34a853",
                color: "white",
                border: "none",
                borderRadius: 4,
                fontSize: 14,
                fontWeight: 500,
                cursor: !selectedGameId || isLoadingGuide ? "not-allowed" : "pointer"
              }}
            >
              {isLoadingGuide ? "Generating..." : "Generate Setup Guide"}
            </button>
          </form>
        </div>

        {/* Error Message */}
        {errorMessage && (
          <div
            style={{
              margin: "0 0 24px 0",
              padding: 16,
              background: "#fce8e6",
              color: "#d93025",
              borderRadius: 8,
              fontSize: 14,
              border: "1px solid #f5c6cb"
            }}
          >
            {errorMessage}
          </div>
        )}

        {/* Loading State */}
        {isLoadingGuide && (
          <div
            style={{
              background: "white",
              border: "1px solid #dadce0",
              borderRadius: 8,
              padding: 48,
              textAlign: "center"
            }}
          >
            <div style={{ fontSize: 16, color: "#94a3b8", marginBottom: 16 }}>
              Generating your setup guide...
            </div>
            <div style={{ fontSize: 14, color: "#9aa0a6" }}>
              This may take a few moments while we retrieve the best instructions from the rulebook.
            </div>
          </div>
        )}

        {/* Setup Guide Content */}
        {setupGuide && !isLoadingGuide && (
          <>
            {/* Progress Card */}
            <div
              style={{
                background: "white",
                border: "1px solid #dadce0",
                borderRadius: 8,
                padding: 24,
                marginBottom: 24
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
                <div>
                  <h2 style={{ margin: "0 0 8px 0", fontSize: 20, fontWeight: 600 }}>
                    {setupGuide.gameTitle}
                  </h2>
                  <div style={{ fontSize: 14, color: "#94a3b8" }}>
                    Estimated setup time: {setupGuide.estimatedSetupTimeMinutes} minutes
                  </div>
                </div>
                <button
                  onClick={resetProgress}
                  disabled={completedSteps.size === 0}
                  style={{
                    padding: "8px 16px",
                    background: completedSteps.size === 0 ? "#e8eaed" : "#ea4335",
                    color: completedSteps.size === 0 ? "#9aa0a6" : "white",
                    border: "none",
                    borderRadius: 4,
                    fontSize: 13,
                    fontWeight: 500,
                    cursor: completedSteps.size === 0 ? "not-allowed" : "pointer"
                  }}
                >
                  Reset Progress
                </button>
              </div>

              {/* Progress Bar */}
              <div>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                  <span style={{ fontSize: 14, fontWeight: 500, color: "#202124" }}>
                    Progress: {completedSteps.size} / {setupGuide.steps.length} steps
                  </span>
                  <span style={{ fontSize: 14, fontWeight: 500, color: "#34a853" }}>
                    {progressPercentage}%
                  </span>
                </div>
                <div
                  style={{
                    width: "100%",
                    height: 8,
                    background: "#e8eaed",
                    borderRadius: 4,
                    overflow: "hidden"
                  }}
                >
                  <div
                    style={{
                      width: `${progressPercentage}%`,
                      height: "100%",
                      background: progressPercentage === 100 ? "#34a853" : "#1a73e8",
                      transition: "width 0.3s ease"
                    }}
                  />
                </div>
              </div>

              {/* Completion Message */}
              {progressPercentage === 100 && (
                <div
                  style={{
                    marginTop: 16,
                    padding: 16,
                    background: "#e6f4ea",
                    border: "1px solid #34a853",
                    borderRadius: 6,
                    fontSize: 14,
                    color: "#137333",
                    display: "flex",
                    alignItems: "center",
                    gap: 12
                  }}
                >
                  <span style={{ fontSize: 24 }}>🎉</span>
                  <span>
                    <strong>Setup Complete!</strong> Your game is ready to play. Have fun!
                  </span>
                </div>
              )}

              {/* AI Confidence */}
              {setupGuide.confidence !== null && (
                <div style={{ marginTop: 12, fontSize: 12, color: "#94a3b8" }}>
                  AI Confidence: {Math.round(setupGuide.confidence * 100)}%
                </div>
              )}
            </div>

            {/* Steps List */}
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              {setupGuide.steps.map((step) => (
                <SetupStepCard
                  key={step.stepNumber}
                  step={step}
                  isCompleted={completedSteps.has(step.stepNumber)}
                  onToggleComplete={() => toggleStepCompletion(step.stepNumber)}
                  onViewReferences={() => setSelectedStepReferences(step.references)}
                />
              ))}
            </div>
          </>
        )}

        {/* Empty State */}
        {!setupGuide && !isLoadingGuide && !errorMessage && (
          <div
            style={{
              background: "white",
              border: "1px dashed #dadce0",
              borderRadius: 8,
              padding: 48,
              textAlign: "center"
            }}
          >
            <div style={{ fontSize: 48, marginBottom: 16 }}>🎲</div>
            <h3 style={{ margin: "0 0 8px 0", fontSize: 18, color: "#202124" }}>
              No Setup Guide Yet
            </h3>
            <p style={{ margin: 0, fontSize: 14, color: "#94a3b8" }}>
              Select a game and click &quot;Generate Setup Guide&quot; to get started with step-by-step instructions.
            </p>
          </div>
        )}
      </div>

      {/* Citation Modal */}
      {selectedStepReferences && (
        <CitationModal
          snippets={selectedStepReferences}
          onClose={() => setSelectedStepReferences(null)}
        />
      )}
    </main>
  );
}
