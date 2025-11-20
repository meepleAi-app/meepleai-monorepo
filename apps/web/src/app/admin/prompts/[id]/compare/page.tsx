'use client';

import React, { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import dynamic from "next/dynamic";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { ErrorDisplay } from "@/components/errors";
import { categorizeError } from "@/lib/errorUtils";
import { getErrorMessage } from '@/lib/utils/errorHandler';

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
  const params = useParams();
  const id = params?.id as string | undefined;

  const [versions, setVersions] = useState<PromptVersion[]>([]);
  const [version1Id, setVersion1Id] = useState<string>("");
  const [version2Id, setVersion2Id] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Handle missing ID
  if (!id) {
    return (
      <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
        <div className="max-w-[1600px] mx-auto p-8">
          <div className="bg-white rounded-xl shadow-[0_20px_60px_rgba(0,0,0,0.3)] overflow-hidden p-8">
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
    } catch (err) {
      setError(getErrorMessage(err, "Failed to fetch versions"));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchVersions();
  }, [fetchVersions]);

  const version1 = versions.find((v) => v.id === version1Id);
  const version2 = versions.find((v) => v.id === version2Id);

  const canCompare = version1 && version2 && version1.id !== version2.id;

  return (
    <div className="min-h-screen" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
      <div className="max-w-[1600px] mx-auto p-8">
        <div className="bg-white rounded-xl shadow-[0_20px_60px_rgba(0,0,0,0.3)] overflow-hidden">
          {/* Header */}
          <div className="p-8 text-white" style={{ background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" }}>
            <div className="mb-4">
              <Link href={`/admin/prompts/${id}`}>
                <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer">
                  ← Back to Template
                </button>
              </Link>
            </div>
            <h1 className="text-3xl font-bold mb-2">Compare Versions</h1>
            <p className="opacity-90">Select two versions to compare side-by-side</p>
          </div>

          {/* Content */}
          <div className="p-8">
            {loading && <div className="text-center py-8 text-gray-500">Loading...</div>}

            {error && (
              <ErrorDisplay
                error={categorizeError(new Error(error))}
                onRetry={fetchVersions}
                showTechnicalDetails={process.env.NODE_ENV === 'development'}
              />
            )}

            {!loading && versions.length < 2 && (
              <div className="text-center py-12 text-gray-500">
                <p className="text-lg mb-2">Not enough versions to compare</p>
                <p className="text-sm">You need at least two versions to use the comparison feature</p>
              </div>
            )}

            {!loading && versions.length >= 2 && (
              <>
                {/* Version Selectors */}
                <div className="grid grid-cols-2 gap-8 mb-8">
                  <div>
                    <label className="block mb-2 font-semibold">Version A (Original)</label>
                    <select
                      value={version1Id}
                      onChange={(e) => setVersion1Id(e.target.value)}
                      className="w-full px-3 py-3 border border-gray-300 rounded-lg text-base"
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
                    <label className="block mb-2 font-semibold">Version B (Modified)</label>
                    <select
                      value={version2Id}
                      onChange={(e) => setVersion2Id(e.target.value)}
                      className="w-full px-3 py-3 border border-gray-300 rounded-lg text-base"
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
                    <div className="mb-4 p-4 bg-gray-50 rounded-lg border border-gray-200">
                      <div className="flex justify-between text-sm text-gray-500">
                        <div>
                          <strong>Version {version1?.versionNumber}</strong> - {new Date(version1?.createdAt || "").toLocaleString()}
                        </div>
                        <div>
                          <strong>Version {version2?.versionNumber}</strong> - {new Date(version2?.createdAt || "").toLocaleString()}
                        </div>
                      </div>
                    </div>

                    <div className="border border-gray-200 rounded-lg overflow-hidden">
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
                          <div className="flex items-center justify-center h-[700px]">
                            <div className="text-center text-gray-500">
                              Loading diff view...
                            </div>
                          </div>
                        }
                      />
                    </div>

                    <div className="mt-6 p-4 bg-blue-50 rounded-lg text-sm text-blue-800">
                      <strong>Legend:</strong> <span className="text-red-600">Red</span> indicates removed content, <span className="text-emerald-600">Green</span> indicates added content
                    </div>
                  </div>
                )}

                {!canCompare && version1Id && version2Id && version1Id === version2Id && (
                  <div className="text-center py-8 text-gray-500">
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