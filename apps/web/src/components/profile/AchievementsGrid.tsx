'use client';

import React, { useState } from 'react';

import { Trophy, Lock, TrendingUp, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useAchievements, type AchievementDto } from '@/hooks/queries/useAchievements';
import { cn } from '@/lib/utils';

// Re-export the type for downstream consumers (e.g. `lib/sessions-summary/fsm.ts`)
// that imported it from this module before the hook extraction (#1542).
export type { AchievementDto };

type AchievementFilter = 'all' | 'earned' | 'locked' | 'in-progress';

function getStatus(a: AchievementDto): 'earned' | 'locked' | 'in-progress' {
  if (a.isUnlocked) return 'earned';
  if (a.progress !== null && a.progress > 0) return 'in-progress';
  return 'locked';
}

function getIcon(a: AchievementDto): string {
  if (a.iconUrl) return a.iconUrl;
  switch (a.category.toLowerCase()) {
    case 'gameplay':
      return '🎮';
    case 'collection':
      return '📚';
    case 'social':
      return '🤝';
    default:
      return '🏆';
  }
}

export function AchievementsGrid(): React.ReactElement {
  const [filter, setFilter] = useState<AchievementFilter>('all');

  const { data: achievements, isLoading, error } = useAchievements();

  const filtered = (achievements ?? []).filter(a => {
    if (filter === 'all') return true;
    return getStatus(a) === filter;
  });

  return (
    <div>
      {/* Filters */}
      <div className="flex gap-2 mb-6">
        {(['all', 'earned', 'in-progress', 'locked'] as const).map(f => (
          <Button
            key={f}
            variant={filter === f ? 'default' : 'outline'}
            onClick={() => setFilter(f)}
            size="sm"
          >
            {f === 'all'
              ? 'Tutti'
              : f === 'earned'
                ? 'Ottenuti'
                : f === 'in-progress'
                  ? 'In Corso'
                  : 'Bloccati'}
          </Button>
        ))}
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="text-center py-16 text-muted-foreground">
          <p>Impossibile caricare gli achievements. Riprova più tardi.</p>
        </div>
      )}

      {/* Empty state */}
      {!isLoading && !error && filtered.length === 0 && (
        <div className="text-center py-16 text-muted-foreground">
          <Trophy className="h-12 w-12 mx-auto mb-4 opacity-50" />
          <p className="text-lg font-medium">
            {filter === 'all' ? 'Nessun achievement disponibile' : `Nessun achievement "${filter}"`}
          </p>
          <p className="text-sm mt-1">Inizia a giocare per sbloccare achievements!</p>
        </div>
      )}

      {/* Achievement Grid */}
      {!isLoading && !error && filtered.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filtered.map(achievement => {
            const status = getStatus(achievement);
            const icon = getIcon(achievement);
            return (
              <div
                key={achievement.id}
                className={cn(
                  'p-6 rounded-xl border transition-all',
                  status === 'earned' && 'bg-card border-primary/50',
                  status === 'locked' && 'bg-muted/50 border-border opacity-60',
                  status === 'in-progress' && 'bg-card border-amber-500/50'
                )}
              >
                <div className="flex items-start gap-4 mb-4">
                  <div className="text-4xl">{icon}</div>
                  <div className="flex-1">
                    <h3 className="font-bold text-lg mb-1">{achievement.name}</h3>
                    <p className="text-sm text-muted-foreground">{achievement.description}</p>
                  </div>
                  {status === 'earned' && <Trophy className="h-5 w-5 text-primary" />}
                  {status === 'locked' && <Lock className="h-5 w-5 text-muted-foreground" />}
                  {status === 'in-progress' && <TrendingUp className="h-5 w-5 text-amber-500" />}
                </div>

                {status === 'in-progress' &&
                  achievement.progress !== null &&
                  achievement.threshold > 0 && (
                    <div className="mb-3">
                      <div className="flex justify-between text-xs mb-1">
                        <span>Progresso</span>
                        <span>
                          {achievement.progress}/{achievement.threshold}
                        </span>
                      </div>
                      <div className="h-2 bg-muted rounded-full overflow-hidden">
                        <div
                          className="h-full bg-amber-500 transition-all"
                          style={{
                            width: `${Math.min((achievement.progress / achievement.threshold) * 100, 100)}%`,
                          }}
                        />
                      </div>
                    </div>
                  )}

                {status === 'earned' && achievement.unlockedAt && (
                  <p className="text-xs text-muted-foreground">
                    Ottenuto il {new Date(achievement.unlockedAt).toLocaleDateString('it-IT')}
                  </p>
                )}

                <div className="mt-3">
                  <span
                    className={cn(
                      'text-xs px-2 py-1 rounded',
                      achievement.rarity.toLowerCase() === 'common' && 'bg-gray-100 text-gray-700',
                      achievement.rarity.toLowerCase() === 'rare' && 'bg-blue-100 text-blue-700',
                      achievement.rarity.toLowerCase() === 'epic' &&
                        'bg-purple-100 text-purple-700',
                      achievement.rarity.toLowerCase() === 'legendary' &&
                        'bg-amber-100 text-amber-700'
                    )}
                  >
                    {achievement.rarity}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
