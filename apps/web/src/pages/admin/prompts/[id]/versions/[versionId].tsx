import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../../../../../lib/api";
import PromptEditor from "../../../../../components/PromptEditor";

type PromptVersion = {
  id: string;
  templateId: string;
  versionNumber: number;
  content: string;
  metadata?: Record<string, any>;
  isActive: boolean;
  createdById: string;
  createdByEmail: string;
  createdAt: string;
};

type ToastState = {
  show: boolean;
  message: string;
  type: "success" | "error";
};

export default function PromptVersionDetail() {
  const router = useRouter();
  const { id, versionId } = router.query;

  const [version, setVersion] = useState<PromptVersion | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState>({ show: false, message: "", type: "success" });

  const showToast = useCallback((message: string, type: "success" | "error") => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: "", type: "success" }), 5000);
  }, []);

  const fetchVersion = useCallback(async () => {
    if (!id || !versionId) return;

    setLoading(true);
    setError(null);

    try {
      const result = await api.get<PromptVersion>(`/api/v1/admin/prompts/${id}/versions/${versionId}`);
      if (!result) throw new Error("Unauthorized");

      setVersion(result);
    } catch (err: any) {
      setError(err.message || "Failed to fetch version");
      showToast("Failed to fetch version", "error");
    } finally {
      setLoading(false);
    }
  }, [id, versionId, showToast]);

  useEffect(() => {
    fetchVersion();
  }, [fetchVersion]);

  const handleActivate = async () => {
    if (!id || !versionId) return;

    try {
      await api.post(`/api/v1/admin/prompts/${id}/versions/${versionId}/activate`, {});
      showToast("Version activated successfully", "success");
      fetchVersion();
    } catch (err: any) {
      showToast(err.message || "Failed to activate version", "error");
    }
  };

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <div style={{ textAlign: "center", color: "#6b7280" }}>Loading...</div>
      </div>
    );
  }

  if (error || !version) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ color: "#991b1b", marginBottom: "1rem", fontSize: "1.125rem" }}>
            {error || "Version not found"}
          </div>
          <Link href={`/admin/prompts/${id}`}>
            <button style={{ padding: "0.5rem 1rem", background: "#667eea", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
              Back to Template
            </button>
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div style={{ maxWidth: "1400px", margin: "0 auto", padding: "2rem" }}>
        <div style={{ background: "white", borderRadius: "12px", boxShadow: "0 20px 60px rgba(0,0,0,0.3)", overflow: "hidden" }}>
          {/* Header */}
          <div style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", padding: "2rem", color: "white" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "1rem" }}>
              <Link href={`/admin/prompts/${id}`}>
                <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                  ← Back to Template
                </button>
              </Link>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                {!version.isActive && (
                  <button
                    onClick={handleActivate}
                    style={{ padding: "0.5rem 1.5rem", background: "white", color: "#667eea", border: "none", borderRadius: "8px", cursor: "pointer", fontWeight: "600" }}
                  >
                    Activate Version
                  </button>
                )}
                <Link href={`/admin/prompts/${id}/compare?versions=${versionId}`}>
                  <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                    Compare
                  </button>
                </Link>
              </div>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
              <h1 style={{ fontSize: "2rem", fontWeight: "bold" }}>Version {version.versionNumber}</h1>
              {version.isActive && (
                <span style={{ padding: "0.5rem 1rem", background: "rgba(16, 185, 129, 0.2)", color: "#d1fae5", borderRadius: "9999px", fontSize: "0.875rem", fontWeight: "600" }}>
                  ✓ Active
                </span>
              )}
            </div>
            <div style={{ display: "flex", gap: "2rem", marginTop: "1rem", fontSize: "0.875rem", opacity: 0.9 }}>
              <span>Created by: <strong>{version.createdByEmail}</strong></span>
              <span>Created: <strong>{new Date(version.createdAt).toLocaleString()}</strong></span>
            </div>
          </div>

          {/* Content */}
          <div style={{ padding: "2rem" }}>
            <div style={{ marginBottom: "2rem" }}>
              <h2 style={{ fontSize: "1.25rem", fontWeight: "600", marginBottom: "1rem" }}>Prompt Content</h2>
              <PromptEditor
                value={version.content}
                readonly={true}
                height="600px"
              />
            </div>

            {version.metadata && Object.keys(version.metadata).length > 0 && (
              <div>
                <h2 style={{ fontSize: "1.25rem", fontWeight: "600", marginBottom: "1rem" }}>Metadata</h2>
                <pre
                  style={{
                    background: "#f9fafb",
                    padding: "1.5rem",
                    borderRadius: "8px",
                    border: "1px solid #e5e7eb",
                    overflow: "auto",
                    fontSize: "0.875rem",
                    fontFamily: "monospace",
                  }}
                >
                  {JSON.stringify(version.metadata, null, 2)}
                </pre>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Toast */}
      {toast.show && (
        <div
          style={{
            position: "fixed",
            bottom: "2rem",
            right: "2rem",
            padding: "1rem 1.5rem",
            background: toast.type === "success" ? "#10b981" : "#ef4444",
            color: "white",
            borderRadius: "8px",
            boxShadow: "0 10px 40px rgba(0,0,0,0.3)",
            zIndex: 100,
            fontWeight: "500",
          }}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}
