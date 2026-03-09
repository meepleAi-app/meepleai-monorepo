/**
 * SessionJoinForm - Join a session via 6-character code
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 */

'use client';

import { useState, useCallback, type FormEvent } from 'react';

import { LogIn, Loader2, AlertCircle } from 'lucide-react';

interface SessionJoinFormProps {
  onJoin: (code: string, displayName: string) => Promise<void>;
  isLoading?: boolean;
  error?: string | null;
  className?: string;
}

export function SessionJoinForm({
  onJoin,
  isLoading = false,
  error = null,
  className = '',
}: SessionJoinFormProps) {
  const [code, setCode] = useState('');
  const [displayName, setDisplayName] = useState('');

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      if (!code.trim() || !displayName.trim() || isLoading) return;
      await onJoin(code.trim().toUpperCase(), displayName.trim());
    },
    [code, displayName, isLoading, onJoin]
  );

  const handleCodeChange = useCallback((value: string) => {
    // Allow only alphanumeric, max 6 chars, auto-uppercase
    const cleaned = value
      .replace(/[^A-Za-z0-9]/g, '')
      .toUpperCase()
      .slice(0, 6);
    setCode(cleaned);
  }, []);

  return (
    <form onSubmit={handleSubmit} className={`space-y-4 ${className}`}>
      <div>
        <label htmlFor="session-code" className="mb-1.5 block text-sm font-medium text-gray-700">
          Session Code
        </label>
        <input
          id="session-code"
          type="text"
          value={code}
          onChange={e => handleCodeChange(e.target.value)}
          placeholder="ABC123"
          maxLength={6}
          className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-center font-mono text-2xl tracking-[0.3em] uppercase placeholder:text-gray-300 placeholder:tracking-[0.3em] focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
          disabled={isLoading}
          autoComplete="off"
          aria-describedby={error ? 'join-error' : undefined}
        />
      </div>

      <div>
        <label htmlFor="display-name" className="mb-1.5 block text-sm font-medium text-gray-700">
          Your Name
        </label>
        <input
          id="display-name"
          type="text"
          value={displayName}
          onChange={e => setDisplayName(e.target.value)}
          placeholder="Player name"
          maxLength={50}
          className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
          disabled={isLoading}
        />
      </div>

      {error && (
        <div
          id="join-error"
          className="flex items-center gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
          role="alert"
        >
          <AlertCircle className="h-4 w-4 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <button
        type="submit"
        disabled={isLoading || code.length !== 6 || !displayName.trim()}
        className="flex w-full items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            Joining...
          </>
        ) : (
          <>
            <LogIn className="h-4 w-4" />
            Join Session
          </>
        )}
      </button>
    </form>
  );
}
