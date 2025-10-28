import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../../../lib/api";
import PromptVersionCard from "../../../components/PromptVersionCard";

type PromptTemplate = {
  id: string;
  name: string;
  description: string;
  category: string;
  createdAt: string;
  updatedAt: string;
  activeVersionId?: string | null;
};

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

export default function PromptTemplateDetail() {
  const router = useRouter();
  const { id } = router.query;

  const [template, setTemplate] = useState<PromptTemplate | null>(null);
  const [versions, setVersions] = useState<PromptVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState>({ show: false, message: "", type: "success" });

  const showToast = useCallback((message: string, type: "success" | "error") => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: "", type: "success" }), 5000);
  }, []);

  const fetchTemplate = useCallback(async () => {
    if (!id) return;

    setLoading(true);
    setError(null);

    try {
      const [templateResult, versionsResult] = await Promise.all([
        api.get<PromptTemplate>(`/api/v1/admin/prompts/${id}`),
        api.get<PromptVersion[]>(`/api/v1/admin/prompts/${id}/versions`),
      ]);

      if (!templateResult || !versionsResult) throw new Error("Unauthorized");

      setTemplate(templateResult);
      setVersions(versionsResult);
    } catch (err: any) {
      setError(err.message || "Failed to fetch template");
      showToast("Failed to fetch template", "error");
    } finally {
      setLoading(false);
    }
  }, [id, showToast]);

  useEffect(() => {
    fetchTemplate();
  }, [fetchTemplate]);

  const handleActivateVersion = async (versionId: string) => {
    if (!id) return;

    try {
      await api.post(`/api/v1/admin/prompts/${id}/versions/${versionId}/activate`, {});
      showToast("Version activated successfully", "success");
      fetchTemplate();
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

  if (error || !template) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ color: "#991b1b", marginBottom: "1rem", fontSize: "1.125rem" }}>
            {error || "Template not found"}
          </div>
          <Link href="/admin/prompts">
            <button style={{ padding: "0.5rem 1rem", background: "#667eea", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
              Back to Templates
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
              <Link href="/admin/prompts">
                <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                  ← Back
                </button>
              </Link>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <Link href={`/admin/prompts/${id}/versions/new`}>
                  <button style={{ padding: "0.5rem 1rem", background: "white", color: "#667eea", border: "none", borderRadius: "8px", cursor: "pointer", fontWeight: "600" }}>
                    New Version
                  </button>
                </Link>
                <Link href={`/admin/prompts/${id}/compare`}>
                  <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                    Compare Versions
                  </button>
                </Link>
                <Link href={`/admin/prompts/${id}/audit`}>
                  <button style={{ padding: "0.5rem 1rem", background: "rgba(255,255,255,0.2)", color: "white", border: "none", borderRadius: "8px", cursor: "pointer" }}>
                    Audit Log
                  </button>
                </Link>
              </div>
            </div>
            <h1 style={{ fontSize: "2rem", fontWeight: "bold", marginBottom: "0.5rem" }}>{template.name}</h1>
            <p style={{ opacity: 0.9 }}>{template.description}</p>
            <div style={{ display: "flex", gap: "1rem", marginTop: "1rem", fontSize: "0.875rem" }}>
              <span>Category: <strong>{template.category}</strong></span>
              <span>Created: <strong>{new Date(template.createdAt).toLocaleDateString()}</strong></span>
              <span>Updated: <strong>{new Date(template.updatedAt).toLocaleDateString()}</strong></span>
            </div>
          </div>

          {/* Version History */}
          <div style={{ padding: "2rem" }}>
            <h2 style={{ fontSize: "1.5rem", fontWeight: "bold", marginBottom: "1.5rem" }}>Version History</h2>

            {versions.length === 0 && (
              <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                <p style={{ fontSize: "1.125rem", marginBottom: "0.5rem" }}>No versions yet</p>
                <p style={{ fontSize: "0.875rem", marginBottom: "1rem" }}>Create your first version to get started</p>
                <Link href={`/admin/prompts/${id}/versions/new`}>
                  <button style={{ padding: "0.5rem 1.5rem", background: "#667eea", color: "white", border: "none", borderRadius: "8px", cursor: "pointer", fontWeight: "600" }}>
                    Create Version
                  </button>
                </Link>
              </div>
            )}

            {versions.length > 0 && (
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(400px, 1fr))", gap: "1.5rem" }}>
                {versions.map((version) => (
                  <PromptVersionCard
                    key={version.id}
                    version={version}
                    onActivate={() => handleActivateVersion(version.id)}
                    onCompare={() => router.push(`/admin/prompts/${id}/compare?versions=${version.id}`)}
                    showActions={true}
                  />
                ))}
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
