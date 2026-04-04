/**
 * Achievements Page (Issue #4117, #5322)
 * Display user achievements with filtering and progress tracking.
 */

import { AchievementsGrid } from '@/components/profile/AchievementsGrid';

export default function AchievementsPage() {
  return (
    <div className="container mx-auto py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-quicksand mb-2">Achievements</h1>
        <p className="text-muted-foreground font-nunito">
          Tieni traccia dei tuoi traguardi di gioco
        </p>
      </div>
      <AchievementsGrid />
    </div>
  );
}
