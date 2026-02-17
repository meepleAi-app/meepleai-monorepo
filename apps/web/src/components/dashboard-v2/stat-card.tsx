/**
 * StatCard Component - Issue #4581
 * Single stat display card with glassmorphic design
 */

import { Card, CardContent } from '@/components/ui/card';

interface StatCardProps {
  icon: string;
  value: number | string;
  label: string;
  sublabel?: string;
  className?: string;
}

export function StatCard({ icon, value, label, sublabel, className = '' }: StatCardProps) {
  return (
    <Card className={`bg-white/70 backdrop-blur-md border border-white/20 ${className}`}>
      <CardContent className="pt-6">
        <div className="text-4xl mb-2" role="img" aria-label={label}>
          {icon}
        </div>
        <div className="text-3xl font-bold font-quicksand">{value}</div>
        <div className="text-sm text-muted-foreground font-nunito">{label}</div>
        {sublabel && (
          <div className="text-xs text-muted-foreground mt-1">{sublabel}</div>
        )}
      </CardContent>
    </Card>
  );
}
