import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../lib/api";
import { CommentThread } from "../components/CommentThread";

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

type RuleSpecVersion = {
  version: string;
  createdAt: string;
  ruleCount: number;
  createdBy?: string | null;
};

type RuleSpecHistory = {
  gameId: string;
  versions: RuleSpecVersion[];
  totalVersions: number;
};

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

type FieldChange = {
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
};

type ChangeType = "Added" | "Modified" | "Deleted" | "Unchanged";

type RuleAtomChange = {
  type: ChangeType;
  oldAtom?: string | null;
  newAtom?: string | null;
  oldValue?: RuleAtom | null;
  newValue?: RuleAtom | null;
  fieldChanges?: FieldChange[] | null;
};

type DiffSummary = {
  totalChanges: number;
  added: number;
  modified: number;
  deleted: number;
  unchanged: number;
};

type RuleSpecDiff = {
  gameId: string;
  fromVersion: string;
  toVersion: string;
  fromCreatedAt: string;
  toCreatedAt: string;
  summary: DiffSummary;
  changes: RuleAtomChange[];
};

export default function VersionHistory() {
  const router = useRouter();
  const { gameId } = router.query;

  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [history, setHistory] = useState<RuleSpecHistory | null>(null);
  const [selectedFromVersion, setSelectedFromVersion] = useState<string>("");
  const [selectedToVersion, setSelectedToVersion] = useState<string>("");
  const [diff, setDiff] = useState<RuleSpecDiff | null>(null);
  const [isLoadingHistory, setIsLoadingHistory] = useState<boolean>(false);
  const [isLoadingDiff, setIsLoadingDiff] = useState<boolean>(false);
  const [isRestoring, setIsRestoring] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string>("");
  const [showOnlyChanges, setShowOnlyChanges] = useState<boolean>(true);

  const loadCurrentUser = useCallback(async () => {
    try {
      const res = await api.get<AuthResponse>("/auth/me");
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  }, []);

  const loadHistory = useCallback(async (gId: string) => {
    setIsLoadingHistory(true);
    setErrorMessage("");
    try {
      const historyData = await api.get<RuleSpecHistory>(`/games/${gId}/rulespec/history`);
      if (historyData) {
        setHistory(historyData);
        // Auto-select the two most recent versions for diff
        if (historyData.versions.length >= 2) {
          setSelectedFromVersion(historyData.versions[1].version);
          setSelectedToVersion(historyData.versions[0].version);
        }
      }
    } catch (err: any) {
      console.error(err);
      setErrorMessage(err?.message || "Impossibile caricare lo storico versioni.");
    } finally {
      setIsLoadingHistory(false);
    }
  }, []);

  const loadDiff = useCallback(async () => {
    if (!gameId || typeof gameId !== "string" || !selectedFromVersion || !selectedToVersion) {
      return;
    }

    setIsLoadingDiff(true);
    setErrorMessage("");
    setDiff(null);
    try {
      const diffData = await api.get<RuleSpecDiff>(
        `/games/${gameId}/rulespec/diff?from=${encodeURIComponent(selectedFromVersion)}&to=${encodeURIComponent(selectedToVersion)}`
      );
      if (diffData) {
        setDiff(diffData);
      }
    } catch (err: any) {
      console.error(err);
      setErrorMessage(err?.message || "Impossibile caricare il diff.");
    } finally {
      setIsLoadingDiff(false);
    }
  }, [gameId, selectedFromVersion, selectedToVersion]);

  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  useEffect(() => {
    if (authUser && gameId && typeof gameId === "string") {
      void loadHistory(gameId);
    }
  }, [authUser, gameId, loadHistory]);

  const handleRestoreVersion = async (version: string) => {
    if (!gameId || typeof gameId !== "string") {
      setErrorMessage("gameId mancante");
      return;
    }

    const confirmed = confirm(`Sei sicuro di voler ripristinare la versione ${version}? Questo creerà una nuova versione.`);
    if (!confirmed) {
      return;
    }

    setIsRestoring(true);
    setErrorMessage("");
    setStatusMessage("");
    try {
      // Get the version to restore
      const versionData = await api.get<RuleSpec>(`/games/${gameId}/rulespec/versions/${version}`);
      if (!versionData) {
        throw new Error("Versione non trovata");
      }

      // Save it as a new version
      const updated = await api.put<RuleSpec>(`/games/${gameId}/rulespec`, versionData);
      setStatusMessage(`Versione ${version} ripristinata con successo come versione ${updated.version}`);

      // Reload history
      await loadHistory(gameId);
    } catch (err: any) {
      console.error(err);
      setErrorMessage(err?.message || "Impossibile ripristinare la versione");
    } finally {
      setIsRestoring(false);
    }
  };

  useEffect(() => {
    if (selectedFromVersion && selectedToVersion) {
      void loadDiff();
    }
  }, [loadDiff, selectedFromVersion, selectedToVersion]);

  if (!authUser) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif" }}>
        <h1>Storico Versioni RuleSpec</h1>
        <p>Devi effettuare l&apos;accesso per visualizzare lo storico.</p>
        <Link href="/" style={{ color: "#0070f3" }}>
          Torna alla home
        </Link>
      </main>
    );
  }

  if (!gameId) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif" }}>
        <h1>Storico Versioni RuleSpec</h1>
        <p>Specifica un gameId nella query string: ?gameId=demo-chess</p>
        <Link href="/" style={{ color: "#0070f3" }}>
          Torna alla home
        </Link>
      </main>
    );
  }

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1600, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>Storico Versioni RuleSpec</h1>
          <p style={{ margin: "8px 0 0 0", color: "#666" }}>
            Game: <strong>{gameId}</strong>
          </p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <Link
            href={`/editor?gameId=${gameId}`}
            style={{
              padding: "8px 16px",
              background: "#0070f3",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Editor
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

      {isLoadingHistory ? (
        <p>Caricamento storico...</p>
      ) : (
        <div style={{ display: "grid", gridTemplateColumns: "300px 1fr", gap: 24 }}>
          {/* Left sidebar: Version list */}
          <div>
            <h2 style={{ marginTop: 0 }}>Versioni ({history?.totalVersions || 0})</h2>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {history?.versions.map((version, index) => (
                <div
                  key={version.version}
                  style={{
                    padding: 12,
                    background: "white",
                    border: "1px solid #ddd",
                    borderRadius: 4,
                    fontSize: 14
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                    <strong style={{ color: index === 0 ? "#4caf50" : "#333" }}>
                      {version.version}
                      {index === 0 && <span style={{ marginLeft: 8, fontSize: 12, color: "#4caf50" }}>(corrente)</span>}
                    </strong>
                  </div>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 8 }}>
                    {new Date(version.createdAt).toLocaleString()}
                  </div>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 8 }}>
                    {version.ruleCount} regole
                  </div>
                  {authUser.role === "Admin" || authUser.role === "Editor" ? (
                    <button
                      onClick={() => handleRestoreVersion(version.version)}
                      disabled={isRestoring || index === 0}
                      style={{
                        width: "100%",
                        padding: "6px 12px",
                        background: index === 0 || isRestoring ? "#ccc" : "#ff9800",
                        color: "white",
                        border: "none",
                        borderRadius: 4,
                        cursor: index === 0 || isRestoring ? "not-allowed" : "pointer",
                        fontSize: 12
                      }}
                    >
                      {isRestoring ? "Ripristino..." : "Ripristina"}
                    </button>
                  ) : null}
                </div>
              ))}
            </div>
          </div>

          {/* Right content: Diff viewer */}
          <div>
            <div style={{ marginBottom: 16 }}>
              <h2 style={{ marginTop: 0, marginBottom: 12 }}>Confronta Versioni</h2>
              <div style={{ display: "flex", gap: 12, alignItems: "center", marginBottom: 12 }}>
                <div style={{ flex: 1 }}>
                  <label htmlFor="from-version" style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: "bold" }}>
                    Da versione:
                  </label>
                  <select
                    id="from-version"
                    value={selectedFromVersion}
                    onChange={(e) => setSelectedFromVersion(e.target.value)}
                    style={{
                      width: "100%",
                      padding: 8,
                      border: "1px solid #ccc",
                      borderRadius: 4,
                      fontSize: 14
                    }}
                  >
                    <option value="">Seleziona versione</option>
                    {history?.versions.map((version) => (
                      <option key={version.version} value={version.version}>
                        {version.version} - {new Date(version.createdAt).toLocaleString()}
                      </option>
                    ))}
                  </select>
                </div>
                <div style={{ flex: 1 }}>
                  <label htmlFor="to-version" style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: "bold" }}>
                    A versione:
                  </label>
                  <select
                    id="to-version"
                    value={selectedToVersion}
                    onChange={(e) => setSelectedToVersion(e.target.value)}
                    style={{
                      width: "100%",
                      padding: 8,
                      border: "1px solid #ccc",
                      borderRadius: 4,
                      fontSize: 14
                    }}
                  >
                    <option value="">Seleziona versione</option>
                    {history?.versions.map((version) => (
                      <option key={version.version} value={version.version}>
                        {version.version} - {new Date(version.createdAt).toLocaleString()}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div style={{ marginBottom: 12 }}>
                <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 14, cursor: "pointer" }}>
                  <input
                    type="checkbox"
                    checked={showOnlyChanges}
                    onChange={(e) => setShowOnlyChanges(e.target.checked)}
                  />
                  Mostra solo modifiche
                </label>
              </div>
            </div>

            {isLoadingDiff ? (
              <p>Caricamento diff...</p>
            ) : diff ? (
              <>
                <DiffViewer diff={diff} showOnlyChanges={showOnlyChanges} />

                {/* Comments section for the selected "to" version */}
                {selectedToVersion && authUser && (
                  <div style={{ marginTop: 32, borderTop: "2px solid #ddd", paddingTop: 24 }}>
                    <CommentThread
                      gameId={gameId as string}
                      version={selectedToVersion}
                      currentUserId={authUser.id}
                      currentUserRole={authUser.role}
                    />
                  </div>
                )}
              </>
            ) : (
              <p style={{ color: "#999" }}>Seleziona due versioni per visualizzare le differenze</p>
            )}
          </div>
        </div>
      )}
    </main>
  );
}

