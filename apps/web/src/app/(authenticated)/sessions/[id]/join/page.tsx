'use client';

/**
 * Token-Based Session Join Page
 *
 * Game Night Improvvisata - guest join flow via invite link.
 * Reads `token` from URL search params, collects display name, calls joinSession API.
 * On success: stores sessionToken in sessionStorage and redirects to session page.
 */

import { useState, useCallback, use, type FormEvent } from 'react';

import { Loader2, Users, AlertCircle } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

interface JoinPageProps {
  params: Promise<{ id: string }>;
}

// ============================================================================
// Page Component
// ============================================================================

export default function JoinSessionPage({ params }: JoinPageProps) {
  const { id: sessionId } = use(params);
  const searchParams = useSearchParams();
  const router = useRouter();

  const token = searchParams.get('token') ?? '';

  const [displayName, setDisplayName] = useState('');
  const [isJoining, setIsJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();

      const trimmedName = displayName.trim();
      if (!trimmedName || !token) return;

      setIsJoining(true);
      setError(null);

      try {
        const result = await api.sessionInvites.joinSession({
          token,
          guestName: trimmedName,
        });

        // Store session token for subsequent API calls
        const targetSessionId = result.sessionId || sessionId;
        sessionStorage.setItem(`session-token-${targetSessionId}`, result.sessionToken);

        // Redirect to the session page
        router.push(`/sessions/${targetSessionId}`);
      } catch {
        setError('Failed to join session. The invite may have expired or is invalid.');
        setIsJoining(false);
      }
    },
    [displayName, token, router, sessionId]
  );

  // --------------------------------------------------------------------------
  // Missing token state
  // --------------------------------------------------------------------------

  if (!token) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center px-4">
        <Card className="w-full max-w-sm bg-white/70 backdrop-blur-md">
          <CardContent className="px-6 py-8 text-center">
            <AlertCircle className="mx-auto mb-3 h-10 w-10 text-red-500" />
            <p className="text-sm text-muted-foreground font-nunito">
              Invalid invite link. Please ask the host for a new invitation.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  // --------------------------------------------------------------------------
  // Join form
  // --------------------------------------------------------------------------

  return (
    <div className="flex min-h-[60vh] items-center justify-center px-4">
      <Card className="w-full max-w-sm bg-white/70 backdrop-blur-md">
        <CardHeader className="pb-4 text-center">
          <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-amber-100">
            <Users className="h-6 w-6 text-amber-900" />
          </div>
          <CardTitle className="font-quicksand text-xl">Join Session</CardTitle>
        </CardHeader>

        <CardContent className="px-6 pb-6">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="display-name" className="font-nunito text-sm">
                Your Display Name
              </Label>
              <Input
                id="display-name"
                type="text"
                value={displayName}
                onChange={e => setDisplayName(e.target.value)}
                placeholder="Enter your name"
                maxLength={50}
                required
                autoFocus
                disabled={isJoining}
                className="font-nunito"
                data-testid="display-name-input"
              />
            </div>

            {/* Error message */}
            {error && (
              <div
                className="flex items-center gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
                role="alert"
                data-testid="join-error"
              >
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span className="font-nunito">{error}</span>
              </div>
            )}

            <Button
              type="submit"
              className="w-full gap-2 bg-amber-600 hover:bg-amber-700 font-nunito"
              disabled={!displayName.trim() || isJoining}
              data-testid="join-session-button"
            >
              {isJoining ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Joining...
                </>
              ) : (
                'Join Session'
              )}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
