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
  Key,
  Send,
  UserCog,
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  api,
  type AdminUser,
  type GetUserActivityResult,
  type UserActivityFilters,
} from '@/lib/api';
import type { UserBadge, RoleChangeHistory, UserLibraryStats } from '@/lib/api/clients/adminClient';

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
  const [badges, setBadges] = useState<UserBadge[]>([]);
  const [libraryStats, setLibraryStats] = useState<UserLibraryStats | null>(null);
  const [roleHistory, setRoleHistory] = useState<RoleChangeHistory[]>([]);
  const [loading, setLoading] = useState(true);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [activityLimit, setActivityLimit] = useState(100);
  const [actionFilter, setActionFilter] = useState<string>('all');

  // Quick actions state (Issue #2890)
  const [showResetPasswordDialog, setShowResetPasswordDialog] = useState(false);
  const [showSendEmailDialog, setShowSendEmailDialog] = useState(false);
  const [newPassword, setNewPassword] = useState('');
  const [emailSubject, setEmailSubject] = useState('');
  const [emailBody, setEmailBody] = useState('');
  const [actionLoading, setActionLoading] = useState(false);

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
      // Issue #2890: Use dedicated GET /admin/users/{id} endpoint
      const user = await api.admin.getUserDetail(userId);
      setUserProfile(user as UserProfile);
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

  // Fetch user badges (Issue #2890)
  const fetchBadges = useCallback(async () => {
    try {
      const userBadges = await api.admin.getUserBadges(userId);
      setBadges(userBadges);
    } catch (err) {
      console.error('Failed to fetch badges:', err);
      // Non-critical: don't show error, just empty badges
    }
  }, [userId]);

  // Fetch library stats (Issue #2890)
  const fetchLibraryStats = useCallback(async () => {
    try {
      const stats = await api.admin.getUserLibraryStats(userId);
      setLibraryStats(stats);
    } catch (err) {
      console.error('Failed to fetch library stats:', err);
      // Non-critical: don't show error
    }
  }, [userId]);

  // Fetch role history (Issue #2890)
  const fetchRoleHistory = useCallback(async () => {
    try {
      const history = await api.admin.getUserRoleHistory(userId);
      setRoleHistory(history);
    } catch (err) {
      console.error('Failed to fetch role history:', err);
      // Non-critical: don't show error
    }
  }, [userId]);

  // Initial load
  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      await Promise.all([
        fetchUserProfile(),
        fetchActivities(),
        fetchBadges(),
        fetchLibraryStats(),
        fetchRoleHistory(),
      ]);
      setLoading(false);
    };
    loadData();
  }, [fetchUserProfile, fetchActivities, fetchBadges, fetchLibraryStats, fetchRoleHistory]);

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

  // Quick actions handlers (Issue #2890)
  const handleResetPassword = async () => {
    if (!newPassword || newPassword.length < 8) {
      addToast('error', 'La password deve essere almeno 8 caratteri');
      return;
    }

    try {
      setActionLoading(true);
      await api.admin.resetUserPassword(userId, newPassword);
      addToast('success', 'Password reimpostata con successo');
      setShowResetPasswordDialog(false);
      setNewPassword('');
    } catch (err) {
      console.error('Failed to reset password:', err);
      addToast('error', 'Errore nel reset della password');
    } finally {
      setActionLoading(false);
    }
  };

  const handleSendEmail = async () => {
    if (!emailSubject || !emailBody) {
      addToast('error', 'Compila tutti i campi');
      return;
    }

    try {
      setActionLoading(true);
      await api.admin.sendUserEmail(userId, emailSubject, emailBody);
      addToast('success', 'Email inviata con successo');
      setShowSendEmailDialog(false);
      setEmailSubject('');
      setEmailBody('');
    } catch (err) {
      console.error('Failed to send email:', err);
      addToast('error', "Errore nell'invio dell'email");
    } finally {
      setActionLoading(false);
    }
  };

  const handleImpersonate = async () => {
    if (!confirm(`⚠️ ATTENZIONE: Stai per impersonare l'utente ${userProfile?.displayName}. Continuare?`)) {
      return;
    }

    try {
      setActionLoading(true);
      const response = await api.admin.impersonateUser(userId);
      // Store new session token and reload
      document.cookie = `session=${response.sessionToken}; path=/; Secure; SameSite=Strict`;
      addToast('success', `Impersonazione attiva fino a ${formatDate(response.expiresAt)}`);
      setTimeout(() => {
        window.location.href = '/';
      }, 2000);
    } catch (err) {
      console.error('Failed to impersonate user:', err);
      // Handle specific error cases (Issue #2890 code review)
      if (err instanceof Error && err.message.includes('suspended')) {
        addToast('error', 'Impossibile impersonare: utente sospeso');
      } else {
        addToast('error', "Errore nell'impersonazione");
      }
      setActionLoading(false);
    }
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

                  {/* Quick Actions (Issue #2890) */}
                  <div className="pt-4 border-t space-y-2">
                    <Label className="text-xs text-muted-foreground">Azioni Rapide</Label>
                    <div className="grid grid-cols-1 gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setShowResetPasswordDialog(true)}
                        disabled={actionLoading}
                      >
                        <Key className="h-4 w-4 mr-2" />
                        Reset Password
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setShowSendEmailDialog(true)}
                        disabled={actionLoading}
                      >
                        <Send className="h-4 w-4 mr-2" />
                        Invia Email
                      </Button>
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={handleImpersonate}
                        disabled={actionLoading}
                      >
                        <UserCog className="h-4 w-4 mr-2" />
                        ⚠️ Impersona Utente
                      </Button>
                    </div>
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
                    {libraryStats && (
                      <>
                        <div className="flex justify-between items-center">
                          <span className="text-sm text-muted-foreground">Giochi in libreria</span>
                          <Badge variant="outline">{libraryStats.totalGames}</Badge>
                        </div>
                        <div className="flex justify-between items-center">
                          <span className="text-sm text-muted-foreground">Sessioni giocate</span>
                          <Badge variant="outline">{libraryStats.sessionsPlayed}</Badge>
                        </div>
                      </>
                    )}
                  </div>
                </CardContent>
              </Card>

              {/* Token Usage Card (Issue #3704) */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-sm">Token Usage (This Month)</CardTitle>
                  <CardDescription>Subscription: {userProfile?.tier || 'Free'}</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    <div className="flex justify-between items-center">
                      <span className="text-sm text-muted-foreground">Used</span>
                      <span className="font-mono font-medium">
                        {(userProfile as any)?.tokenUsage?.toLocaleString() || '0'}
                      </span>
                    </div>
                    <div className="flex justify-between items-center">
                      <span className="text-sm text-muted-foreground">Limit</span>
                      <span className="font-mono font-medium">
                        {(userProfile as any)?.tokenLimit?.toLocaleString() || '10,000'}
                      </span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className={`h-2 rounded-full ${
                          ((userProfile as any)?.tokenUsage || 0) >= ((userProfile as any)?.tokenLimit || 10000)
                            ? 'bg-red-600'
                            : ((userProfile as any)?.tokenUsage || 0) >= ((userProfile as any)?.tokenLimit || 10000) * 0.8
                            ? 'bg-orange-500'
                            : 'bg-green-500'
                        }`}
                        style={{
                          width: `${Math.min(
                            100,
                            (((userProfile as any)?.tokenUsage || 0) /
                              ((userProfile as any)?.tokenLimit || 10000)) *
                              100
                          )}%`,
                        }}
                      />
                    </div>
                    <div className="text-xs text-center text-muted-foreground">
                      {Math.round(
                        (((userProfile as any)?.tokenUsage || 0) /
                          ((userProfile as any)?.tokenLimit || 10000)) *
                          100
                      )}
                      % used
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Badges Card (Issue #2890) */}
              {badges.length > 0 && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-sm">Badge ({badges.length})</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="grid grid-cols-2 gap-2">
                      {badges.map(badge => (
                        <div
                          key={badge.id}
                          className="flex flex-col items-center p-2 border rounded-lg hover:bg-muted/50 transition-colors"
                          title={badge.description}
                        >
                          <div className="text-2xl mb-1">{badge.iconUrl || '🏆'}</div>
                          <div className="text-xs font-medium text-center">{badge.name}</div>
                          <div className="text-xs text-muted-foreground">{badge.tier}</div>
                          {!badge.isDisplayed && (
                            <Badge variant="secondary" className="text-xs mt-1">
                              Nascosto
                            </Badge>
                          )}
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              )}
            </div>

            {/* Right Column: Tabbed Content - Issue #3946 */}
            <div className="lg:col-span-2">
              <Tabs defaultValue="overview" className="space-y-6">
                <TabsList>
                  <TabsTrigger value="overview">Overview</TabsTrigger>
                  <TabsTrigger value="activity">
                    Activity
                    {totalActivities > 0 && (
                      <Badge variant="secondary" className="ml-2">
                        {totalActivities}
                      </Badge>
                    )}
                  </TabsTrigger>
                  <TabsTrigger value="sessions">Sessions</TabsTrigger>
                  <TabsTrigger value="api-keys">API Keys</TabsTrigger>
                </TabsList>

                {/* Overview Tab */}
                <TabsContent value="overview" className="space-y-6">
                  {/* Role History Card (Issue #2890) */}
                  {roleHistory.length > 0 && (
                <Card>
                  <CardHeader>
                    <CardTitle>Cronologia Ruoli</CardTitle>
                    <CardDescription>Storico cambiamenti ruolo</CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-3">
                      {roleHistory.map((change, idx) => (
                        <div key={idx} className="flex items-center justify-between p-3 border rounded-lg">
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <Badge variant="outline">{change.oldRole}</Badge>
                              <span className="text-muted-foreground">→</span>
                              <Badge variant={getRoleBadgeVariant(change.newRole)}>
                                {change.newRole}
                              </Badge>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              Modificato da: {change.changedByDisplayName}
                            </div>
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {formatDate(change.changedAt)}
                          </div>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              )}
                </TabsContent>

                {/* Activity Tab - Issue #3946 */}
                <TabsContent value="activity" className="space-y-6">
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
                </TabsContent>

                {/* Sessions Tab - Issue #3946 */}
                <TabsContent value="sessions" className="space-y-6">
                  <Card>
                    <CardHeader>
                      <CardTitle>User Sessions</CardTitle>
                      <CardDescription>
                        Game sessions for this user
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      <Alert>
                        <AlertCircle className="h-4 w-4" />
                        <AlertDescription>
                          Session history coming soon. View all sessions in{' '}
                          <Link href="/admin/game-sessions" className="underline">
                            Game Sessions
                          </Link>
                          .
                        </AlertDescription>
                      </Alert>
                    </CardContent>
                  </Card>
                </TabsContent>

                {/* API Keys Tab - Issue #3946 */}
                <TabsContent value="api-keys" className="space-y-6">
                  <Card>
                    <CardHeader>
                      <CardTitle>API Keys</CardTitle>
                      <CardDescription>
                        API keys created by this user
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      <Alert>
                        <AlertCircle className="h-4 w-4" />
                        <AlertDescription>
                          API key management coming soon. View all keys in{' '}
                          <Link href="/admin/api-keys" className="underline">
                            API Keys
                          </Link>
                          .
                        </AlertDescription>
                      </Alert>
                    </CardContent>
                  </Card>
                </TabsContent>
              </Tabs>
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

        {/* Reset Password Dialog (Issue #2890) */}
        <Dialog open={showResetPasswordDialog} onOpenChange={setShowResetPasswordDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Reset Password</DialogTitle>
              <DialogDescription>
                Imposta una nuova password per {userProfile?.displayName}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="new-password">Nuova Password</Label>
                <Input
                  id="new-password"
                  type="password"
                  placeholder="Minimo 8 caratteri"
                  value={newPassword}
                  onChange={e => setNewPassword(e.target.value)}
                  disabled={actionLoading}
                />
              </div>
            </div>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setShowResetPasswordDialog(false)}
                disabled={actionLoading}
              >
                Annulla
              </Button>
              <Button onClick={handleResetPassword} disabled={actionLoading}>
                {actionLoading ? 'Invio...' : 'Reset Password'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Send Email Dialog (Issue #2890) */}
        <Dialog open={showSendEmailDialog} onOpenChange={setShowSendEmailDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Invia Email</DialogTitle>
              <DialogDescription>
                Invia un&apos;email personalizzata a {userProfile?.displayName}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="email-subject">Oggetto</Label>
                <Input
                  id="email-subject"
                  placeholder="Oggetto dell'email"
                  value={emailSubject}
                  onChange={e => setEmailSubject(e.target.value)}
                  disabled={actionLoading}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="email-body">Messaggio</Label>
                <Textarea
                  id="email-body"
                  placeholder="Corpo dell'email"
                  value={emailBody}
                  onChange={e => setEmailBody(e.target.value)}
                  rows={6}
                  disabled={actionLoading}
                />
              </div>
            </div>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setShowSendEmailDialog(false)}
                disabled={actionLoading}
              >
                Annulla
              </Button>
              <Button onClick={handleSendEmail} disabled={actionLoading}>
                {actionLoading ? 'Invio...' : 'Invia Email'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </AdminAuthGuard>
  );
}
