'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Users, Search, Filter, Grid3x3, List, Shield, Award, UserX, UserCheck } from 'lucide-react';
import Link from 'next/link';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { Badge } from '@/components/ui/badge';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { StatCard } from '@/components/ui/data-display/stat-card';
import { adminClient } from '@/lib/api/admin-client';

type ViewMode = 'grid' | 'list';

interface UserDetailPanelProps {
  userId: string;
  isOpen: boolean;
  onClose: () => void;
}

function UserDetailPanel({ userId, isOpen, onClose }: UserDetailPanelProps) {
  const { data: user } = useQuery({
    queryKey: ['user-detail', userId],
    queryFn: () => adminClient.getUserDetail(userId),
    enabled: isOpen,
  });

  const { data: libraryStats } = useQuery({
    queryKey: ['user-library-stats', userId],
    queryFn: () => adminClient.getUserLibraryStats(userId),
    enabled: isOpen,
  });

  const { data: badges } = useQuery({
    queryKey: ['user-badges', userId],
    queryFn: () => adminClient.getUserBadges(userId),
    enabled: isOpen,
  });

  return (
    <Sheet open={isOpen} onOpenChange={onClose}>
      <SheetContent className="w-full sm:max-w-lg overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="text-xl font-semibold">
            User Profile
          </SheetTitle>
        </SheetHeader>

        {user && (
          <div className="mt-6 space-y-6">
            {/* User Info */}
            <div className="bg-white/60 backdrop-blur-sm rounded-xl border border-amber-200/60 p-6">
              <div className="flex items-start justify-between mb-4">
                <div>
                  <h3 className="font-quicksand font-bold text-xl text-slate-900">
                    {user.displayName}
                  </h3>
                  <p className="font-nunito text-sm text-slate-600">{user.email}</p>
                </div>
                <Badge
                  variant={user.isActive ? 'default' : 'secondary'}
                  className="font-nunito"
                >
                  {user.isActive ? 'Active' : 'Suspended'}
                </Badge>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="font-nunito text-xs text-slate-500 uppercase tracking-wider mb-1">
                    Role
                  </div>
                  <Badge variant="outline" className="font-nunito">
                    <Shield className="w-3 h-3 mr-1" />
                    {user.role}
                  </Badge>
                </div>
                <div>
                  <div className="font-nunito text-xs text-slate-500 uppercase tracking-wider mb-1">
                    Tier
                  </div>
                  <Badge
                    variant="outline"
                    className={`font-nunito ${
                      user.tier === 'premium'
                        ? 'border-amber-500 text-amber-700 bg-amber-50'
                        : user.tier === 'normal'
                        ? 'border-blue-500 text-blue-700 bg-blue-50'
                        : 'border-slate-400 text-slate-600'
                    }`}
                  >
                    {user.tier}
                  </Badge>
                </div>
              </div>
            </div>

            {/* Library Stats - Simple Display */}
            {libraryStats && (
              <div className="bg-white/60 backdrop-blur-sm rounded-xl border border-amber-200/60 p-6">
                <h4 className="font-quicksand font-semibold text-lg text-slate-900 mb-4">
                  Library Statistics
                </h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="text-center p-4 bg-gradient-to-br from-amber-100/60 to-orange-100/40 rounded-lg border border-amber-200/40">
                    <div className="font-quicksand font-bold text-3xl text-amber-900">
                      {libraryStats.gamesOwned}
                    </div>
                    <div className="font-nunito text-xs text-amber-700 uppercase tracking-wider mt-1">
                      Games Owned
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-emerald-100/60 to-green-100/40 rounded-lg border border-emerald-200/40">
                    <div className="font-quicksand font-bold text-3xl text-emerald-900">
                      {libraryStats.totalPlays}
                    </div>
                    <div className="font-nunito text-xs text-emerald-700 uppercase tracking-wider mt-1">
                      Total Plays
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-blue-100/60 to-cyan-100/40 rounded-lg border border-blue-200/40">
                    <div className="font-quicksand font-bold text-3xl text-blue-900">
                      {libraryStats.wishlistCount}
                    </div>
                    <div className="font-nunito text-xs text-blue-700 uppercase tracking-wider mt-1">
                      Wishlist
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-purple-100/60 to-pink-100/40 rounded-lg border border-purple-200/40">
                    <div className="font-quicksand font-bold text-3xl text-purple-900">
                      {libraryStats.averageRating.toFixed(1)}
                    </div>
                    <div className="font-nunito text-xs text-purple-700 uppercase tracking-wider mt-1">
                      Avg Rating
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Badges */}
            {badges && badges.length > 0 && (
              <div className="bg-white/60 backdrop-blur-sm rounded-xl border border-amber-200/60 p-6">
                <h4 className="font-quicksand font-semibold text-lg text-slate-900 mb-4 flex items-center gap-2">
                  <Award className="w-5 h-5 text-amber-600" />
                  Achievements
                </h4>
                <div className="space-y-2">
                  {badges.map((badge) => (
                    <div
                      key={badge.id}
                      className="flex items-center gap-3 p-3 bg-gradient-to-r from-amber-50 to-orange-50/50 rounded-lg border border-amber-200/40"
                    >
                      {badge.icon && <span className="text-2xl">{badge.icon}</span>}
                      <div className="flex-1">
                        <div className="font-nunito font-semibold text-sm text-slate-900">
                          {badge.name}
                        </div>
                        <div className="font-nunito text-xs text-slate-600">
                          {badge.description}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}

export function UserManagementBlock() {
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [tierFilter, setTierFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin-users', roleFilter, tierFilter, searchQuery],
    queryFn: () =>
      adminClient.getUsers({
        page: 1,
        pageSize: 6,
        role: roleFilter !== 'all' ? roleFilter : undefined,
        tier: tierFilter !== 'all' ? tierFilter : undefined,
        search: searchQuery || undefined,
      }),
  });

  const suspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.suspendUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({
        title: 'User suspended',
        description: 'User account has been suspended',
      });
    },
  });

  const unsuspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.unsuspendUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({
        title: 'User unsuspended',
        description: 'User account has been reactivated',
      });
    },
  });

  return (
    <div className="space-y-6">
      {/* Block Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="text-xl font-semibold text-foreground">
            User Management
          </h2>
          <Badge variant="secondary">
            {data?.totalCount ?? 0} users
          </Badge>
        </div>
        <Link
          href="/admin/users/management"
          className="text-sm text-primary hover:text-primary/80 font-medium transition-colors"
        >
          View All →
        </Link>
      </div>

      {/* Filters and View Toggle */}
      <div className="bg-card border rounded-lg p-4">
        <div className="flex flex-col lg:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Search by name or email..."
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
              className="pl-10 bg-background focus:border-amber-400"
            />
          </div>

          {/* Role Filter */}
          <Select value={roleFilter} onValueChange={setRoleFilter}>
            <SelectTrigger className="w-[140px] bg-background">
              <Filter className="w-4 h-4 mr-2" />
              <SelectValue placeholder="Role" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Roles</SelectItem>
              <SelectItem value="user">User</SelectItem>
              <SelectItem value="admin">Admin</SelectItem>
            </SelectContent>
          </Select>

          {/* Tier Filter */}
          <Select value={tierFilter} onValueChange={setTierFilter}>
            <SelectTrigger className="w-[140px] bg-background">
              <Filter className="w-4 h-4 mr-2" />
              <SelectValue placeholder="Tier" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Tiers</SelectItem>
              <SelectItem value="free">Free</SelectItem>
              <SelectItem value="normal">Normal</SelectItem>
              <SelectItem value="premium">Premium</SelectItem>
            </SelectContent>
          </Select>

          {/* View Toggle */}
          <div className="flex gap-1 bg-white/80 border border-amber-200/60 rounded-lg p-1">
            <Button
              size="sm"
              variant={viewMode === 'grid' ? 'default' : 'ghost'}
              onClick={() => setViewMode('grid')}
              className="h-8 w-8 p-0"
            >
              <Grid3x3 className="w-4 h-4" />
            </Button>
            <Button
              size="sm"
              variant={viewMode === 'list' ? 'default' : 'ghost'}
              onClick={() => setViewMode('list')}
              className="h-8 w-8 p-0"
            >
              <List className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Users Display - Using MeepleCard */}
      {isLoading ? (
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {[...Array(6)].map((_, i) => (
            <div
              key={i}
              className="h-[240px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 animate-pulse"
            />
          ))}
        </div>
      ) : !data || !data.items || data.items.length === 0 ? (
        <div className="text-center py-12 bg-white/50 backdrop-blur-sm rounded-xl border border-amber-200/60">
          <p className="font-nunito text-slate-500">No users found</p>
        </div>
      ) : (
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {data?.items.map((user) => (
            <MeepleCard
              key={user.id}
              id={user.id}
              entity="player"
              variant={viewMode === 'grid' ? 'grid' : 'list'}
              title={user.displayName}
              subtitle={user.email}
              metadata={[
                { label: user.role },
                { label: `Level ${user.level}` },
                { label: user.tier },
              ]}
              rating={user.experiencePoints / 1000}
              ratingMax={10}
              actions={[
                {
                  label: 'View Profile',
                  primary: true,
                  onClick: () => setSelectedUserId(user.id),
                },
                {
                  label: user.isActive ? 'Suspend' : 'Unsuspend',
                  onClick: () =>
                    user.isActive
                      ? suspendMutation.mutate(user.id)
                      : unsuspendMutation.mutate(user.id),
                },
              ]}
              badge={user.isActive ? 'ACTIVE' : 'SUSPENDED'}
              onClick={() => setSelectedUserId(user.id)}
            />
          ))}
        </div>
      )}

      {/* User Detail Panel */}
      {selectedUserId && (
        <UserDetailPanel
          userId={selectedUserId}
          isOpen={!!selectedUserId}
          onClose={() => setSelectedUserId(null)}
        />
      )}
    </div>
  );
}
