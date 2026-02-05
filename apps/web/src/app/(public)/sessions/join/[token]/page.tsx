'use client';

/**
 * Session Join Page (Issue #3354)
 *
 * Page for joining a session via invite link.
 */

import { useState, useEffect, useCallback, use } from 'react';

import {
  AlertCircle,
  Calendar,
  CheckCircle2,
  Gamepad2,
  Loader2,
  MapPin,
  User,
  Users,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import type { SessionInviteResponse, JoinSessionResponse } from '@/components/session/types';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

// ============================================================================
// Types
// ============================================================================

interface JoinSessionPageProps {
  params: Promise<{ token: string }>;
}

// ============================================================================
// Page Component
// ============================================================================

export default function JoinSessionPage({ params }: JoinSessionPageProps) {
  const { token } = use(params);
  const router = useRouter();

  const [sessionInfo, setSessionInfo] = useState<SessionInviteResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isJoining, setIsJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [joinSuccess, setJoinSuccess] = useState<JoinSessionResponse | null>(null);

  // ==========================================================================
  // Fetch session info
  // ==========================================================================

  useEffect(() => {
    async function fetchSessionInfo() {
      try {
        const response = await fetch(`/api/v1/game-sessions/invite/${token}`);

        if (!response.ok) {
          if (response.status === 404) {
            setError('This invite link is invalid or has expired.');
          } else {
            setError('Failed to load session information.');
          }
          return;
        }

        const data: SessionInviteResponse = await response.json();
        setSessionInfo(data);

        if (!data.canJoin) {
          setError(data.reasonCannotJoin || 'You cannot join this session.');
        }
      } catch (err) {
        setError('Failed to connect to server.');
        console.error('Error fetching session info:', err);
      } finally {
        setIsLoading(false);
      }
    }

    fetchSessionInfo();
  }, [token]);

  // ==========================================================================
  // Join session handler
  // ==========================================================================

  const handleJoin = useCallback(async () => {
    if (!displayName.trim()) {
      setError('Please enter your display name.');
      return;
    }

    setIsJoining(true);
    setError(null);

    try {
      const response = await fetch(`/api/v1/game-sessions/invite/${token}/join`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ displayName: displayName.trim() }),
      });

      if (!response.ok) {
        if (response.status === 401) {
          // Redirect to login with return URL
          router.push(`/login?returnUrl=/sessions/join/${token}`);
          return;
        }
        if (response.status === 409) {
          const data = await response.json();
          setError(data.error || 'Cannot join session.');
          return;
        }
        throw new Error('Failed to join session');
      }

      const data: JoinSessionResponse = await response.json();
      setJoinSuccess(data);

      // Redirect to session after short delay
      setTimeout(() => {
        router.push(`/sessions/${data.sessionId}`);
      }, 2000);
    } catch (err) {
      setError('Failed to join session. Please try again.');
      console.error('Error joining session:', err);
    } finally {
      setIsJoining(false);
    }
  }, [token, displayName, router]);

  // ==========================================================================
  // Format helpers
  // ==========================================================================

  const formatDate = (date: Date | string): string => {
    const d = new Date(date);
    return d.toLocaleDateString(undefined, {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // ==========================================================================
  // Render
  // ==========================================================================

  // Loading state
  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
        <Card className="w-full max-w-md">
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-primary" />
            <p className="mt-4 text-muted-foreground">Loading session...</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Success state
  if (joinSuccess) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
        <Card className="w-full max-w-md">
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <div className="rounded-full bg-green-100 p-3 dark:bg-green-900/30">
              <CheckCircle2 className="h-8 w-8 text-green-600 dark:text-green-400" />
            </div>
            <h2 className="mt-4 text-xl font-semibold">You&#39;re in!</h2>
            <p className="mt-2 text-muted-foreground">
              Welcome, {joinSuccess.displayName}! Redirecting to session...
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Error state (no session info)
  if (!sessionInfo) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-destructive">
              <AlertCircle className="h-5 w-5" />
              Invalid Invite
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground">
              {error || 'This invite link is invalid or has expired.'}
            </p>
          </CardContent>
          <CardFooter>
            <Button onClick={() => router.push('/sessions')} className="w-full">
              Go to Sessions
            </Button>
          </CardFooter>
        </Card>
      </div>
    );
  }

  // Main join form
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-primary/10">
            <Gamepad2 className="h-8 w-8 text-primary" />
          </div>
          <CardTitle>Join Game Session</CardTitle>
          <CardDescription>
            {sessionInfo.ownerDisplayName} invited you to join
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* Session info */}
          <div className="rounded-lg border bg-muted/50 p-4 space-y-3">
            {sessionInfo.gameName && (
              <div className="flex items-center gap-2 text-sm">
                <Gamepad2 className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">{sessionInfo.gameName}</span>
              </div>
            )}

            <div className="flex items-center gap-2 text-sm">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <span>{formatDate(sessionInfo.sessionDate)}</span>
            </div>

            {sessionInfo.location && (
              <div className="flex items-center gap-2 text-sm">
                <MapPin className="h-4 w-4 text-muted-foreground" />
                <span>{sessionInfo.location}</span>
              </div>
            )}

            <div className="flex items-center gap-2 text-sm">
              <Users className="h-4 w-4 text-muted-foreground" />
              <span>{sessionInfo.participantCount} participants</span>
            </div>
          </div>

          {/* Session code */}
          <div className="text-center">
            <p className="text-xs text-muted-foreground">Session Code</p>
            <p className="font-mono text-xl font-bold tracking-wider">
              {sessionInfo.sessionCode}
            </p>
          </div>

          {/* Error alert */}
          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Error</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {/* Join form */}
          {sessionInfo.canJoin && (
            <div className="space-y-2">
              <Label htmlFor="displayName">Your Display Name</Label>
              <div className="flex gap-2">
                <div className="relative flex-1">
                  <User className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="displayName"
                    placeholder="Enter your name"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    className="pl-10"
                    maxLength={50}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        handleJoin();
                      }
                    }}
                  />
                </div>
              </div>
            </div>
          )}
        </CardContent>

        <CardFooter className="flex flex-col gap-2">
          {sessionInfo.canJoin ? (
            <Button
              onClick={handleJoin}
              disabled={isJoining || !displayName.trim()}
              className="w-full"
            >
              {isJoining ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Joining...
                </>
              ) : (
                'Join Session'
              )}
            </Button>
          ) : (
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                {sessionInfo.reasonCannotJoin || 'This session cannot accept new participants.'}
              </AlertDescription>
            </Alert>
          )}

          <Button
            variant="ghost"
            onClick={() => router.push('/sessions')}
            className="w-full"
          >
            Cancel
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
