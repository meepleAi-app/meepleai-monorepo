/**
 * User Detail Client - Gap Analysis Implementation
 *
 * Admin interface for viewing user details and activity timeline:
 * - User profile information
 * - Activity timeline with filters
 * - Sessions management
 * - Role management
 *
 * Backend Integration:
 * - GET /api/v1/admin/users/{userId}/activity
 * - GET /api/v1/admin/users/{userId}/sessions
 * - PUT /api/v1/admin/users/{userId}/tier
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

import {
  ArrowLeft,
  User,
  Mail,
  Shield,
  Calendar,
  Clock,
  AlertCircle,
  RefreshCw,
} from 'lucide-react';
import Link from 'next/link';

import {
  AdminAuthGuard,
  UserActivityTimeline,
  type UserActivityEvent,
} from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { api, type AdminUser, type GetUserActivityResult, type UserActivityFilters } from '@/lib/api';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

// Extended profile with optional fields not in AdminUser
type UserProfile = AdminUser & {
  subscriptionTier?: string;
  lastSeenAt?: string | null;
};

// ========== Props ==========

interface UserDetailClientProps {
  userId: string;
}

// ========== Main Component ==========

export function UserDetailClient({ userId }: UserDetailClientProps) {
  const { user: currentUser, loading: authLoading } = useAuthUser();

  // Data state
  const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
  const [activities, setActivities] = useState<UserActivityEvent[]>([]);
  const [totalActivities, setTotalActivities] = useState(0);
  const [loading, setLoading] = useState(true);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [activityLimit, setActivityLimit] = useState(100);
  const [actionFilter, setActionFilter] = useState<string>('all');

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Fetch user profile
  const fetchUserProfile = useCallback(async () => {
    try {
      // Use admin users list and find the specific user
      // Note: In a real implementation, you'd have a dedicated GET /admin/users/{id} endpoint
      const result = await api.admin.getUsers({ page: 1, pageSize: 100 });
      const foundUser = result.items.find(u => u.id === userId);

      if (foundUser) {
        // AdminUser extends naturally to UserProfile (optional fields)
        setUserProfile(foundUser as UserProfile);
      } else {
        setError('Utente non trovato');
      }
    } catch (err) {
      console.error('Failed to fetch user profile:', err);
      setError('Errore nel caricamento del profilo utente');
    }
  }, [userId]);

  // Fetch user activities
  const fetchActivities = useCallback(async () => {
    try {
      setActivitiesLoading(true);

      const filters: UserActivityFilters = {
        limit: activityLimit,
      };

      if (actionFilter !== 'all') {
        filters.actionFilter = actionFilter;
      }

      const result: GetUserActivityResult = await api.admin.getUserActivity(userId, filters);

      // Transform API response to UserActivityEvent format
      // Note: UserActivityDto doesn't include userId/userEmail, we use the userId from props
      const transformedActivities: UserActivityEvent[] = result.activities.map(activity => ({
        id: activity.id,
        eventType: activity.action,
        description: activity.details || `${activity.action} su ${activity.resource}`,
        userId: userId,  // From props, since activities are filtered by user
        userEmail: userProfile?.email ?? undefined,  // From loaded profile
        entityId: activity.resourceId ?? undefined,
        entityType: activity.resource,
        timestamp: activity.createdAt,
        severity: activity.result === 'Success' ? 'Info' : activity.result === 'Failed' ? 'Error' : 'Warning',
        metadata: {
          resource: activity.resource,
          result: activity.result,
          ipAddress: activity.ipAddress,
        },
      }));

      setActivities(transformedActivities);
      setTotalActivities(result.totalCount);
    } catch (err) {
      console.error('Failed to fetch activities:', err);
      addToast('error', "Errore nel caricamento dell'attività");
    } finally {
      setActivitiesLoading(false);
    }
  }, [userId, activityLimit, actionFilter, addToast, userProfile]);

  // Initial load
  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      await fetchUserProfile();
      await fetchActivities();
      setLoading(false);
    };
    loadData();
  }, [fetchUserProfile, fetchActivities]);

  // Refetch activities when filters change
  useEffect(() => {
    if (!loading) {
      fetchActivities();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actionFilter, activityLimit]);

  // Handle refresh
  const handleRefresh = async () => {
    await fetchActivities();
    addToast('success', 'Attività aggiornate');
  };

  // Format date helper
  const formatDate = (dateStr: string | null) => {
    if (!dateStr) return 'Mai';
    return new Date(dateStr).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // Get role badge variant
  const getRoleBadgeVariant = (role: string) => {
    switch (role.toLowerCase()) {
      case 'admin':
        return 'destructive';
      case 'editor':
        return 'default';
      default:
        return 'secondary';
    }
  };

  return (
    <AdminAuthGuard loading={authLoading} user={currentUser}>
      <div className="container mx-auto p-6 max-w-7xl space-y-6">
        {/* Header */}
        <div className="flex items-center gap-4">
          <Link href="/admin/users">
            <Button variant="ghost" size="sm">
              <ArrowLeft className="h-4 w-4 mr-1" />
              Indietro
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Dettaglio Utente</h1>
        </div>

        {/* Loading State */}
        {loading && (
          <div className="flex justify-center py-12">
            <Spinner size="lg" />
          </div>
        )}

        {/* Error State */}
        {error && !loading && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!loading && !error && userProfile && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left Column: User Profile */}
            <div className="lg:col-span-1 space-y-6">
              {/* Profile Card */}
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <User className="h-5 w-5" />
                    Profilo
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  {/* Avatar Placeholder */}
                  <div className="flex justify-center">
                    <div className="w-20 h-20 rounded-full bg-primary/10 flex items-center justify-center">
                      <span className="text-2xl font-bold text-primary">
                        {userProfile.displayName?.charAt(0).toUpperCase() ||
                          userProfile.email?.charAt(0).toUpperCase()}
                      </span>
                    </div>
                  </div>

                  {/* Display Name */}
                  <div className="text-center">
                    <h2 className="text-xl font-semibold">{userProfile.displayName}</h2>
                  </div>

                  {/* Email */}
                  <div className="flex items-center gap-2 text-sm">
                    <Mail className="h-4 w-4 text-muted-foreground" />
                    <span className="text-muted-foreground">{userProfile.email}</span>
                  </div>

                  {/* Role */}
                  <div className="flex items-center gap-2">
                    <Shield className="h-4 w-4 text-muted-foreground" />
                    <Badge variant={getRoleBadgeVariant(userProfile.role)}>
                      {userProfile.role}
                    </Badge>
                  </div>

                  {/* Created At */}
                  <div className="flex items-center gap-2 text-sm">
                    <Calendar className="h-4 w-4 text-muted-foreground" />
                    <span className="text-muted-foreground">
                      Registrato: {formatDate(userProfile.createdAt)}
                    </span>
                  </div>

                  {/* Last Seen */}
                  <div className="flex items-center gap-2 text-sm">
                    <Clock className="h-4 w-4 text-muted-foreground" />
                    <span className="text-muted-foreground">
                      Ultimo accesso: {formatDate(userProfile.lastSeenAt ?? null)}
                    </span>
                  </div>

                  {/* User ID */}
                  <div className="pt-4 border-t">
                    <Label className="text-xs text-muted-foreground">ID Utente</Label>
                    <code className="block text-xs bg-muted p-2 rounded mt-1 break-all">
                      {userProfile.id}
                    </code>
                  </div>
                </CardContent>
              </Card>

              {/* Quick Stats Card */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-sm">Statistiche</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    <div className="flex justify-between items-center">
                      <span className="text-sm text-muted-foreground">Attività totali</span>
                      <Badge variant="outline">{totalActivities}</Badge>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>

            {/* Right Column: Activity Timeline */}
            <div className="lg:col-span-2">
              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div>
                      <CardTitle>Timeline Attività</CardTitle>
                      <CardDescription>
                        Cronologia delle azioni eseguite dall&apos;utente
                      </CardDescription>
                    </div>
                    <div className="flex items-center gap-2">
                      {/* Action Filter */}
                      <Select
                        value={actionFilter}
                        onValueChange={setActionFilter}
                      >
                        <SelectTrigger className="w-[150px]">
                          <SelectValue placeholder="Tutti" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="all">Tutte le azioni</SelectItem>
                          <SelectItem value="Login">Login</SelectItem>
                          <SelectItem value="Logout">Logout</SelectItem>
                          <SelectItem value="Create">Creazione</SelectItem>
                          <SelectItem value="Update">Modifica</SelectItem>
                          <SelectItem value="Delete">Eliminazione</SelectItem>
                        </SelectContent>
                      </Select>

                      {/* Limit Filter */}
                      <Select
                        value={activityLimit.toString()}
                        onValueChange={v => setActivityLimit(Number(v))}
                      >
                        <SelectTrigger className="w-[100px]">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="50">50</SelectItem>
                          <SelectItem value="100">100</SelectItem>
                          <SelectItem value="200">200</SelectItem>
                          <SelectItem value="500">500</SelectItem>
                        </SelectContent>
                      </Select>

                      {/* Refresh Button */}
                      <Button
                        variant="outline"
                        size="icon"
                        onClick={handleRefresh}
                        disabled={activitiesLoading}
                      >
                        <RefreshCw className={`h-4 w-4 ${activitiesLoading ? 'animate-spin' : ''}`} />
                      </Button>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  {activitiesLoading && activities.length === 0 ? (
                    <div className="flex justify-center py-12">
                      <Spinner size="lg" />
                    </div>
                  ) : activities.length === 0 ? (
                    <Alert>
                      <AlertCircle className="h-4 w-4" />
                      <AlertDescription>
                        Nessuna attività registrata per questo utente.
                      </AlertDescription>
                    </Alert>
                  ) : (
                    <UserActivityTimeline
                      events={activities}
                      pageSize={20}
                      showFilters={true}
                    />
                  )}
                </CardContent>
              </Card>
            </div>
          </div>
        )}

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 z-50 space-y-2">
          {toasts.map(toast => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96"
            >
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