function DiffViewer({ diff, showOnlyChanges }: { diff: RuleSpecDiff; showOnlyChanges: boolean }) {
  const changesToShow = showOnlyChanges
    ? diff.changes.filter((c) => c.type !== "Unchanged")
    : diff.changes;

  return (
    <div>
      <div
        style={{
          padding: 16,
          background: "#f5f5f5",
          border: "1px solid #ddd",
          borderRadius: 4,
          marginBottom: 16
        }}
      >
        <h3 style={{ marginTop: 0, marginBottom: 12 }}>Riepilogo Modifiche</h3>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 12, fontSize: 14 }}>
          <div style={{ padding: 8, background: "#e7f5e7", borderRadius: 4, textAlign: "center" }}>
            <div style={{ fontWeight: "bold", color: "#4caf50" }}>+{diff.summary.added}</div>
            <div style={{ fontSize: 12, color: "#666" }}>Aggiunte</div>
          </div>
          <div style={{ padding: 8, background: "#fff3e0", borderRadius: 4, textAlign: "center" }}>
            <div style={{ fontWeight: "bold", color: "#ff9800" }}>~{diff.summary.modified}</div>
            <div style={{ fontSize: 12, color: "#666" }}>Modificate</div>
          </div>
          <div style={{ padding: 8, background: "#fce4e4", borderRadius: 4, textAlign: "center" }}>
            <div style={{ fontWeight: "bold", color: "#d93025" }}>-{diff.summary.deleted}</div>
            <div style={{ fontSize: 12, color: "#666" }}>Eliminate</div>
          </div>
          <div style={{ padding: 8, background: "#f0f0f0", borderRadius: 4, textAlign: "center" }}>
            <div style={{ fontWeight: "bold", color: "#666" }}>{diff.summary.unchanged}</div>
            <div style={{ fontSize: 12, color: "#666" }}>Non modificate</div>
          </div>
        </div>
      </div>

      <h3>Modifiche ({changesToShow.length})</h3>
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        {changesToShow.map((change, index) => (
          <ChangeItem key={index} change={change} />
        ))}
      </div>
    </div>
  );
}

