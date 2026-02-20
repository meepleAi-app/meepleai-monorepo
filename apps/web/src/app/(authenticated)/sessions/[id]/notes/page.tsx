/**
 * Session Notes Page - /sessions/[id]/notes
 *
 * Displays session notes and provides a personal notes editor (localStorage-based).
 * Shows the session's official notes field plus a user-editable personal notes area.
 *
 * @see Issue #4891
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft, FileText, Save } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api, type GameSessionDto } from '@/lib/api';

const STORAGE_KEY_PREFIX = 'meepleai_session_notes_';

export default function SessionNotesPage() {
  const params = useParams();
  const id = params?.id as string;
  const [session, setSession] = useState<GameSessionDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [personalNote, setPersonalNote] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    api.sessions
      .getById(id)
      .then((data) => {
        setSession(data);
        // Load personal note from localStorage
        const stored = localStorage.getItem(`${STORAGE_KEY_PREFIX}${id}`);
        if (stored) setPersonalNote(stored);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load session');
      })
      .finally(() => setLoading(false));
  }, [id]);

  const handleSave = () => {
    localStorage.setItem(`${STORAGE_KEY_PREFIX}${id}`, personalNote);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  if (loading) {
    return (
      <div className="container mx-auto py-8 px-4 max-w-3xl">
        <Skeleton className="h-8 w-48 mb-6" />
        <Skeleton className="h-48 w-full rounded-xl mb-4" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    );
  }

  if (error || !session) {
    return (
      <div className="container mx-auto py-8 px-4 max-w-3xl">
        <p className="text-destructive mb-4">{error ?? 'Session not found'}</p>
        <Button asChild variant="ghost">
          <Link href={`/sessions/${id}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Session
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4 max-w-3xl">
      {/* Header */}
      <div className="mb-6">
        <Button asChild variant="ghost" className="mb-4 font-nunito">
          <Link href={`/sessions/${id}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Session
          </Link>
        </Button>
        <div className="flex items-center gap-3">
          <FileText className="h-6 w-6 text-primary" />
          <h1 className="text-3xl font-bold font-quicksand">Session Notes</h1>
        </div>
      </div>

      <div className="space-y-6">
        {/* Session official notes */}
        <Card className="border-l-4 border-l-[hsl(240,60%,55%)] shadow-sm">
          <CardHeader>
            <CardTitle className="font-quicksand text-lg">Session Summary</CardTitle>
            <CardDescription className="font-nunito">
              Official notes recorded at session end
            </CardDescription>
          </CardHeader>
          <CardContent className="font-nunito">
            {session.notes ? (
              <p className="text-foreground whitespace-pre-wrap">{session.notes}</p>
            ) : (
              <p className="text-muted-foreground italic">No official notes recorded for this session.</p>
            )}
          </CardContent>
        </Card>

        {/* Personal notes */}
        <Card className="border-l-4 border-l-[hsl(25,95%,38%)] shadow-sm">
          <CardHeader>
            <CardTitle className="font-quicksand text-lg">My Notes</CardTitle>
            <CardDescription className="font-nunito">
              Personal notes visible only to you (saved locally)
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Textarea
              value={personalNote}
              onChange={(e) => {
                setPersonalNote(e.target.value);
                setSaved(false);
              }}
              placeholder="Add your personal notes for this session..."
              className="min-h-[160px] font-nunito resize-y"
              aria-label="Personal session notes"
            />
            <div className="flex items-center justify-between">
              <p className="text-xs text-muted-foreground font-nunito">
                Stored locally in your browser
              </p>
              <Button
                onClick={handleSave}
                size="sm"
                variant={saved ? 'secondary' : 'default'}
                className="font-nunito"
              >
                <Save className="mr-2 h-4 w-4" />
                {saved ? 'Saved!' : 'Save Notes'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
