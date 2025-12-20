'use client';

import type { AuthUser } from '@/types/auth';
import React, { useState, useEffect, useCallback } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { PromptEditor } from '@/components/prompt';
import { ErrorDisplay } from '@/components/errors';
import { categorizeError } from '@/lib/errorUtils';
import { getErrorMessage } from '@/lib/utils/errorHandler';

type PromptVersion = {
  id: string;
  templateId: string;
  versionNumber: number;
  content: string;
  metadata?: Record<string, unknown>;
  isActive: boolean;
  createdById: string;
  createdByEmail: string;
  createdAt: string;
};

type ToastState = {
  show: boolean;
  message: string;
  type: 'success' | 'error';
};

interface AdminPageClientProps {
  user: AuthUser;
}

export function AdminPageClient({ user: _user }: AdminPageClientProps) {
  const _router = useRouter();
  const params = useParams();
  const id = params?.id as string | undefined;
  const versionId = params?.versionId as string | undefined;

  const [version, setVersion] = useState<PromptVersion | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState>({ show: false, message: '', type: 'success' });

  // Handle missing IDs
  if (!id || !versionId) {
    return (
      <div
        className="min-h-screen"
        style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
      >
        <div className="max-w-[1400px] mx-auto p-8">
          <div className="bg-white rounded-xl shadow-[0_20px_60px_rgba(0,0,0,0.3)] overflow-hidden p-8">
            <h1 className="text-2xl font-bold mb-4">Invalid Version ID</h1>
            <p className="text-gray-600 mb-4">No template or version ID provided.</p>
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

  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: '', type: 'success' }), 5000);
  }, []);

  const fetchVersion = useCallback(async () => {
    if (!id || !versionId) return;

    setLoading(true);
    setError(null);

    try {
      const result = await api.admin.getPromptVersion(id, versionId);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any -- API response type coercion
      setVersion(result as any);
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to fetch version'));
      showToast('Failed to fetch version', 'error');
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
      await api.admin.activatePromptVersion(id, versionId);
      showToast('Version activated successfully', 'success');
      fetchVersion();
    } catch (err) {
      showToast(getErrorMessage(err, 'Failed to activate version'), 'error');
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center text-gray-500">Loading...</div>
      </div>
    );
  }

  if (error || !version) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="max-w-2xl w-full p-8">
          <ErrorDisplay
            error={categorizeError(new Error(error || 'Version not found'))}
            onRetry={fetchVersion}
            showTechnicalDetails={process.env.NODE_ENV === 'development'}
          />
          <div className="text-center mt-4">
            <Link href={`/admin/prompts/${id}`}>
              <button className="px-4 py-2 bg-[#667eea] text-white border-none rounded-lg cursor-pointer">
                Back to Template
              </button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div
      className="min-h-screen"
      style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
    >
      <div className="max-w-[1400px] mx-auto p-8">
        <div className="bg-white rounded-xl shadow-[0_20px_60px_rgba(0,0,0,0.3)] overflow-hidden">
          {/* Header */}
          <div
            className="p-8 text-white"
            style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
          >
            <div className="flex items-center justify-between mb-4">
              <Link href={`/admin/prompts/${id}`}>
                <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer">
                  ← Back to Template
                </button>
              </Link>
              <div className="flex gap-2">
                {!version.isActive && (
                  <button
                    onClick={handleActivate}
                    className="px-6 py-2 bg-white text-[#667eea] border-none rounded-lg cursor-pointer font-semibold"
                  >
                    Activate Version
                  </button>
                )}
                <Link href={`/admin/prompts/${id}/compare?versions=${versionId}`}>
                  <button className="px-4 py-2 bg-white/20 text-white border-none rounded-lg cursor-pointer">
                    Compare
                  </button>
                </Link>
              </div>
            </div>
            <div className="flex items-center gap-4">
              <h1 className="text-3xl font-bold">Version {version.versionNumber}</h1>
              {version.isActive && (
                <span className="px-4 py-2 bg-emerald-500/20 text-emerald-200 rounded-full text-sm font-semibold">
                  ✓ Active
                </span>
              )}
            </div>
            <div className="flex gap-8 mt-4 text-sm opacity-90">
              <span>
                Created by: <strong>{version.createdByEmail}</strong>
              </span>
              <span>
                Created: <strong>{new Date(version.createdAt).toLocaleString()}</strong>
              </span>
            </div>
          </div>

          {/* Content */}
          <div className="p-8">
            <div className="mb-8">
              <h2 className="text-xl font-semibold mb-4">Prompt Content</h2>
              <PromptEditor value={version.content} readonly={true} height="600px" />
            </div>

            {version.metadata && Object.keys(version.metadata).length > 0 && (
              <div>
                <h2 className="text-xl font-semibold mb-4">Metadata</h2>
                <pre className="bg-gray-50 p-6 rounded-lg border border-gray-200 overflow-auto text-sm font-mono">
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
          className={cn(
            'fixed bottom-8 right-8 px-6 py-4 text-white rounded-lg shadow-[0_10px_40px_rgba(0,0,0,0.3)] z-[100] font-medium',
            toast.type === 'success' ? 'bg-emerald-500' : 'bg-red-500'
          )}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}
