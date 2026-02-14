"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import Link from "next/link";
import { AlertCircle, BookOpen, Flame, Target } from "lucide-react";

interface AIInsight {
  id: string;
  type: "backlog" | "rulesReminder" | "recommendation" | "streak";
  title: string;
  description: string;
  actionLabel: string;
  actionUrl: string;
  priority: number;
}

interface AIInsightsWidgetProps {
  insights?: AIInsight[];
  isLoading?: boolean;
  onInsightClick?: (insight: AIInsight) => void;
}

const iconMap = {
  backlog: Target,
  rulesReminder: BookOpen,
  recommendation: AlertCircle,
  streak: Flame,
};

export function AIInsightsWidget({ insights = [], isLoading, onInsightClick }: AIInsightsWidgetProps) {
  if (isLoading) {
    return (
      <Card className="bg-gradient-to-br from-amber-50 to-yellow-50 dark:from-amber-950/20 dark:to-yellow-950/20 border-2 border-amber-200 dark:border-amber-800">
        <CardHeader>
          <CardTitle className="font-quicksand">Suggerimenti AI</CardTitle>
          <CardDescription>Caricamento...</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </CardContent>
      </Card>
    );
  }

  if (insights.length === 0) {
    return (
      <Card className="bg-gradient-to-br from-amber-50 to-yellow-50 dark:from-amber-950/20 dark:to-yellow-950/20 border-2 border-amber-200 dark:border-amber-800">
        <CardHeader>
          <CardTitle className="font-quicksand">Suggerimenti AI</CardTitle>
          <CardDescription>Nessun suggerimento disponibile al momento</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card className="bg-gradient-to-br from-amber-50 to-yellow-50 dark:from-amber-950/20 dark:to-yellow-950/20 border-2 border-amber-200 dark:border-amber-800">
      <CardHeader>
        <CardTitle className="font-quicksand">Suggerimenti AI</CardTitle>
        <CardDescription>Scopri azioni per migliorare la tua esperienza</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {insights.slice(0, 5).map((insight) => {
          const Icon = iconMap[insight.type];
          return (
            <Link
              key={insight.id}
              href={insight.actionUrl}
              onClick={() => onInsightClick?.(insight)}
              className="block group"
            >
              <div className="flex items-start gap-3 p-3 rounded-lg hover:bg-amber-100/50 dark:hover:bg-amber-900/20 transition-all hover:scale-[1.02] hover:shadow-md">
                <div className="mt-0.5 text-amber-600 dark:text-amber-400">
                  <Icon className="h-6 w-6" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-quicksand font-semibold text-sm text-stone-900 dark:text-stone-100 group-hover:text-amber-900 dark:group-hover:text-amber-100">
                    {insight.title}
                  </p>
                  <p className="text-xs text-stone-600 dark:text-stone-400 mt-0.5 font-nunito">
                    {insight.description}
                  </p>
                </div>
                <div className="text-xs text-amber-600 dark:text-amber-400 font-nunito whitespace-nowrap">
                  {insight.actionLabel}
                </div>
              </div>
            </Link>
          );
        })}
      </CardContent>
    </Card>
  );
}
