'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Search,
  Filter,
  ChevronLeft,
  ChevronRight,
  UserX,
  UserCheck,
  Shield,
  Mail,
  Download,
  Eye,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/data-display/badge';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Input } from '@/components/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { adminClient } from '@/lib/api/admin-client';

interface User {
  id: string;
  displayName: string;
  email: string;
  role: 'user' | 'admin';
  tier: 'free' | 'normal' | 'premium';
  level: number;
  createdAt: string;
  isActive: boolean;
}

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
      <SheetContent className="w-full sm:max-w-lg bg-gradient-to-br from-white/95 to-amber-50/95 backdrop-blur-xl border-l-2 border-amber-300/60">
        <SheetHeader>
          <SheetTitle className="font-quicksand font-bold text-2xl text-slate-900">
            User Profile
          </SheetTitle>
        </SheetHeader>

        {user && (
          <div className="mt-6 space-y-6">
            {/* User Info Card */}
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

              <div className="grid grid-cols-2 gap-4 mt-4">
                <div>
                  <div className="font-nunito text-xs text-slate-500 uppercase tracking-wider mb-1">
                    Role
                  </div>
                  <Badge variant="outline" className="font-nunito">
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
                        ? 'border-amber-500 text-amber-700'
                        : user.tier === 'normal'
                        ? 'border-blue-500 text-blue-700'
                        : 'border-slate-400 text-slate-600'
                    }`}
                  >
                    {user.tier}
                  </Badge>
                </div>
                <div>
                  <div className="font-nunito text-xs text-slate-500 uppercase tracking-wider mb-1">
                    Level
                  </div>
                  <div className="font-quicksand font-bold text-lg text-slate-900">
                    {user.level}
                  </div>
                </div>
                <div>
                  <div className="font-nunito text-xs text-slate-500 uppercase tracking-wider mb-1">
                    Member Since
                  </div>
                  <div className="font-nunito text-sm text-slate-700">
                    {new Date(user.createdAt).toLocaleDateString()}
                  </div>
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
                <h4 className="font-quicksand font-semibold text-lg text-slate-900 mb-4">
                  Achievement Badges
                </h4>
                <div className="flex flex-wrap gap-2">
                  {badges.map((badge) => (
                    <div
                      key={badge.id}
                      className="inline-flex items-center gap-2 px-3 py-2 bg-gradient-to-r from-amber-100/80 to-orange-100/60 rounded-lg border border-amber-200/60"
                    >
                      {badge.iconUrl && <span className="text-2xl">{badge.iconUrl}</span>}
                      <div>
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
            <div className="grid grid-cols-2 gap-3 pt-4 border-t border-amber-200/60">
              <Button variant="outline" className="w-full">
                <Mail className="w-4 h-4 mr-2" />
                Send Email
              </Button>
              <Button variant="outline" className="w-full">
                <Shield className="w-4 h-4 mr-2" />
                Change Tier
              </Button>
              <Button variant="outline" className="w-full text-orange-600 hover:text-orange-700">
                <UserX className="w-4 h-4 mr-2" />
                Suspend
              </Button>
              <Button variant="outline" className="w-full text-blue-600 hover:text-blue-700">
                <Eye className="w-4 h-4 mr-2" />
                Impersonate
              </Button>
            </div>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}

export function UserManagementSection() {
  const [page, setPage] = useState(1);
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [tierFilter, setTierFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin-users', page, roleFilter, tierFilter, searchQuery],
    queryFn: () =>
      adminClient.getUsers({
        page,
        pageSize: 10,
        role: roleFilter !== 'all' ? roleFilter : undefined,
        tier: tierFilter !== 'all' ? tierFilter : undefined,
        search: searchQuery || undefined,
      }),
  });

  const suspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.suspendUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast.warning('User account has been suspended');
    },
  });

  const unsuspendMutation = useMutation({
    mutationFn: (userId: string) => adminClient.unsuspendUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      toast.success('User account has been reactivated');
    },
  });

  return (
    <div>
      {/* Section Header */}
      <div className="mb-6 flex items-center gap-2">
        <div className="h-1 w-12 bg-gradient-to-r from-amber-500 to-orange-500 rounded-full" />
        <h2 className="font-quicksand font-bold text-xl text-slate-700">
          User Management
        </h2>
        <Badge variant="secondary" className="ml-2 font-nunito">
          {data?.totalCount ?? 0} users
        </Badge>
      </div>

      {/* Filters and Actions Bar */}
      <div className="mb-6 bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-4">
        <div className="flex flex-col lg:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Search by name or email..."
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
              className="pl-10 bg-white/80 border-amber-200/60 focus:border-amber-400"
            />
          </div>

          {/* Role Filter */}
          <Select value={roleFilter} onValueChange={setRoleFilter}>
            <SelectTrigger className="w-[140px] bg-white/80 border-amber-200/60">
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
            <SelectTrigger className="w-[140px] bg-white/80 border-amber-200/60">
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

          {/* Export */}
          <Button variant="outline" className="bg-white/80 border-amber-200/60">
            <Download className="w-4 h-4 mr-2" />
            Export CSV
          </Button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gradient-to-r from-amber-100/80 to-orange-100/60 border-b border-amber-200/60">
              <tr>
                <th className="px-4 py-3 text-left font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  User
                </th>
                <th className="px-4 py-3 text-left font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Email
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Role
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Tier
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Level
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Status
                </th>
                <th className="px-4 py-3 text-right font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-amber-100/60">
              {isLoading ? (
                [...Array(5)].map((_, i) => (
                  <tr key={i} className="animate-pulse">
                    <td className="px-4 py-4" colSpan={7}>
                      <div className="h-12 bg-slate-200/40 rounded" />
                    </td>
                  </tr>
                ))
              ) : data?.items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center">
                    <p className="font-nunito text-slate-500">No users found</p>
                  </td>
                </tr>
              ) : (
                data?.items.map((user: User) => (
                  <tr
                    key={user.id}
                    className="hover:bg-amber-50/40 transition-colors group cursor-pointer"
                    onClick={() => setSelectedUserId(user.id)}
                  >
                    <td className="px-4 py-4">
                      <div className="font-nunito font-semibold text-slate-900">
                        {user.displayName}
                      </div>
                      <div className="font-nunito text-xs text-slate-400">
                        ID: {user.id.slice(0, 8)}...
                      </div>
                    </td>
                    <td className="px-4 py-4">
                      <div className="font-nunito text-sm text-slate-700">
                        {user.email}
                      </div>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <Badge
                        variant={user.role === 'admin' ? 'default' : 'secondary'}
                        className="font-nunito"
                      >
                        {user.role}
                      </Badge>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <Badge
                        variant="outline"
                        className={`font-nunito ${
                          user.tier === 'premium'
                            ? 'border-amber-500 text-amber-700 bg-amber-50'
                            : user.tier === 'normal'
                            ? 'border-blue-500 text-blue-700 bg-blue-50'
                            : 'border-slate-400 text-slate-600 bg-slate-50'
                        }`}
                      >
                        {user.tier}
                      </Badge>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <span className="font-nunito font-semibold text-slate-700">
                        {user.level}
                      </span>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <Badge
                        variant={user.isActive ? 'default' : 'secondary'}
                        className="font-nunito"
                      >
                        {user.isActive ? 'Active' : 'Suspended'}
                      </Badge>
                    </td>
                    <td className="px-4 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={(e: React.MouseEvent<HTMLButtonElement>) => {
                            e.stopPropagation();
                            setSelectedUserId(user.id);
                          }}
                        >
                          <Eye className="w-4 h-4" />
                        </Button>
                        {user.isActive ? (
                          <Button
                            size="sm"
                            variant="outline"
                            className="text-orange-600 hover:text-orange-700"
                            onClick={(e) => {
                              e.stopPropagation();
                              suspendMutation.mutate(user.id);
                            }}
                          >
                            <UserX className="w-4 h-4" />
                          </Button>
                        ) : (
                          <Button
                            size="sm"
                            variant="outline"
                            className="text-emerald-600 hover:text-emerald-700"
                            onClick={(e) => {
                              e.stopPropagation();
                              unsuspendMutation.mutate(user.id);
                            }}
                          >
                            <UserCheck className="w-4 h-4" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="px-4 py-3 bg-gradient-to-r from-amber-50/80 to-orange-50/60 border-t border-amber-200/60 flex items-center justify-between">
            <div className="font-nunito text-sm text-slate-600">
              Page {page} of {data.totalPages} ({data.totalCount} total)
            </div>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                <ChevronLeft className="w-4 h-4" />
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                disabled={page === data.totalPages}
              >
                <ChevronRight className="w-4 h-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

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
