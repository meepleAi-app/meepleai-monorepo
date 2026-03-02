/**
 * Game Rules Page - /games/[id]/rules
 *
 * Shows all rule versions for a game using api.games.getRules(id).
 * Displays rules organized by version with atoms (rule items).
 *
 * @see Issue #4889
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, BookOpen, ChevronDown, ChevronRight, FileText } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { RuleSpec } from '@/lib/api/schemas/games.schemas';

function RuleVersionCard({ spec }: { spec: RuleSpec }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-sm">
      <CardHeader className="pb-2">
        <button
          onClick={() => setExpanded(v => !v)}
          className="flex items-center justify-between w-full text-left"
        >
          <div className="flex items-center gap-3">
            <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
            <CardTitle className="font-quicksand text-base">Version {spec.version}</CardTitle>
            <Badge variant="secondary" className="font-nunito text-xs">
              {spec.atoms.length} rule{spec.atoms.length !== 1 ? 's' : ''}
            </Badge>
          </div>
          <div className="flex items-center gap-2 text-muted-foreground text-xs font-nunito">
            <span>{new Date(spec.createdAt).toLocaleDateString()}</span>
            {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </div>
        </button>
      </CardHeader>
      {expanded && (
        <CardContent>
          {spec.atoms.length === 0 ? (
            <p className="text-sm text-muted-foreground font-nunito">No rules in this version.</p>
          ) : (
            <ul className="space-y-3">
              {spec.atoms.map(atom => (
                <li key={atom.id} className="border-l-2 border-muted pl-3">
                  {atom.section && (
                    <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide font-nunito mb-1">
                      {atom.section}
                      {atom.page !== null && ` · p.${atom.page}`}
                    </p>
                  )}
                  <p className="text-sm font-nunito">{atom.text}</p>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      )}
    </Card>
  );
}

export default function GameRulesPage() {
  const params = useParams();
  const gameId = params?.id as string;

  const [rules, setRules] = useState<RuleSpec[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!gameId) return;
    setIsLoading(true);
    api.games
      .getRules(gameId)
      .then(data => setRules(data))
      .catch(err => setError(err instanceof Error ? err : new Error(String(err))))
      .finally(() => setIsLoading(false));
  }, [gameId]);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/games/${gameId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Game
          </Link>
        </Button>

        {/* Header */}
        <div className="flex items-center gap-3 mb-8">
          <BookOpen className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Rules</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Rule specifications and versions
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-4">
            {[...Array(3)].map((_, i) => (
              <Skeleton key={i} className="h-16 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load rules: {error.message}
            </AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!isLoading && !error && rules !== null && (
          <>
            {rules.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No rules have been published for this game yet.
                </CardContent>
              </Card>
            ) : (
              <div className="space-y-4">
                {rules.map(spec => (
                  <RuleVersionCard key={spec.id} spec={spec} />
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
