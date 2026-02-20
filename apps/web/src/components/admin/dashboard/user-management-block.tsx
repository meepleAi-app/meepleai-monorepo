'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Search, Filter, Grid3x3, List, Shield, Award, Mail, ArrowUpDown, UserX, Eye, Table2, ChevronDown } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { useToast } from '@/hooks/use-toast';
import { useDebounce } from '@/hooks/useDebounce';
import { adminClient } from '@/lib/api/admin-client';

type ViewMode = 'grid' | 'list' | 'table';

interface UserDetailPanelProps {
  userId: string;
  isOpen: boolean;
  onClose: () => void;
}

function UserDetailPanel({ userId, isOpen, onClose }: UserDetailPanelProps) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

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

  const changeTierMutation = useMutation({
    mutationFn: (tier: string) => adminClient.updateUserTier(userId, tier),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['user-detail', userId] });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({ title: 'Tier updated', description: 'User tier has been changed' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to update tier', variant: 'destructive' });
    },
  });

  const suspendMutation = useMutation({
    mutationFn: () => user?.isActive ? adminClient.suspendUser(userId) : adminClient.unsuspendUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['user-detail', userId] });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({
        title: user?.isActive ? 'User suspended' : 'User unsuspended',
        description: user?.isActive ? 'Account has been suspended' : 'Account has been reactivated',
      });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to update user status', variant: 'destructive' });
    },
  });

  const impersonateMutation = useMutation({
    mutationFn: () => adminClient.impersonateUser(userId),
    onSuccess: (result) => {
      if (!result?.sessionToken) {
        toast({ title: 'Error', description: 'Impersonation failed: no session returned', variant: 'destructive' });
        return;
      }
      toast({ title: 'Impersonation started', description: `Session token: ${result.sessionToken.slice(0, 8)}...` });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to impersonate user', variant: 'destructive' });
    },
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

            {/* Library Stats */}
            {libraryStats && (
              <div className="bg-white/60 backdrop-blur-sm rounded-xl border border-amber-200/60 p-6">
                <h4 className="font-quicksand font-semibold text-lg text-slate-900 mb-4">
                  Library Statistics
                </h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="text-center p-4 bg-gradient-to-br from-amber-100/60 to-orange-100/40 rounded-lg border border-amber-200/40">
                    <div className="font-quicksand font-bold text-3xl text-amber-900">
                      {libraryStats.totalGames}
                    </div>
                    <div className="font-nunito text-xs text-amber-700 uppercase tracking-wider mt-1">
                      Games Owned
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-emerald-100/60 to-green-100/40 rounded-lg border border-emerald-200/40">
                    <div className="font-quicksand font-bold text-3xl text-emerald-900">
                      {libraryStats.sessionsPlayed}
                    </div>
                    <div className="font-nunito text-xs text-emerald-700 uppercase tracking-wider mt-1">
                      Sessions Played
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-blue-100/60 to-cyan-100/40 rounded-lg border border-blue-200/40">
                    <div className="font-quicksand font-bold text-3xl text-blue-900">
                      {libraryStats.favoriteGames}
                    </div>
                    <div className="font-nunito text-xs text-blue-700 uppercase tracking-wider mt-1">
                      Favorites
                    </div>
                  </div>
                  <div className="text-center p-4 bg-gradient-to-br from-purple-100/60 to-pink-100/40 rounded-lg border border-purple-200/40">
                    <div className="font-quicksand font-bold text-3xl text-purple-900">
                      {libraryStats.newestAddedAt
                        ? new Date(libraryStats.newestAddedAt).toLocaleDateString()
                        : '-'}
                    </div>
                    <div className="font-nunito text-xs text-purple-700 uppercase tracking-wider mt-1">
                      Last Added
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
                      {badge.iconUrl && <span className="text-2xl">{badge.iconUrl}</span>}
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

            {/* Quick Actions */}
            <div className="bg-white/60 backdrop-blur-sm rounded-xl border border-amber-200/60 p-6">
              <h4 className="font-quicksand font-semibold text-lg text-slate-900 mb-4">
                Quick Actions
              </h4>
              <div className="grid grid-cols-2 gap-3">
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-2 justify-start"
                  onClick={() => {
                    if (user.email) {
                      window.open(`mailto:${user.email}`, '_blank');
                    }
                  }}
                >
                  <Mail className="w-4 h-4" />
                  Email
                </Button>
                <Select
                  value={user.tier}
                  onValueChange={(tier) => changeTierMutation.mutate(tier)}
                >
                  <SelectTrigger className="h-9 gap-2">
                    <ArrowUpDown className="w-4 h-4" />
                    <SelectValue placeholder="Change Tier" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="free">Free</SelectItem>
                    <SelectItem value="normal">Normal</SelectItem>
                    <SelectItem value="premium">Premium</SelectItem>
                  </SelectContent>
                </Select>
                <Button
                  variant={user.isActive ? 'destructive' : 'default'}
                  size="sm"
                  className="gap-2 justify-start"
                  onClick={() => suspendMutation.mutate()}
                  disabled={suspendMutation.isPending}
                >
                  <UserX className="w-4 h-4" />
                  {user.isActive ? 'Suspend' : 'Unsuspend'}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-2 justify-start"
                  onClick={() => impersonateMutation.mutate()}
                  disabled={impersonateMutation.isPending}
                >
                  <Eye className="w-4 h-4" />
                  Impersonate
                </Button>
              </div>
            </div>
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
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const debouncedSearch = useDebounce(searchQuery, 300);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id],
    );
  };

  const toggleSelectAll = () => {
    if (!data?.items) return;
    if (selectedIds.length === data.items.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(data.items.map((u) => u.id));
    }
  };

  const queryKey = ['admin-users', roleFilter, tierFilter, debouncedSearch];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      adminClient.getUsers({
        page: 1,
        pageSize: 6,
        role: roleFilter !== 'all' ? roleFilter : undefined,
        tier: tierFilter !== 'all' ? tierFilter : undefined,
        search: debouncedSearch || undefined,
      }),
    staleTime: 2 * 60 * 1000,
  });

  const suspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.suspendUser(userId),
    onMutate: async (userId) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData(queryKey);
      queryClient.setQueryData(queryKey, (old: typeof data) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((u) =>
            u.id === userId ? { ...u, isActive: false } : u
          ),
        };
      });
      return { previous };
    },
    onError: (_err, _userId, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast({ title: 'Error', description: 'Failed to suspend user', variant: 'destructive' });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({ title: 'User suspended', description: 'User account has been suspended' });
    },
  });

  const unsuspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.unsuspendUser(userId),
    onMutate: async (userId) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData(queryKey);
      queryClient.setQueryData(queryKey, (old: typeof data) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((u) =>
            u.id === userId ? { ...u, isActive: true } : u
          ),
        };
      });
      return { previous };
    },
    onError: (_err, _userId, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast({ title: 'Error', description: 'Failed to unsuspend user', variant: 'destructive' });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast({ title: 'User unsuspended', description: 'User account has been reactivated' });
    },
  });

  const bulkSuspendMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map((id) => adminClient.suspendUser(id)));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      setSelectedIds([]);
      toast({ title: 'Users suspended', description: `${selectedIds.length} accounts suspended` });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to suspend some users', variant: 'destructive' });
    },
  });

  const bulkUnsuspendMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map((id) => adminClient.unsuspendUser(id)));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      setSelectedIds([]);
      toast({ title: 'Users unsuspended', description: `${selectedIds.length} accounts reactivated` });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to unsuspend some users', variant: 'destructive' });
    },
  });

  const bulkChangeTierMutation = useMutation({
    mutationFn: async ({ ids, tier }: { ids: string[]; tier: string }) => {
      await Promise.all(ids.map((id) => adminClient.updateUserTier(id, tier)));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      setSelectedIds([]);
      toast({ title: 'Tier updated', description: `Tier changed for ${selectedIds.length} users` });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to change tier for some users', variant: 'destructive' });
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
          <div className="flex gap-1 bg-white/80 dark:bg-zinc-800/80 border border-amber-200/60 dark:border-zinc-700/60 rounded-lg p-1">
            <Button
              size="sm"
              variant={viewMode === 'grid' ? 'default' : 'ghost'}
              onClick={() => setViewMode('grid')}
              className="h-8 w-8 p-0"
              title="Grid view"
            >
              <Grid3x3 className="w-4 h-4" />
            </Button>
            <Button
              size="sm"
              variant={viewMode === 'list' ? 'default' : 'ghost'}
              onClick={() => setViewMode('list')}
              className="h-8 w-8 p-0"
              title="List view"
            >
              <List className="w-4 h-4" />
            </Button>
            <Button
              size="sm"
              variant={viewMode === 'table' ? 'default' : 'ghost'}
              onClick={() => setViewMode('table')}
              className="h-8 w-8 p-0"
              title="Table view"
            >
              <Table2 className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Bulk Actions Bar (table mode) */}
      {viewMode === 'table' && selectedIds.length > 0 && (
        <div className="flex items-center gap-3 px-4 py-2 bg-primary/5 border border-primary/20 rounded-lg">
          <span className="text-sm font-medium text-primary">
            {selectedIds.length} selected
          </span>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5 text-xs"
            onClick={() => bulkSuspendMutation.mutate(selectedIds)}
            disabled={bulkSuspendMutation.isPending}
          >
            <UserX className="w-3.5 h-3.5" />
            Suspend
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5 text-xs"
            onClick={() => bulkUnsuspendMutation.mutate(selectedIds)}
            disabled={bulkUnsuspendMutation.isPending}
          >
            <Eye className="w-3.5 h-3.5" />
            Unsuspend
          </Button>
          <Select
            onValueChange={(tier) => bulkChangeTierMutation.mutate({ ids: selectedIds, tier })}
          >
            <SelectTrigger className="w-[130px] h-8 text-xs">
              <ArrowUpDown className="w-3 h-3 mr-1" />
              <SelectValue placeholder="Change Tier" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="free">Free</SelectItem>
              <SelectItem value="normal">Normal</SelectItem>
              <SelectItem value="premium">Premium</SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Users Display */}
      {isLoading ? (
        <div className={viewMode === 'table' ? 'space-y-0' : viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {[...Array(6)].map((_, i) => (
            <div
              key={i}
              className={viewMode === 'table' ? 'h-12 bg-white/40 dark:bg-zinc-800/40 animate-pulse border-b' : 'h-[240px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 animate-pulse'}
            />
          ))}
        </div>
      ) : !data || !data.items || data.items.length === 0 ? (
        <div className="text-center py-12 bg-white/50 dark:bg-zinc-800/50 backdrop-blur-sm rounded-xl border border-amber-200/60 dark:border-zinc-700/60">
          <p className="font-nunito text-slate-500 dark:text-zinc-400">No users found</p>
        </div>
      ) : viewMode === 'table' ? (
        /* Table View */
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                  <th className="text-left p-3 w-10">
                    <input
                      type="checkbox"
                      checked={data.items.length > 0 && selectedIds.length === data.items.length}
                      onChange={toggleSelectAll}
                      className="rounded border-slate-300 dark:border-zinc-600"
                    />
                  </th>
                  <th className="text-left p-3 font-medium text-muted-foreground">Name</th>
                  <th className="text-left p-3 font-medium text-muted-foreground">Email</th>
                  <th className="text-left p-3 font-medium text-muted-foreground">Role</th>
                  <th className="text-left p-3 font-medium text-muted-foreground">Tier</th>
                  <th className="text-left p-3 font-medium text-muted-foreground">Status</th>
                  <th className="text-right p-3 font-medium text-muted-foreground">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((user) => (
                  <tr
                    key={user.id}
                    className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30 cursor-pointer"
                    onClick={() => setSelectedUserId(user.id)}
                  >
                    <td className="p-3" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={selectedIds.includes(user.id)}
                        onChange={() => toggleSelect(user.id)}
                        className="rounded border-slate-300 dark:border-zinc-600"
                      />
                    </td>
                    <td className="p-3">
                      <span className="font-medium">{user.displayName}</span>
                    </td>
                    <td className="p-3 text-muted-foreground">{user.email}</td>
                    <td className="p-3">
                      <Badge variant="outline" className="font-nunito text-xs">
                        <Shield className="w-3 h-3 mr-1" />
                        {user.role}
                      </Badge>
                    </td>
                    <td className="p-3">
                      <Badge
                        variant="outline"
                        className={`font-nunito text-xs ${
                          user.tier === 'premium'
                            ? 'border-amber-500 text-amber-700 bg-amber-50 dark:bg-amber-900/20 dark:text-amber-300'
                            : user.tier === 'normal'
                            ? 'border-blue-500 text-blue-700 bg-blue-50 dark:bg-blue-900/20 dark:text-blue-300'
                            : 'border-slate-400 text-slate-600 dark:text-zinc-400'
                        }`}
                      >
                        {user.tier}
                      </Badge>
                    </td>
                    <td className="p-3">
                      <Badge
                        variant={user.isActive ? 'default' : 'secondary'}
                        className={user.isActive
                          ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300'
                          : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300'
                        }
                      >
                        {user.isActive ? 'Active' : 'Suspended'}
                      </Badge>
                    </td>
                    <td className="p-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 px-2 text-xs gap-1"
                          onClick={() =>
                            user.isActive
                              ? suspendMutation.mutate(user.id)
                              : unsuspendMutation.mutate(user.id)
                          }
                        >
                          <UserX className="w-3 h-3" />
                          {user.isActive ? 'Suspend' : 'Unsuspend'}
                        </Button>
                        <Select
                          value={user.tier}
                          onValueChange={(tier) =>
                            adminClient.updateUserTier(user.id, tier).then(() => {
                              queryClient.invalidateQueries({ queryKey: ['admin-users'] });
                              toast({ title: 'Tier updated' });
                            })
                          }
                        >
                          <SelectTrigger className="h-7 w-[90px] text-xs">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="free">Free</SelectItem>
                            <SelectItem value="normal">Normal</SelectItem>
                            <SelectItem value="premium">Premium</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        /* Grid / List View (existing) */
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
