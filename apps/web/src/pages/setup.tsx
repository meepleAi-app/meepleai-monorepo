import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";
import { cn } from "../lib/utils";

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
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-[1000] p-6"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-lg max-w-[800px] max-h-[80vh] overflow-auto p-6 shadow-[0_4px_24px_rgba(0,0,0,0.15)]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between items-center mb-4">
          <h3 className="m-0 text-lg font-semibold">References</h3>
          <button
            onClick={onClose}
            className="bg-transparent border-none text-2xl cursor-pointer text-slate-500 p-0 w-8 h-8 flex items-center justify-center"
            title="Close"
          >
            ×
          </button>
        </div>

        <div className="flex flex-col gap-3">
          {snippets.map((snippet, idx) => (
            <div
              key={idx}
              className="p-3 border border-gray-300 rounded-md bg-gray-50"
            >
              <div className="flex justify-between mb-2">
                <span className="font-medium text-sm text-gray-900">
                  {snippet.source}
                </span>
                <span className="text-xs text-slate-500">
                  Page {snippet.page}
                </span>
              </div>
              <div className="text-[13px] text-slate-500 leading-relaxed">
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
      className={cn(
        "border border-gray-300 rounded-lg p-5 transition-all duration-200 shadow-sm",
        isCompleted ? "bg-green-50" : "bg-white"
      )}
    >
      <div className="flex items-start gap-4">
        {/* Checkbox */}
        <input
          type="checkbox"
          checked={isCompleted}
          onChange={onToggleComplete}
          className="w-5 h-5 mt-0.5 cursor-pointer"
          style={{ accentColor: "#34a853" }}
          aria-label={`Mark step ${step.stepNumber} as ${isCompleted ? "incomplete" : "complete"}`}
        />

        {/* Content */}
        <div className="flex-1">
          {/* Header */}
          <div className="flex items-center gap-2 mb-2">
            <span className="font-semibold text-base text-gray-900">
              {step.stepNumber}. {step.title}
            </span>
            {step.isOptional && (
              <span className="text-[11px] font-medium px-2 py-0.5 bg-gray-200 text-slate-500 rounded-xl">
                OPTIONAL
              </span>
            )}
          </div>

          {/* Instruction */}
          <div
            className={cn(
              "text-sm text-slate-500 leading-relaxed mb-3",
              isCompleted && "line-through opacity-70"
            )}
          >
            {step.instruction}
          </div>

          {/* References */}
          {step.references.length > 0 && (
            <button
              onClick={onViewReferences}
              className="bg-transparent border border-blue-600 text-blue-600 px-3 py-1.5 rounded text-xs font-medium cursor-pointer flex items-center gap-1.5"
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
      <main className="p-6 max-w-[900px] mx-auto font-sans">
        <Link href="/" className="text-blue-500 no-underline">
          ← Back to Home
        </Link>
        <div className="mt-6 p-8 text-center border border-gray-300 rounded-lg">
          <h2>Accesso richiesto</h2>
          <p>Devi effettuare l'accesso per utilizzare la guida alla configurazione.</p>
          <Link
            href="/"
            className="inline-block mt-4 px-4 py-2 bg-blue-800 text-white no-underline rounded"
          >
            Vai al Login
          </Link>
        </div>
      </main>
    );
  }

  // Main setup guide interface
  return (
    <main className="min-h-screen bg-gray-50 font-sans">
      {/* Header */}
      <div className="bg-white border-b border-gray-300 p-4 sticky top-0 z-[100]">
        <div className="max-w-[900px] mx-auto flex justify-between items-center">
          <h1 className="m-0 text-2xl font-semibold">Game Setup Guide</h1>
          <Link
            href="/"
            className="px-4 py-2 bg-blue-600 text-white no-underline rounded text-sm font-medium"
          >
            Home
          </Link>
        </div>
      </div>

      {/* Content */}
      <div className="max-w-[900px] mx-auto p-6">
        {/* Game Selection Card */}
        <div className="bg-white border border-gray-300 rounded-lg p-6 mb-6">
          <h2 className="m-0 mb-4 text-lg font-semibold">Select Game</h2>

          <form onSubmit={loadSetupGuide} className="flex gap-3 items-end">
            <div className="flex-1">
              <label
                htmlFor="gameSelect"
                className="block mb-2 font-medium text-sm text-slate-500"
              >
                Game:
              </label>
              <select
                id="gameSelect"
                value={selectedGameId ?? ""}
                onChange={(e) => setSelectedGameId(e.target.value || null)}
                disabled={isLoadingGames || isLoadingGuide}
                className="w-full p-3 text-sm border border-gray-300 rounded bg-white"
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
              className={cn(
                "px-6 py-3 text-white border-none rounded text-sm font-medium",
                !selectedGameId || isLoadingGuide
                  ? "bg-gray-300 cursor-not-allowed"
                  : "bg-green-600 cursor-pointer"
              )}
            >
              {isLoadingGuide ? "Generating..." : "Generate Setup Guide"}
            </button>
          </form>
        </div>

        {/* Error Message */}
        {errorMessage && (
          <div className="mb-6 p-4 bg-red-50 text-red-600 rounded-lg text-sm border border-red-200">
            {errorMessage}
          </div>
        )}

        {/* Loading State */}
        {isLoadingGuide && (
          <div className="bg-white border border-gray-300 rounded-lg p-12 text-center">
            <div className="text-base text-slate-500 mb-4">
              Generating your setup guide...
            </div>
            <div className="text-sm text-slate-500">
              This may take a few moments while we retrieve the best instructions from the rulebook.
            </div>
          </div>
        )}

        {/* Setup Guide Content */}
        {setupGuide && !isLoadingGuide && (
          <>
            {/* Progress Card */}
            <div className="bg-white border border-gray-300 rounded-lg p-6 mb-6">
              <div className="flex justify-between items-center mb-4">
                <div>
                  <h2 className="m-0 mb-2 text-xl font-semibold">
                    {setupGuide.gameTitle}
                  </h2>
                  <div className="text-sm text-slate-500">
                    Estimated setup time: {setupGuide.estimatedSetupTimeMinutes} minutes
                  </div>
                </div>
                <button
                  onClick={resetProgress}
                  disabled={completedSteps.size === 0}
                  className={cn(
                    "px-4 py-2 border-none rounded text-[13px] font-medium",
                    completedSteps.size === 0
                      ? "bg-gray-200 text-slate-500 cursor-not-allowed"
                      : "bg-red-600 text-white cursor-pointer"
                  )}
                >
                  Reset Progress
                </button>
              </div>

              {/* Progress Bar */}
              <div>
                <div className="flex justify-between mb-2">
                  <span className="text-sm font-medium text-gray-900">
                    Progress: {completedSteps.size} / {setupGuide.steps.length} steps
                  </span>
                  <span className="text-sm font-medium text-green-600">
                    {progressPercentage}%
                  </span>
                </div>
                <div className="w-full h-2 bg-gray-200 rounded overflow-hidden">
                  <div
                    className="h-full transition-all duration-300"
                    style={{
                      width: `${progressPercentage}%`,
                      background: progressPercentage === 100 ? "#34a853" : "#1a73e8"
                    }}
                  />
                </div>
              </div>

              {/* Completion Message */}
              {progressPercentage === 100 && (
                <div className="mt-4 p-4 bg-green-50 border border-green-600 rounded-md text-sm text-green-800 flex items-center gap-3">
                  <span className="text-2xl">🎉</span>
                  <span>
                    <strong>Setup Complete!</strong> Your game is ready to play. Have fun!
                  </span>
                </div>
              )}

              {/* AI Confidence */}
              {setupGuide.confidence !== null && (
                <div className="mt-3 text-xs text-slate-500">
                  AI Confidence: {Math.round(setupGuide.confidence * 100)}%
                </div>
              )}
            </div>

            {/* Steps List */}
            <div className="flex flex-col gap-4">
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
          <div className="bg-white border border-dashed border-gray-300 rounded-lg p-12 text-center">
            <div className="text-5xl mb-4">🎲</div>
            <h3 className="m-0 mb-2 text-lg text-gray-900">
              No Setup Guide Yet
            </h3>
            <p className="m-0 text-sm text-slate-500">
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