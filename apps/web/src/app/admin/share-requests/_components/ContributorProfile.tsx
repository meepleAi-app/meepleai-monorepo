import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui';
import { Badge } from '@/components/ui';
import { formatDistanceToNow } from 'date-fns';
import { Award, Calendar, CheckCircle, TrendingUp } from 'lucide-react';
import type { ContributorProfileDto } from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Contributor Profile Component
 *
 * Displays contributor information in admin review sidebar:
 * - Avatar and name
 * - Join date
 * - Total and approved contributions
 * - Approval rate
 * - Earned badges
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ContributorProfileProps {
  contributor: ContributorProfileDto;
  className?: string;
}

export function ContributorProfile({ contributor, className }: ContributorProfileProps){
  const approvalRatePercentage = Math.round(contributor.approvalRate * 100);

  return (
    <div className={`space-y-4 ${className ?? ''}`}>
      {/* Avatar and Name */}
      <div className="flex items-center gap-3">
        <Avatar className="h-12 w-12">
          <AvatarImage src={contributor.avatarUrl ?? undefined} alt={contributor.userName} />
          <AvatarFallback className="bg-primary text-primary-foreground">
            {contributor.userName.substring(0, 2).toUpperCase()}
          </AvatarFallback>
        </Avatar>
        <div>
          <p className="font-medium">{contributor.userName}</p>
          <p className="text-sm text-muted-foreground flex items-center gap-1">
            <Calendar className="h-3 w-3" />
            Joined {formatDistanceToNow(new Date(contributor.joinedAt), { addSuffix: true })}
          </p>
        </div>
      </div>

      {/* Stats */}
      <div className="space-y-2 rounded-lg border p-3">
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground flex items-center gap-1">
            <TrendingUp className="h-3 w-3" />
            Total Contributions
          </span>
          <span className="font-medium">{contributor.totalContributions}</span>
        </div>
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground flex items-center gap-1">
            <CheckCircle className="h-3 w-3" />
            Approved
          </span>
          <span className="font-medium">{contributor.approvedContributions}</span>
        </div>
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">Approval Rate</span>
          <span
            className={`font-medium ${
              approvalRatePercentage >= 80
                ? 'text-green-600'
                : approvalRatePercentage >= 50
                  ? 'text-yellow-600'
                  : 'text-red-600'
            }`}
          >
            {approvalRatePercentage}%
          </span>
        </div>
      </div>

      {/* Badges */}
      {contributor.badges.length > 0 && (
        <div className="space-y-2">
          <p className="text-sm font-medium flex items-center gap-1">
            <Award className="h-4 w-4" />
            Badges ({contributor.badges.length})
          </p>
          <div className="flex flex-wrap gap-2">
            {contributor.badges.map((badge) => (
              <Badge
                key={badge.id}
                variant="outline"
                className="border-amber-300 bg-amber-50 text-amber-700"
                title={`Earned ${formatDistanceToNow(new Date(badge.awardedAt), { addSuffix: true })}`}
              >
                {badge.iconUrl && (
                  <img src={badge.iconUrl} alt="" className="mr-1 h-3 w-3" />
                )}
                {badge.name}
              </Badge>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
