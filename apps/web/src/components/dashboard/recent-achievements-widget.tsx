/**
 * Recent Achievements Widget (Issue #4117)
 * Dashboard widget showing last 3 earned achievements
 */

'use client';

import React from 'react';

import { Trophy, ArrowRight } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export function RecentAchievementsWidget() {
  // TODO: Fetch from API GET /achievements/recent
  const recentAchievements = [
    { id: '1', name: 'First Win', icon: '🏆', earnedAt: '2 hours ago', rarity: 'common' },
    { id: '2', name: '10 Games Played', icon: '🎮', earnedAt: '1 day ago', rarity: 'rare' },
    { id: '3', name: 'Library Builder', icon: '📚', earnedAt: '3 days ago', rarity: 'rare' },
  ];

  return (
    <div className="bg-card rounded-xl p-6 border border-border/50">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Trophy className="h-5 w-5 text-amber-500" />
          <h2 className="text-xl font-bold font-quicksand">Recent Achievements</h2>
        </div>
        <Link href="/profile/achievements">
          <Button variant="ghost" size="sm" className="gap-1">
            View All
            <ArrowRight className="h-4 w-4" />
          </Button>
        </Link>
      </div>

      <div className="space-y-3">
        {recentAchievements.map(achievement => (
          <div
            key={achievement.id}
            className="flex items-center gap-3 p-3 rounded-lg bg-accent/50 hover:bg-accent transition-colors"
          >
            <div className="text-2xl">{achievement.icon}</div>
            <div className="flex-1">
              <p className="font-medium">{achievement.name}</p>
              <p className="text-xs text-muted-foreground">{achievement.earnedAt}</p>
            </div>
            <span
              className={cn(
                'text-xs px-2 py-1 rounded',
                achievement.rarity === 'common' && 'bg-gray-100 text-gray-700',
                achievement.rarity === 'rare' && 'bg-blue-100 text-blue-700',
                achievement.rarity === 'epic' && 'bg-purple-100 text-purple-700'
              )}
            >
              {achievement.rarity}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
