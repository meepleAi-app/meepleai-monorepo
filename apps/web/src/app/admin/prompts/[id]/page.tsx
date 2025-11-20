'use client';

import React, { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { PromptVersionCard } from "@/components/prompt";
import { cn } from "@/lib/utils";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";
import { getErrorMessage } from '@/lib/utils/errorHandler';

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
  const params = useParams();
  const id = params?.id as string | undefined;

  const [template, setTemplate] = useState<PromptTemplate | null>(null);
  const [versions, setVersions] = useState<PromptVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState>({ show: false, message: "", type: "success" });

  // Handle missing ID
  if (!id) {
    return (
      <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
        <div className="max-w-7xl mx-auto p-8">
          <div className="bg-white rounded-xl shadow-2xl overflow-hidden p-8">
            <h1 className="text-2xl font-bold mb-4">Invalid Template ID</h1>
            <p className="text-gray-600 mb-4">No template ID provided.</p>
            <Link href="/admin/prompts">
              <button className="px-6 py-2 bg-indigo-500 text-white border-none rounded-lg cursor-pointer hover:bg-indigo-600">
                Back to Templates
              </button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

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
    } catch (err) {
      setError(getErrorMessage(err, "Failed to fetch template"));
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
    } catch (err) {
      showToast(getErrorMessage(err, "Failed to activate version"), "error");
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center text-gray-500">Loading...</div>
      </div>
    );
  }

  if (error || !template) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="max-w-2xl w-full p-8">
          <ErrorDisplay
            error={categorizeError(new Error(error || "Template not found"))}
            onRetry={fetchTemplate}
            showTechnicalDetails={process.env.NODE_ENV === 'development'}
          />
          <div className="text-center mt-4">
            <Link href="/admin/prompts">
              <button className="px-4 py-2 bg-indigo-500 text-white border-none rounded-lg cursor-pointer hover:bg-indigo-600">
                Back to Templates
              </button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div className="max-w-7xl mx-auto p-8">
        <div className="bg-white rounded-xl shadow-2xl overflow-hidden">
          {/* Header */}
          <div className="p-8 text-white" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
            <div className="flex items-center justify-between mb-4">
              <Link href="/admin/prompts">
                <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer hover:bg-white/30">
                  ← Back
                </button>
              </Link>
              <div className="flex gap-2">
                <Link href={`/admin/prompts/${id}/versions/new`}>
                  <button className="px-4 py-2 bg-white text-indigo-500 border-none rounded-lg cursor-pointer font-semibold hover:bg-gray-100">
                    New Version
                  </button>
                </Link>
                <Link href={`/admin/prompts/${id}/compare`}>
                  <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer hover:bg-white/30">
                    Compare Versions
                  </button>
                </Link>
                <Link href={`/admin/prompts/${id}/audit`}>
                  <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer hover:bg-white/30">
                    Audit Log
                  </button>
                </Link>
              </div>
            </div>
            <h1 className="text-3xl font-bold mb-2">{template.name}</h1>
            <p className="opacity-90">{template.description}</p>
            <div className="flex gap-4 mt-4 text-sm">
              <span>Category: <strong>{template.category}</strong></span>
              <span>Created: <strong>{new Date(template.createdAt).toLocaleDateString()}</strong></span>
              <span>Updated: <strong>{new Date(template.updatedAt).toLocaleDateString()}</strong></span>
            </div>
          </div>

          {/* Version History */}
          <div className="p-8">
            <h2 className="text-2xl font-bold mb-6">Version History</h2>

            {versions.length === 0 && (
              <div className="text-center p-12 text-gray-500">
                <p className="text-lg mb-2">No versions yet</p>
                <p className="text-sm mb-4">Create your first version to get started</p>
                <Link href={`/admin/prompts/${id}/versions/new`}>
                  <button className="px-6 py-2 bg-indigo-500 text-white border-none rounded-lg cursor-pointer font-semibold hover:bg-indigo-600">
                    Create Version
                  </button>
                </Link>
              </div>
            )}

            {versions.length > 0 && (
              <div className="grid grid-cols-[repeat(auto-fill,minmax(400px,1fr))] gap-6">
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
          className={cn(
            "fixed bottom-8 right-8 px-6 py-4 rounded-lg shadow-2xl z-[100] font-medium",
            toast.type === "success" ? "bg-emerald-500 text-white" : "bg-red-500 text-white"
          )}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}
