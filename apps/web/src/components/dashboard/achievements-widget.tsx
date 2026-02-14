"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import Link from "next/link";

interface Achievement {
  id: string;
  title: string;
  description: string;
  icon: string;
  rarity: "common" | "rare" | "epic" | "legendary";
  unlockedAt?: Date;
}

const rarityStyles = {
  common: "bg-stone-100 dark:bg-stone-800 border-stone-300 dark:border-stone-700",
  rare: "bg-blue-100 dark:bg-blue-900/30 border-blue-300 dark:border-blue-700",
  epic: "bg-purple-100 dark:bg-purple-900/30 border-purple-300 dark:border-purple-700",
  legendary: "bg-amber-100 dark:bg-amber-900/30 border-amber-300 dark:border-amber-700",
};

export function AchievementsWidget({ achievements = [], totalCount = 0 }: { achievements?: Achievement[]; totalCount?: number }) {
  if (achievements.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand">Achievement</CardTitle>
          <CardDescription>Nessun achievement sbloccato</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="font-quicksand">Achievement</CardTitle>
        <CardDescription>Ultimi {achievements.length} sbloccati</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          {achievements.slice(0, 3).map((achievement) => (
            <div
              key={achievement.id}
              className={`p-4 rounded-lg border-2 ${rarityStyles[achievement.rarity]} hover:scale-105 transition-transform`}
            >
              <div className="text-3xl mb-2">{achievement.icon}</div>
              <p className="font-quicksand font-semibold text-sm">{achievement.title}</p>
              <p className="text-xs text-stone-600 dark:text-stone-400 mt-1">{achievement.description}</p>
            </div>
          ))}
        </div>
        {totalCount > 3 && (
          <Button asChild variant="link" className="w-full">
            <Link href="/achievements">Vedi Tutti i Badge ({totalCount}) →</Link>
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
