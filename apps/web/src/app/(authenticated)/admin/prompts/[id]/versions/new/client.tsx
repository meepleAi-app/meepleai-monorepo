'use client';

import React, { useState, useCallback } from 'react';

import Link from 'next/link';
import { useRouter, useParams } from 'next/navigation';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { PromptEditor } from '@/components/prompt';
import { api } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';
import { getErrorMessage } from '@/lib/utils/errorHandler';
import type { AuthUser } from '@/types/auth';

type ToastState = {
  show: boolean;
  message: string;
  type: 'success' | 'error';
};

interface AdminPageClientProps {
  user: AuthUser;
}

export function AdminPageClient({ user: _user }: AdminPageClientProps) {
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string | undefined;

  const [content, setContent] = useState('');
  const [metadata, setMetadata] = useState('');
  const [activateImmediately, setActivateImmediately] = useState(false);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<ToastState>({ show: false, message: '', type: 'success' });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ show: true, message, type });
    setTimeout(() => setToast({ show: false, message: '', type: 'success' }), 5000);
  }, []);

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!content.trim()) {
      newErrors.content = 'Content is required';
    }

    if (metadata.trim()) {
      try {
        JSON.parse(metadata);
      } catch {
        newErrors.metadata = 'Metadata must be valid JSON';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      showToast('Please fix the errors before submitting', 'error');
      return;
    }

    setLoading(true);

    try {
      const payload: Record<string, unknown> = {
        content,
      };

      if (metadata.trim()) {
        payload.metadata = JSON.parse(metadata);
      }

      // eslint-disable-next-line @typescript-eslint/no-explicit-any -- API payload type coercion
      const result = await api.admin.createPromptVersion(id, payload as any);

      showToast('Version created successfully', 'success');

      if (activateImmediately && result.id) {
        try {
          await api.admin.activatePromptVersion(id, result.id);
          showToast('Version created and activated', 'success');
        } catch (err) {
          logger.error(
            'Failed to activate prompt version',
            err instanceof Error ? err : new Error(String(err)),
            createErrorContext('NewPromptVersion', 'handleSubmit', {
              promptId: id,
              versionId: result.id,
              operation: 'activate_version',
            })
          );
        }
      }

      setTimeout(() => {
        router.push(`/admin/prompts/${id}`);
      }, 1000);
    } catch (err) {
      showToast(getErrorMessage(err, 'Failed to create version'), 'error');
    } finally {
      setLoading(false);
    }
  };

  // Early return after all hooks
  if (!id) {
    return (
      <div
        className="min-h-screen"
        style={{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}
      >
        <div className="max-w-[1400px] mx-auto p-8">
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
            </div>
            <h1 className="text-3xl font-bold mb-2">Create New Version</h1>
            <p className="opacity-90">Create a new version of this prompt template</p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="p-8">
            <div className="mb-6">
              <label className="block mb-2 font-semibold text-lg">
                Prompt Content <span className="text-red-600">*</span>
              </label>
              <p className="text-sm text-gray-500 mb-2">
                Enter the prompt content using Markdown formatting
              </p>
              <PromptEditor
                value={content}
                onChange={value => setContent(value || '')}
                height="500px"
                placeholder="Enter your prompt content here..."
              />
              {errors.content && <div className="text-red-600 text-sm mt-2">{errors.content}</div>}
            </div>

            <div className="mb-6">
              <label className="block mb-2 font-semibold text-lg">Metadata (Optional)</label>
              <p className="text-sm text-gray-500 mb-2">
                Provide additional metadata as JSON (e.g., model parameters, tags)
              </p>
              <textarea
                value={metadata}
                onChange={e => setMetadata(e.target.value)}
                rows={6}
                placeholder='{\n  "temperature": 0.7,\n  "max_tokens": 1000,\n  "tags": ["production"]\n}'
                className="w-full px-3 py-3 border border-gray-300 rounded-lg font-mono text-sm"
              />
              {errors.metadata && (
                <div className="text-red-600 text-sm mt-2">{errors.metadata}</div>
              )}
            </div>

            <div className="mb-8">
              <label className="flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={activateImmediately}
                  onChange={e => setActivateImmediately(e.target.checked)}
                  className="mr-2 w-[18px] h-[18px] cursor-pointer"
                />
                <span className="font-medium">Activate this version immediately</span>
              </label>
              <p className="text-sm text-gray-500 ml-7">
                This will make this version the active prompt for all services using this template
              </p>
            </div>

            <div className="flex gap-4 justify-end border-t border-gray-200 pt-6">
              <Link href={`/admin/prompts/${id}`}>
                <button
                  type="button"
                  className="px-6 py-3 bg-gray-200 text-gray-700 border-none rounded-lg cursor-pointer font-semibold"
                >
                  Cancel
                </button>
              </Link>
              <LoadingButton
                type="submit"
                isLoading={loading}
                loadingText="Creating..."
                className="px-6 py-3 bg-[#667eea] text-white border-none rounded-lg font-semibold hover:opacity-90"
              >
                Create Version
              </LoadingButton>
            </div>
          </form>
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
