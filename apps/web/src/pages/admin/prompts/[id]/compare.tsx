import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import dynamic from "next/dynamic";
import { api } from "../../../../lib/api";

// Dynamically import Monaco DiffEditor to avoid SSR issues
const DiffEditor = dynamic(
  () => import("@monaco-editor/react").then((mod) => mod.DiffEditor),
  { ssr: false }
);

type PromptVersion = {
  id: string;
  versionNumber: number;
  content: string;
  createdAt: string;
  createdByEmail: string;
};

export default function CompareVersions() {
  const router = useRouter();
  const { id } = router.query;

  const [versions, setVersions] = useState<PromptVersion[]>([]);
  const [version1Id, setVersion1Id] = useState<string>("");
  const [version2Id, setVersion2Id] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchVersions = useCallback(async () => {
    if (!id) return;

    setLoading(true);
    setError(null);

    try {
      const result = await api.get<PromptVersion[]>(`/api/v1/admin/prompts/${id}/versions`);
      if (!result) throw new Error("Unauthorized");

      setVersions(result);

      // Auto-select latest two versions if available
      if (result.length >= 2) {
        setVersion1Id(result[0].id);
        setVersion2Id(result[1].id);
      } else if (result.length === 1) {
        setVersion1Id(result[0].id);
      }

      // Check for pre-selected version from query params
      const { versions: queryVersions } = router.query;
      if (queryVersions && typeof queryVersions === "string") {
        setVersion2Id(queryVersions);
      }
    } catch (err: any) {
      setError(err.message || "Failed to fetch versions");
    } finally {
      setLoading(false);
    }
  }, [id, router.query]);

  useEffect(() => {
    fetchVersions();
  }, [fetchVersions]);

  const version1 = versions.find((v) => v.id === version1Id);
  const version2 = versions.find((v) => v.id === version2Id);

  const canCompare = version1 && version2 && version1.id !== version2.id;

  return (
    <div style={{ minHeight: "100vh", background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div style={{ maxWidth: "1600px", margin: "0 auto", padding: "2rem" }}>
        <div style={{ background: "white", borderRadius: "12px", boxShadow: "0 20px 60px rgba(0,0,0,0.3)", overflow: "hidden" }}>
          {/* Header */}
          <div style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", padding: "2rem", color: "white" }}>
            <div style={{ marginBottom: "1rem" }}>
              <Link href={`/admin/prompts/${id}`}>
                <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                  ← Back to Template
                </button>
              </Link>
            </div>
            <h1 style={{ fontSize: "2rem", fontWeight: "bold", marginBottom: "0.5rem" }}>Compare Versions</h1>
            <p style={{ opacity: 0.9 }}>Select two versions to compare side-by-side</p>
          </div>

          {/* Content */}
          <div style={{ padding: "2rem" }}>
            {loading && <div style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Loading...</div>}

            {error && (
              <div style={{ padding: "1rem", background: "#fee2e2", color: "#991b1b", borderRadius: "8px", marginBottom: "1rem" }}>
                {error}
              </div>
            )}

            {!loading && versions.length < 2 && (
              <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                <p style={{ fontSize: "1.125rem", marginBottom: "0.5rem" }}>Not enough versions to compare</p>
                <p style={{ fontSize: "0.875rem" }}>You need at least two versions to use the comparison feature</p>
              </div>
            )}

            {!loading && versions.length >= 2 && (
              <>
                {/* Version Selectors */}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "2rem", marginBottom: "2rem" }}>
                  <div>
                    <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "600" }}>Version A (Original)</label>
                    <select
                      value={version1Id}
                      onChange={(e) => setVersion1Id(e.target.value)}
                      style={{ width: "100%", padding: "0.75rem", border: "1px solid #d1d5db", borderRadius: "8px", fontSize: "1rem" }}
                    >
                      <option value="">Select a version...</option>
                      {versions.map((v) => (
                        <option key={v.id} value={v.id}>
                          Version {v.versionNumber} - {new Date(v.createdAt).toLocaleDateString()} by {v.createdByEmail}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "600" }}>Version B (Modified)</label>
                    <select
                      value={version2Id}
                      onChange={(e) => setVersion2Id(e.target.value)}
                      style={{ width: "100%", padding: "0.75rem", border: "1px solid #d1d5db", borderRadius: "8px", fontSize: "1rem" }}
                    >
                      <option value="">Select a version...</option>
                      {versions.map((v) => (
                        <option key={v.id} value={v.id}>
                          Version {v.versionNumber} - {new Date(v.createdAt).toLocaleDateString()} by {v.createdByEmail}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Diff View */}
                {canCompare && (
                  <div>
                    <div style={{ marginBottom: "1rem", padding: "1rem", background: "#f9fafb", borderRadius: "8px", border: "1px solid #e5e7eb" }}>
                      <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem", color: "#6b7280" }}>
                        <div>
                          <strong>Version {version1?.versionNumber}</strong> - {new Date(version1?.createdAt || "").toLocaleString()}
                        </div>
                        <div>
                          <strong>Version {version2?.versionNumber}</strong> - {new Date(version2?.createdAt || "").toLocaleString()}
                        </div>
                      </div>
                    </div>

                    <div style={{ border: "1px solid #e5e7eb", borderRadius: "8px", overflow: "hidden" }}>
                      <DiffEditor
                        height="700px"
                        language="markdown"
                        original={version1?.content || ""}
                        modified={version2?.content || ""}
                        theme="vs-light"
                        options={{
                          readOnly: true,
                          minimap: { enabled: false },
                          renderSideBySide: true,
                          fontSize: 14,
                          wordWrap: "on",
                          scrollBeyondLastLine: false,
                          automaticLayout: true,
                        }}
                        loading={
                          <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "700px" }}>
                            <div style={{ textAlign: "center", color: "#6b7280" }}>
                              Loading diff view...
                            </div>
                          </div>
                        }
                      />
                    </div>

                    <div style={{ marginTop: "1.5rem", padding: "1rem", background: "#eff6ff", borderRadius: "8px", fontSize: "0.875rem", color: "#1e40af" }}>
                      <strong>Legend:</strong> <span style={{ color: "#dc2626" }}>Red</span> indicates removed content, <span style={{ color: "#059669" }}>Green</span> indicates added content
                    </div>
                  </div>
                )}

                {!canCompare && version1Id && version2Id && version1Id === version2Id && (
                  <div style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    <p>Please select two different versions to compare</p>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
