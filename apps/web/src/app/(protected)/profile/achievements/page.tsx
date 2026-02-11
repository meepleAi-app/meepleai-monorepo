/**
 * Achievements Page (Issue #4117)
 * Display user achievements with filtering and progress tracking
 */

'use client';

import React, { useState } from 'react';
import { Trophy, Lock, TrendingUp } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/primitives/button';

// TODO: Replace with API
interface Achievement {
  id: string;
  name: string;
  description: string;
  icon: string;
  status: 'earned' | 'locked' | 'in-progress';
  progress?: number;
  total?: number;
  earnedAt?: string;
  rarity: 'common' | 'rare' | 'epic' | 'legendary';
}

const mockAchievements: Achievement[] = [
  {
    id: '1',
    name: 'First Game',
    description: 'Play your first game',
    icon: '🎮',
    status: 'earned',
    earnedAt: '2026-01-15',
    rarity: 'common',
  },
  {
    id: '2',
    name: 'Collector',
    description: 'Own 10 games',
    icon: '📚',
    status: 'in-progress',
    progress: 7,
    total: 10,
    rarity: 'rare',
  },
  {
    id: '3',
    name: 'Master Player',
    description: 'Win 50 games',
    icon: '👑',
    status: 'locked',
    rarity: 'epic',
  },
];

export default function AchievementsPage() {
  const [filter, setFilter] = useState<'all' | 'earned' | 'locked' | 'in-progress'>('all');

  const filteredAchievements = mockAchievements.filter(
    a => filter === 'all' || a.status === filter
  );

  return (
    <div className="container mx-auto py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-quicksand mb-2">Achievements</h1>
        <p className="text-muted-foreground">Track your gaming milestones and unlock rewards</p>
      </div>

      {/* Filters */}
      <div className="flex gap-2 mb-6">
        <Button
          variant={filter === 'all' ? 'default' : 'outline'}
          onClick={() => setFilter('all')}
          size="sm"
        >
          All
        </Button>
        <Button
          variant={filter === 'earned' ? 'default' : 'outline'}
          onClick={() => setFilter('earned')}
          size="sm"
        >
          Earned
        </Button>
        <Button
          variant={filter === 'in-progress' ? 'default' : 'outline'}
          onClick={() => setFilter('in-progress')}
          size="sm"
        >
          In Progress
        </Button>
        <Button
          variant={filter === 'locked' ? 'default' : 'outline'}
          onClick={() => setFilter('locked')}
          size="sm"
        >
          Locked
        </Button>
      </div>

      {/* Achievement Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {filteredAchievements.map(achievement => (
          <div
            key={achievement.id}
            className={cn(
              'p-6 rounded-xl border transition-all',
              achievement.status === 'earned' && 'bg-card border-primary/50',
              achievement.status === 'locked' && 'bg-muted/50 border-border opacity-60',
              achievement.status === 'in-progress' && 'bg-card border-amber-500/50'
            )}
          >
            <div className="flex items-start gap-4 mb-4">
              <div className="text-4xl">{achievement.icon}</div>
              <div className="flex-1">
                <h3 className="font-bold text-lg mb-1">{achievement.name}</h3>
                <p className="text-sm text-muted-foreground">{achievement.description}</p>
              </div>
              {achievement.status === 'earned' && (
                <Trophy className="h-5 w-5 text-primary" />
              )}
              {achievement.status === 'locked' && <Lock className="h-5 w-5 text-muted-foreground" />}
              {achievement.status === 'in-progress' && (
                <TrendingUp className="h-5 w-5 text-amber-500" />
              )}
            </div>

            {/* Progress bar for in-progress */}
            {achievement.status === 'in-progress' && achievement.progress && achievement.total && (
              <div className="mb-3">
                <div className="flex justify-between text-xs mb-1">
                  <span>Progress</span>
                  <span>
                    {achievement.progress}/{achievement.total}
                  </span>
                </div>
                <div className="h-2 bg-muted rounded-full overflow-hidden">
                  <div
                    className="h-full bg-amber-500 transition-all"
                    style={{ width: `${(achievement.progress / achievement.total) * 100}%` }}
                  />
                </div>
              </div>
            )}

            {/* Earned date */}
            {achievement.status === 'earned' && achievement.earnedAt && (
              <p className="text-xs text-muted-foreground">Earned {achievement.earnedAt}</p>
            )}

            {/* Rarity badge */}
            <div className="mt-3">
              <span
                className={cn(
                  'text-xs px-2 py-1 rounded',
                  achievement.rarity === 'common' && 'bg-gray-100 text-gray-700',
                  achievement.rarity === 'rare' && 'bg-blue-100 text-blue-700',
                  achievement.rarity === 'epic' && 'bg-purple-100 text-purple-700',
                  achievement.rarity === 'legendary' && 'bg-amber-100 text-amber-700'
                )}
              >
                {achievement.rarity}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
