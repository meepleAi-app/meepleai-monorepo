'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Dice6, Users, CalendarDays } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useSessionStore } from '@/lib/stores/sessionStore';
import { toast } from 'sonner';

/**
 * Toolkit Landing Page
 *
 * Features:
 * - Create new session
 * - Join session by code
 * - Recent sessions list
 */
export default function ToolkitLandingPage() {
  const router = useRouter();
  // Using toast from sonner (imported above)
  const { createSession, joinSession, isLoading, error } = useSessionStore();

  // Create session form state
  const [participantNames, setParticipantNames] = useState<string[]>(['']);
  const [isCreating, setIsCreating] = useState(false);

  // Join session form state
  const [joinCode, setJoinCode] = useState('');
  const [isJoining, setIsJoining] = useState(false);

  /**
   * Handle create session
   */
  const handleCreateSession = async () => {
    const validParticipants = participantNames.filter(name => name.trim().length > 0);

    if (validParticipants.length === 0) {
      toast.error('Please add at least one participant');
      return;
    }

    setIsCreating(true);

    try {
      await createSession({
        participants: validParticipants.map(name => ({ displayName: name.trim() })),
        sessionDate: new Date(),
      });

      const activeSession = useSessionStore.getState().activeSession;
      if (activeSession) {
        toast.success(`Session created! Code: ${activeSession.sessionCode}`);

        router.push(`/toolkit/${activeSession.id}`);
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to create session');
    } finally {
      setIsCreating(false);
    }
  };

  /**
   * Handle join session
   */
  const handleJoinSession = async () => {
    const code = joinCode.trim().toUpperCase();

    if (code.length !== 6) {
      toast.error('Session code must be 6 characters');
      return;
    }

    setIsJoining(true);

    try {
      await joinSession(code);

      const activeSession = useSessionStore.getState().activeSession;
      if (activeSession) {
        toast.success(`Joined session ${activeSession.sessionCode}`);

        router.push(`/toolkit/${activeSession.id}`);
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to join session');
    } finally {
      setIsJoining(false);
    }
  };

  /**
   * Add participant input
   */
  const addParticipant = () => {
    setParticipantNames([...participantNames, '']);
  };

  /**
   * Remove participant input
   */
  const removeParticipant = (index: number) => {
    setParticipantNames(participantNames.filter((_, i) => i !== index));
  };

  /**
   * Update participant name
   */
  const updateParticipant = (index: number, value: string) => {
    const updated = [...participantNames];
    updated[index] = value;
    setParticipantNames(updated);
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-gray-900">
      <div className="container mx-auto px-4 py-8 max-w-4xl">
        {/* Hero Section */}
        <div className="text-center mb-12">
          <div className="inline-flex items-center justify-center w-16 h-16 mb-4 rounded-full bg-blue-100 dark:bg-blue-900">
            <Dice6 className="w-8 h-8 text-blue-600 dark:text-blue-400" />
          </div>
          <h1 className="text-4xl font-bold mb-2 bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
            Game Session Toolkit
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-300">
            Track scores, manage participants, and sync in real-time
          </p>
        </div>

        {/* Actions Grid */}
        <div className="grid md:grid-cols-2 gap-6 mb-8">
          {/* Create Session Card */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Users className="w-5 h-5" />
                Create New Session
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <Label className="mb-2 block">Participants</Label>
                <div className="space-y-2">
                  {participantNames.map((name, index) => (
                    <div key={index} className="flex gap-2">
                      <Input
                        value={name}
                        onChange={e => updateParticipant(index, e.target.value)}
                        placeholder={`Participant ${index + 1}`}
                        disabled={isCreating}
                      />
                      {participantNames.length > 1 && (
                        <Button
                          variant="outline"
                          size="icon"
                          onClick={() => removeParticipant(index)}
                          disabled={isCreating}
                        >
                          ×
                        </Button>
                      )}
                    </div>
                  ))}
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={addParticipant}
                  disabled={isCreating}
                  className="mt-2"
                >
                  + Add Participant
                </Button>
              </div>

              <Button
                onClick={handleCreateSession}
                disabled={isCreating || isLoading}
                className="w-full"
              >
                {isCreating ? 'Creating...' : 'Create Session'}
              </Button>
            </CardContent>
          </Card>

          {/* Join Session Card */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CalendarDays className="w-5 h-5" />
                Join Existing Session
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <Label htmlFor="join-code" className="mb-2 block">
                  Session Code
                </Label>
                <Input
                  id="join-code"
                  value={joinCode}
                  onChange={e => setJoinCode(e.target.value.toUpperCase())}
                  placeholder="ABC123"
                  maxLength={6}
                  disabled={isJoining}
                  className="font-mono text-center text-lg tracking-wider"
                />
                <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                  6-character code from session creator
                </p>
              </div>

              <Button
                onClick={handleJoinSession}
                disabled={isJoining || isLoading || joinCode.length !== 6}
                className="w-full"
              >
                {isJoining ? 'Joining...' : 'Join Session'}
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* Error Display */}
        {error && (
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-4 text-red-800 dark:text-red-200">
            <p className="font-semibold">Error</p>
            <p className="text-sm">{error}</p>
          </div>
        )}

        {/* Recent Sessions (Placeholder) */}
        <Card>
          <CardHeader>
            <CardTitle>Recent Sessions</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-gray-500 dark:text-gray-400 text-center py-4">
              No recent sessions yet. Create your first session above!
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
