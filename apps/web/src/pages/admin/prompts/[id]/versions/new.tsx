import React, { useState, useCallback } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "../../../../../lib/api";
import PromptEditor from "../../../../../components/PromptEditor";

type ToastState = {
  show: boolean;
  message: string;
  type: "success" | "error";
};

export default function NewPromptVersion() {
  const router = useRouter();
  const { id } = router.query;

  const [content, setContent] = useState("");
  const [metadata, setMetadata] = useState("");
  const [activateImmediately, setActivateImmediately] = useState(false);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<ToastState>({ show: false, message: "", type: "success" });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const showToast = useCallback((message: string, type: "success" | "error") => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: "", type: "success" }), 5000);
  }, []);

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!content.trim()) {
      newErrors.content = "Content is required";
    }

    if (metadata.trim()) {
      try {
        JSON.parse(metadata);
      } catch {
        newErrors.metadata = "Metadata must be valid JSON";
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      showToast("Please fix the errors before submitting", "error");
      return;
    }

    setLoading(true);

    try {
      const payload: any = {
        content,
      };

      if (metadata.trim()) {
        payload.metadata = JSON.parse(metadata);
      }

      const result = await api.post<{ id: string }>(`/api/v1/admin/prompts/${id}/versions`, payload);

      if (!result) throw new Error("Failed to create version");

      showToast("Version created successfully", "success");

      if (activateImmediately && result.id) {
        try {
          await api.post<{}>(`/api/v1/admin/prompts/${id}/versions/${result.id}/activate`, {});
          showToast("Version created and activated", "success");
        } catch (err) {
          console.error("Failed to activate version:", err);
        }
      }

      setTimeout(() => {
        router.push(`/admin/prompts/${id}`);
      }, 1000);
    } catch (err: any) {
      showToast(err.message || "Failed to create version", "error");
    } finally {
      setLoading(false);
    }
  };

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
            </div>
            <h1 style={{ fontSize: "2rem", fontWeight: "bold", marginBottom: "0.5rem" }}>Create New Version</h1>
            <p style={{ opacity: 0.9 }}>Create a new version of this prompt template</p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} style={{ padding: "2rem" }}>
            <div style={{ marginBottom: "1.5rem" }}>
              <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "600", fontSize: "1.125rem" }}>
                Prompt Content <span style={{ color: "#dc2626" }}>*</span>
              </label>
              <p style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                Enter the prompt content using Markdown formatting
              </p>
              <PromptEditor
                value={content}
                onChange={(value) => setContent(value || "")}
                height="500px"
                placeholder="Enter your prompt content here..."
              />
              {errors.content && (
                <div style={{ color: "#dc2626", fontSize: "0.875rem", marginTop: "0.5rem" }}>
                  {errors.content}
                </div>
              )}
            </div>

            <div style={{ marginBottom: "1.5rem" }}>
              <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: "600", fontSize: "1.125rem" }}>
                Metadata (Optional)
              </label>
              <p style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                Provide additional metadata as JSON (e.g., model parameters, tags)
              </p>
              <textarea
                value={metadata}
                onChange={(e) => setMetadata(e.target.value)}
                rows={6}
                placeholder='{\n  "temperature": 0.7,\n  "max_tokens": 1000,\n  "tags": ["production"]\n}'
                style={{
                  width: "100%",
                  padding: "0.75rem",
                  border: "1px solid #d1d5db",
                  borderRadius: "8px",
                  fontFamily: "monospace",
                  fontSize: "0.875rem",
                }}
              />
              {errors.metadata && (
                <div style={{ color: "#dc2626", fontSize: "0.875rem", marginTop: "0.5rem" }}>
                  {errors.metadata}
                </div>
              )}
            </div>

            <div style={{ marginBottom: "2rem" }}>
              <label style={{ display: "flex", alignItems: "center", cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={activateImmediately}
                  onChange={(e) => setActivateImmediately(e.target.checked)}
                  style={{ marginRight: "0.5rem", width: "18px", height: "18px", cursor: "pointer" }}
                />
                <span style={{ fontWeight: "500" }}>Activate this version immediately</span>
              </label>
              <p style={{ fontSize: "0.875rem", color: "#6b7280", marginLeft: "1.75rem" }}>
                This will make this version the active prompt for all services using this template
              </p>
            </div>

            <div style={{ display: "flex", gap: "1rem", justifyContent: "flex-end", borderTop: "1px solid #e5e7eb", paddingTop: "1.5rem" }}>
              <Link href={`/admin/prompts/${id}`}>
                <button
                  type="button"
                  style={{
                    padding: "0.75rem 1.5rem",
                    background: "#e5e7eb",
                    color: "#374151",
                    border: "none",
                    borderRadius: "8px",
                    cursor: "pointer",
                    fontWeight: "600",
                  }}
                >
                  Cancel
                </button>
              </Link>
              <button
                type="submit"
                disabled={loading}
                style={{
                  padding: "0.75rem 1.5rem",
                  background: loading ? "#9ca3af" : "#667eea",
                  color: "white",
                  border: "none",
                  borderRadius: "8px",
                  cursor: loading ? "not-allowed" : "pointer",
                  fontWeight: "600",
                }}
              >
                {loading ? "Creating..." : "Create Version"}
              </button>
            </div>
          </form>
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
