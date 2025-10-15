import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../lib/api";

type RuleAtom = {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
};

type RuleSpec = {
  gameId: string;
  version: string;
  createdAt: string;
  rules: RuleAtom[];
};

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

type HistoryEntry = {
  content: string;
  timestamp: number;
};

export default function RuleSpecEditor() {
  const router = useRouter();
  const { gameId } = router.query;

  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [ruleSpec, setRuleSpec] = useState<RuleSpec | null>(null);
  const [jsonContent, setJsonContent] = useState<string>("");
  const [isValid, setIsValid] = useState<boolean>(true);
  const [validationError, setValidationError] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  // Undo/Redo state
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [historyIndex, setHistoryIndex] = useState<number>(-1);

  const initializeHistory = useCallback((content: string) => {
    const entry: HistoryEntry = { content, timestamp: Date.now() };
    setHistory([entry]);
    setHistoryIndex(0);
  }, []);

  const validateJson = useCallback((content: string) => {
    try {
      const parsed = JSON.parse(content);

      // Basic validation against RuleSpec schema
      if (!parsed.gameId || typeof parsed.gameId !== "string") {
        throw new Error("gameId è richiesto e deve essere una stringa");
      }
      if (!parsed.version || typeof parsed.version !== "string") {
        throw new Error("version è richiesto e deve essere una stringa");
      }
      if (!parsed.createdAt || typeof parsed.createdAt !== "string") {
        throw new Error("createdAt è richiesto e deve essere una stringa");
      }
      if (!Array.isArray(parsed.rules)) {
        throw new Error("rules deve essere un array");
      }

      // Validate each rule atom
      for (let i = 0; i < parsed.rules.length; i++) {
        const rule = parsed.rules[i];
        if (!rule.id || typeof rule.id !== "string") {
          throw new Error(`rules[${i}].id è richiesto e deve essere una stringa`);
        }
        if (!rule.text || typeof rule.text !== "string") {
          throw new Error(`rules[${i}].text è richiesto e deve essere una stringa`);
        }
      }

      setIsValid(true);
      setValidationError("");
    } catch (err: any) {
      setIsValid(false);
      setValidationError(err?.message || "JSON non valido");
    }
  }, []);

  const loadCurrentUser = useCallback(async () => {
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
  }, []);

  const loadRuleSpec = useCallback(
    async (gId: string) => {
      setIsLoading(true);
      setErrorMessage("");
      try {
        const spec = await api.get<RuleSpec>(`/api/v1/games/${gId}/rulespec`);
        if (spec) {
          setRuleSpec(spec);
          const formatted = JSON.stringify(spec, null, 2);
          setJsonContent(formatted);
          initializeHistory(formatted);
          validateJson(formatted);
        } else {
          setErrorMessage("RuleSpec non trovato per questo gioco.");
        }
      } catch (err: any) {
        console.error(err);
        setErrorMessage(err?.message || "Impossibile caricare RuleSpec.");
      } finally {
        setIsLoading(false);
      }
    },
    [initializeHistory, validateJson]
  );

  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  useEffect(() => {
    if (authUser && gameId && typeof gameId === "string") {
      void loadRuleSpec(gameId);
    }
  }, [authUser, gameId, loadRuleSpec]);

  const addToHistory = (content: string) => {
    // Remove any history entries after the current index
    const newHistory = history.slice(0, historyIndex + 1);
    const entry: HistoryEntry = { content, timestamp: Date.now() };
    newHistory.push(entry);
    setHistory(newHistory);
    setHistoryIndex(newHistory.length - 1);
  };

  const handleUndo = () => {
    if (historyIndex > 0) {
      const newIndex = historyIndex - 1;
      setHistoryIndex(newIndex);
      const previousContent = history[newIndex].content;
      setJsonContent(previousContent);
      validateJson(previousContent);
    }
  };

  const handleRedo = () => {
    if (historyIndex < history.length - 1) {
      const newIndex = historyIndex + 1;
      setHistoryIndex(newIndex);
      const nextContent = history[newIndex].content;
      setJsonContent(nextContent);
      validateJson(nextContent);
    }
  };

  const handleJsonChange = (newContent: string) => {
    setJsonContent(newContent);
    validateJson(newContent);
  };

  const handleJsonBlur = () => {
    // Add to history when user finishes editing (on blur)
    if (history.length === 0 || jsonContent !== history[historyIndex].content) {
      addToHistory(jsonContent);
    }
  };

  const handleSave = async () => {
    if (!isValid) {
      setErrorMessage("Impossibile salvare: JSON non valido");
      return;
    }

    if (!gameId || typeof gameId !== "string") {
      setErrorMessage("gameId mancante");
      return;
    }

    try {
      const parsed: RuleSpec = JSON.parse(jsonContent);
      setIsSaving(true);
      setErrorMessage("");
      setStatusMessage("");

      const updated = await api.put<RuleSpec>(`/api/v1/games/${gameId}/rulespec`, parsed);
      setRuleSpec(updated);
      setStatusMessage(`RuleSpec salvato con successo (versione ${updated.version})`);
    } catch (err: any) {
      console.error(err);
      setErrorMessage(err?.message || "Impossibile salvare RuleSpec");
    } finally {
      setIsSaving(false);
    }
  };

  const canUndo = historyIndex > 0;
  const canRedo = historyIndex < history.length - 1;

  if (!authUser) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif" }}>
        <h1>Editor RuleSpec</h1>
        <p>Devi effettuare l&apos;accesso per utilizzare l&apos;editor.</p>
        <Link href="/" style={{ color: "#0070f3" }}>
          Torna alla home
        </Link>
      </main>
    );
  }

  if (authUser.role !== "Admin" && authUser.role !== "Editor") {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif" }}>
        <h1>Editor RuleSpec</h1>
        <p>Non hai i permessi necessari per utilizzare l&apos;editor.</p>
        <Link href="/" style={{ color: "#0070f3" }}>
          Torna alla home
        </Link>
      </main>
    );
  }

  if (!gameId) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif" }}>
        <h1>Editor RuleSpec</h1>
        <p>Specifica un gameId nella query string: ?gameId=demo-chess</p>
        <Link href="/" style={{ color: "#0070f3" }}>
          Torna alla home
        </Link>
      </main>
    );
  }

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>Editor RuleSpec</h1>
          <p style={{ margin: "8px 0 0 0", color: "#666" }}>
            Game: <strong>{gameId}</strong>
          </p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <Link
            href={`/versions?gameId=${gameId}`}
            style={{
              padding: "8px 16px",
              background: "#ff9800",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Storico Versioni
          </Link>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#666",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Home
          </Link>
        </div>
      </div>

      {statusMessage && (
        <div style={{ padding: 12, background: "#e7f5e7", border: "1px solid #4caf50", borderRadius: 4, marginBottom: 16 }}>
          {statusMessage}
        </div>
      )}

      {errorMessage && (
        <div style={{ padding: 12, background: "#fce4e4", border: "1px solid #d93025", borderRadius: 4, marginBottom: 16 }}>
          {errorMessage}
        </div>
      )}

      {isLoading ? (
        <p>Caricamento...</p>
      ) : (
        <div style={{ display: "flex", gap: 24 }}>
          <div style={{ flex: "1 1 50%", display: "flex", flexDirection: "column" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
              <h2 style={{ margin: 0 }}>Editor JSON</h2>
              <div style={{ display: "flex", gap: 8 }}>
                <button
                  onClick={handleUndo}
                  disabled={!canUndo}
                  style={{
                    padding: "6px 12px",
                    background: canUndo ? "#0070f3" : "#ccc",
                    color: "white",
                    border: "none",
                    borderRadius: 4,
                    cursor: canUndo ? "pointer" : "not-allowed",
                    fontSize: 14
                  }}
                  title="Annulla (Ctrl+Z)"
                >
                  ← Annulla
                </button>
                <button
                  onClick={handleRedo}
                  disabled={!canRedo}
                  style={{
                    padding: "6px 12px",
                    background: canRedo ? "#0070f3" : "#ccc",
                    color: "white",
                    border: "none",
                    borderRadius: 4,
                    cursor: canRedo ? "pointer" : "not-allowed",
                    fontSize: 14
                  }}
                  title="Ripeti (Ctrl+Y)"
                >
                  Ripeti →
                </button>
                <button
                  onClick={handleSave}
                  disabled={!isValid || isSaving}
                  style={{
                    padding: "6px 16px",
                    background: isValid && !isSaving ? "#4caf50" : "#ccc",
                    color: "white",
                    border: "none",
                    borderRadius: 4,
                    cursor: isValid && !isSaving ? "pointer" : "not-allowed",
                    fontSize: 14,
                    fontWeight: "bold"
                  }}
                >
                  {isSaving ? "Salvataggio..." : "Salva"}
                </button>
              </div>
            </div>

            <div
              style={{
                padding: 8,
                background: isValid ? "#e7f5e7" : "#fce4e4",
                border: `1px solid ${isValid ? "#4caf50" : "#d93025"}`,
                borderRadius: 4,
                marginBottom: 8,
                fontSize: 14
              }}
            >
              {isValid ? "✓ JSON valido" : `✗ ${validationError}`}
            </div>

            <textarea
              value={jsonContent}
              onChange={(e) => handleJsonChange(e.target.value)}
              onBlur={handleJsonBlur}
              style={{
                flex: 1,
                minHeight: 600,
                fontFamily: "monospace",
                fontSize: 14,
                padding: 12,
                border: `2px solid ${isValid ? "#ccc" : "#d93025"}`,
                borderRadius: 4,
                resize: "vertical"
              }}
              spellCheck={false}
            />
          </div>

          <div style={{ flex: "1 1 50%", display: "flex", flexDirection: "column" }}>
            <h2 style={{ marginTop: 0 }}>Preview</h2>
            <div
              style={{
                flex: 1,
                padding: 16,
                background: "#f9f9f9",
                border: "1px solid #ccc",
                borderRadius: 4,
                overflowY: "auto",
                minHeight: 600
              }}
            >
              {isValid && jsonContent ? (
                <RuleSpecPreview ruleSpec={JSON.parse(jsonContent)} />
              ) : (
                <p style={{ color: "#999" }}>Correggi gli errori per visualizzare l&apos;anteprima</p>
              )}
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function RuleSpecPreview({ ruleSpec }: { ruleSpec: RuleSpec }) {
  return (
    <div>
      <div style={{ marginBottom: 24 }}>
        <h3 style={{ marginTop: 0, marginBottom: 8 }}>Informazioni Gioco</h3>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <tbody>
            <tr>
              <td style={{ padding: "4px 8px", fontWeight: "bold", width: 120 }}>Game ID:</td>
              <td style={{ padding: "4px 8px" }}>{ruleSpec.gameId}</td>
            </tr>
            <tr>
              <td style={{ padding: "4px 8px", fontWeight: "bold" }}>Versione:</td>
              <td style={{ padding: "4px 8px" }}>{ruleSpec.version}</td>
            </tr>
            <tr>
              <td style={{ padding: "4px 8px", fontWeight: "bold" }}>Creato:</td>
              <td style={{ padding: "4px 8px" }}>
                {new Date(ruleSpec.createdAt).toLocaleString()}
              </td>
            </tr>
            <tr>
              <td style={{ padding: "4px 8px", fontWeight: "bold" }}>N. Regole:</td>
              <td style={{ padding: "4px 8px" }}>{ruleSpec.rules.length}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div>
        <h3 style={{ marginBottom: 12 }}>Regole</h3>
        {ruleSpec.rules.map((rule, index) => (
          <div
            key={rule.id}
            style={{
              marginBottom: 16,
              padding: 12,
              background: "white",
              border: "1px solid #ddd",
              borderRadius: 4
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
              <strong style={{ color: "#0070f3" }}>
                {index + 1}. {rule.id}
              </strong>
              <div style={{ fontSize: 12, color: "#666" }}>
                {rule.section && <span style={{ marginRight: 12 }}>Sezione: {rule.section}</span>}
                {rule.page && <span style={{ marginRight: 12 }}>Pag. {rule.page}</span>}
                {rule.line && <span>Riga {rule.line}</span>}
              </div>
            </div>
            <p style={{ margin: 0, lineHeight: 1.6 }}>{rule.text}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