function ChangeItem({ change }: { change: RuleAtomChange }) {
  const getBgColor = () => {
    switch (change.type) {
      case "Added":
        return "#e7f5e7";
      case "Modified":
        return "#fff3e0";
      case "Deleted":
        return "#fce4e4";
      case "Unchanged":
        return "#f9f9f9";
      default:
        return "#f9f9f9";
    }
  };

  const getBorderColor = () => {
    switch (change.type) {
      case "Added":
        return "#4caf50";
      case "Modified":
        return "#ff9800";
      case "Deleted":
        return "#d93025";
      case "Unchanged":
        return "#ccc";
      default:
        return "#ccc";
    }
  };

  const getIcon = () => {
    switch (change.type) {
      case "Added":
        return "+ ";
      case "Modified":
        return "~ ";
      case "Deleted":
        return "- ";
      case "Unchanged":
        return "= ";
      default:
        return "";
    }
  };

  return (
    <div
      style={{
        padding: 16,
        background: getBgColor(),
        border: `2px solid ${getBorderColor()}`,
        borderRadius: 4
      }}
    >
      <div style={{ marginBottom: 12 }}>
        <strong style={{ fontSize: 16 }}>
          {getIcon()}
          {change.type}: {change.newAtom || change.oldAtom}
        </strong>
      </div>

      {change.type === "Added" && change.newValue && (
        <div>
          <div style={{ fontSize: 14, marginBottom: 8 }}>
            <strong>Testo:</strong> {change.newValue.text}
          </div>
          {change.newValue.section && (
            <div style={{ fontSize: 12, color: "#666" }}>
              Sezione: {change.newValue.section}
            </div>
          )}
          {change.newValue.page && (
            <div style={{ fontSize: 12, color: "#666" }}>
              Pagina: {change.newValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Deleted" && change.oldValue && (
        <div>
          <div style={{ fontSize: 14, marginBottom: 8, textDecoration: "line-through" }}>
            <strong>Testo:</strong> {change.oldValue.text}
          </div>
          {change.oldValue.section && (
            <div style={{ fontSize: 12, color: "#666", textDecoration: "line-through" }}>
              Sezione: {change.oldValue.section}
            </div>
          )}
          {change.oldValue.page && (
            <div style={{ fontSize: 12, color: "#666", textDecoration: "line-through" }}>
              Pagina: {change.oldValue.page}
            </div>
          )}
        </div>
      )}

      {change.type === "Modified" && change.fieldChanges && (
        <div>
          {change.fieldChanges.map((fieldChange, idx) => (
            <div key={idx} style={{ marginBottom: 12 }}>
              <div style={{ fontSize: 14, fontWeight: "bold", marginBottom: 4 }}>
                {fieldChange.fieldName}:
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div style={{ padding: 8, background: "#fce4e4", borderRadius: 4 }}>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 4 }}>Prima:</div>
                  <div style={{ fontSize: 14 }}>{fieldChange.oldValue || "(vuoto)"}</div>
                </div>
                <div style={{ padding: 8, background: "#e7f5e7", borderRadius: 4 }}>
                  <div style={{ fontSize: 12, color: "#666", marginBottom: 4 }}>Dopo:</div>
                  <div style={{ fontSize: 14 }}>{fieldChange.newValue || "(vuoto)"}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {change.type === "Unchanged" && change.oldValue && (
        <div>
          <div style={{ fontSize: 14, color: "#666" }}>
            {change.oldValue.text}
          </div>
        </div>
      )}
    </div>
  );
}
