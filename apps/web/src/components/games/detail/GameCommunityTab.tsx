/**
 * Game Community Tab Component
 *
 * Placeholder for future community features:
 * - Comments and discussions
 * - User reviews and ratings
 * - Q&A section
 * - Community-contributed content
 */

import React from 'react';
import { Game } from '@/lib/api';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { MessageSquare, Star, HelpCircle, Users, AlertCircle } from 'lucide-react';

interface GameCommunityTabProps {
  game: Game;
}

export function GameCommunityTab({ game }: GameCommunityTabProps) {
  return (
    <div className="space-y-6" role="region" aria-label="Community features">
      {/* Overview Alert */}
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          Community features are coming soon! This tab will include discussions, reviews, Q&A, and
          user-contributed content.
        </AlertDescription>
      </Alert>

      {/* Comments & Discussions */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MessageSquare className="h-5 w-5" />
            Comments & Discussions
          </CardTitle>
          <CardDescription>
            Community discussions about rules, strategies, and gameplay
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
            <div className="w-16 h-16 mb-4 rounded-full bg-muted flex items-center justify-center">
              <MessageSquare className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-semibold mb-2">Discussion Board Coming Soon</h3>
            <p className="text-muted-foreground max-w-md">
              Share insights, ask questions, and discuss strategies with other players.
            </p>
          </div>
        </CardContent>
      </Card>

      {/* User Reviews & Ratings */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Star className="h-5 w-5" />
            User Reviews & Ratings
          </CardTitle>
          <CardDescription>Rate this game and read what other players think</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
            <div className="w-16 h-16 mb-4 rounded-full bg-muted flex items-center justify-center">
              <Star className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-semibold mb-2">Reviews & Ratings Coming Soon</h3>
            <p className="text-muted-foreground max-w-md">
              Share your experience and help others discover great games.
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Q&A Section */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <HelpCircle className="h-5 w-5" />
            Questions & Answers
          </CardTitle>
          <CardDescription>
            Get help with rules clarifications and gameplay questions
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
            <div className="w-16 h-16 mb-4 rounded-full bg-muted flex items-center justify-center">
              <HelpCircle className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-semibold mb-2">Q&A Section Coming Soon</h3>
            <p className="text-muted-foreground max-w-md">
              Ask questions and get answers from experienced players and the community.
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Community Contributors */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            Community Contributors
          </CardTitle>
          <CardDescription>Top contributors and helpful community members</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
            <div className="w-16 h-16 mb-4 rounded-full bg-muted flex items-center justify-center">
              <Users className="h-8 w-8 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-semibold mb-2">Contributor Leaderboard Coming Soon</h3>
            <p className="text-muted-foreground max-w-md">
              Recognize and celebrate community members who help make MeepleAI better.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
