'use client';

import { Sparkles } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface AIInsight {
  recommendations: Array<{
    gameId: string;
    gameName: string;
    reason: string;
    confidence: number;
  }>;
  backlogAlerts: Array<{
    gameId: string;
    gameName: string;
    daysSinceLastPlayed: number;
  }>;
  rulesReminders: Array<{
    chatId: string;
    topic: string;
    gameName: string;
  }>;
}

interface AIInsightsWidgetProps {
  insights?: AIInsight;
  isLoading?: boolean;
}

/**
 * AI Insights Widget - Display RAG-powered recommendations and alerts
 * Issue #3919: Frontend widget for AI insights service
 */
export function AIInsightsWidget({ insights, isLoading }: AIInsightsWidgetProps) {
  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-purple-500/10 via-blue-500/10 to-purple-500/10 border-purple-500/20">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-purple-600 animate-pulse" />
            AI Insights
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-3 animate-pulse">
            <div className="h-12 bg-purple-200/20 rounded" />
            <div className="h-12 bg-blue-200/20 rounded" />
            <div className="h-12 bg-purple-200/20 rounded" />
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!insights || (insights.recommendations.length === 0 && insights.backlogAlerts.length === 0 && insights.rulesReminders.length === 0)) {
    return (
      <Card className="bg-gradient-to-br from-purple-500/10 via-blue-500/10 to-purple-500/10 border-purple-500/20">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-purple-600" />
            AI Insights
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground text-center py-4">
            Nessun insight disponibile al momento
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-purple-500/10 via-blue-500/10 to-purple-500/10 border-purple-500/20 hover:shadow-lg hover:shadow-purple-500/20 transition-shadow">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-purple-600" />
          AI Insights
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Recommendations */}
        {insights.recommendations.length > 0 && (
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-purple-700">Raccomandazioni</h3>
            <div className="space-y-2">
              {insights.recommendations.slice(0, 3).map((rec) => (
                <Link
                  key={rec.gameId}
                  href={`/games/${rec.gameId}`}
                  className="block p-3 rounded-lg bg-white/50 hover:bg-white/80 transition-colors border border-purple-200/50"
                >
                  <div className="font-medium text-sm">{rec.gameName}</div>
                  <div className="text-xs text-muted-foreground mt-1">{rec.reason}</div>
                  <div className="text-xs text-purple-600 mt-1">
                    Confidence: {(rec.confidence * 100).toFixed(0)}%
                  </div>
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* Backlog Alerts */}
        {insights.backlogAlerts.length > 0 && (
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-blue-700">Backlog Alerts</h3>
            <Link
              href="/library?filter=unplayed"
              className="block p-3 rounded-lg bg-white/50 hover:bg-white/80 transition-colors border border-blue-200/50"
            >
              <div className="text-sm font-medium">
                {insights.backlogAlerts.length} giochi non giocati
              </div>
              <div className="text-xs text-muted-foreground mt-1">
                Alcuni non giocati da 30+ giorni
              </div>
            </Link>
          </div>
        )}

        {/* Rules Reminders */}
        {insights.rulesReminders.length > 0 && (
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-purple-700">Rules Reminders</h3>
            <div className="space-y-2">
              {insights.rulesReminders.slice(0, 2).map((reminder) => (
                <div
                  key={reminder.chatId}
                  className="p-3 rounded-lg bg-white/50 border border-purple-200/50"
                >
                  <div className="font-medium text-sm">{reminder.gameName}</div>
                  <div className="text-xs text-muted-foreground mt-1">{reminder.topic}</div>
                </div>
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
