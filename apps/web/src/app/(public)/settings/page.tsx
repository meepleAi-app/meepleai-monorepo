/**
 * Settings Page - 4 Tabs Implementation (Issue #2881)
 *
 * Comprehensive user settings page with:
 * - Profile tab: Display name, email, avatar, level/badges, stats
 * - Preferences tab: Language, theme, notifications, animations
 * - Privacy tab: Public profile toggles, data retention
 * - Account tab: Password change, 2FA, sessions, delete account
 *
 * Uses Authentication.Application layer via CQRS:
 * - GetUserProfileQuery for data
 * - UpdateUserProfileCommand for changes
 * - Enable2FACommand/Disable2FACommand for 2FA
 */

'use client';

import { useEffect, useState } from 'react';

import { User, Settings, Shield, Key } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { QRCodeSVG } from 'qrcode.react';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { AvatarUpload } from '@/components/profile/AvatarUpload';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Switch } from '@/components/ui/forms/switch';
import { Separator } from '@/components/ui/navigation/separator';
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
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  api,
  type UserProfile,
  type UserPreferences,
  type TotpSetupResponse,
  type TwoFactorStatusDto,
  type UserSessionInfo,
  type UserBadgeDto,
  type UserLibraryStats,
} from '@/lib/api';
import { hasStoredApiKey } from '@/lib/api/core/apiKeyStore';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { getErrorMessage } from '@/lib/utils/errorHandler';

// Tab configuration with icons
const TABS = [
  { id: 'profile', label: 'Profilo', icon: User },
  { id: 'preferences', label: 'Preferenze', icon: Settings },
  { id: 'privacy', label: 'Privacy', icon: Shield },
  { id: 'account', label: 'Account', icon: Key },
] as const;

// OAuth provider configuration
const OAUTH_PROVIDERS = [
  { id: 'google', name: 'Google', color: 'bg-blue-600 hover:bg-blue-700' },
  { id: 'discord', name: 'Discord', color: 'bg-indigo-600 hover:bg-indigo-700' },
  { id: 'github', name: 'GitHub', color: 'bg-card hover:bg-muted' },
];

interface OAuthAccount {
  provider: string;
  createdAt: string;
}

// Privacy preferences (local state until API is implemented)
interface PrivacyPreferences {
  publicProfile: boolean;
  showLibrary: boolean;
  showActivity: boolean;
  showBadges: boolean;
}

export default function SettingsPage() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<string>('profile');

  // Global state
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Profile state
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);

  // Badges and stats
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [libraryStats, setLibraryStats] = useState<UserLibraryStats | null>(null);

  // Password change state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Preferences state
  const [preferences, setPreferences] = useState<UserPreferences>({
    language: 'en',
    emailNotifications: true,
    theme: 'system',
    dataRetentionDays: 90,
  });
  const [animationsEnabled, setAnimationsEnabled] = useState(true);

  // Privacy preferences (local until API)
  const [privacyPrefs, setPrivacyPrefs] = useState<PrivacyPreferences>({
    publicProfile: false,
    showLibrary: false,
    showActivity: false,
    showBadges: true,
  });

  // 2FA state
  const [twoFactorStatus, setTwoFactorStatus] = useState<TwoFactorStatusDto | null>(null);
  const [setup, setSetup] = useState<TotpSetupResponse | null>(null);
  const [verificationCode, setVerificationCode] = useState('');
  const [showBackupCodes, setShowBackupCodes] = useState(false);
  const [disablePassword, setDisablePassword] = useState('');
  const [disableCode, setDisableCode] = useState('');

  // OAuth state
  const [linkedAccounts, setLinkedAccounts] = useState<OAuthAccount[]>([]);
  const [unlinking, setUnlinking] = useState<string | null>(null);

  // API Key authentication state
  const [apiKeyInput, setApiKeyInput] = useState('');
  const [apiKeyLoading, setApiKeyLoading] = useState(false);
  const [apiKeyAuthenticated, setApiKeyAuthenticated] = useState<boolean>(() => hasStoredApiKey());

  // Active sessions state
  const [sessions, setSessions] = useState<UserSessionInfo[]>([]);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [revokingSessionId, setRevokingSessionId] = useState<string | null>(null);

  // Logout all devices state (Issue #2056)
  const [showRevokeAllDialog, setShowRevokeAllDialog] = useState(false);
  const [revokeAllIncludeCurrent, setRevokeAllIncludeCurrent] = useState(false);
  const [revokeAllPassword, setRevokeAllPassword] = useState('');
  const [revokeAllLoading, setRevokeAllLoading] = useState(false);

  // Delete account confirmation
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');

  // Load initial data
  useEffect(() => {
    loadProfile();
    load2FAStatus();
    loadOAuthAccounts();
    loadBadges();
    loadLibraryStats();
    setApiKeyAuthenticated(hasStoredApiKey());
    loadUserSessions();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadProfile = async () => {
    try {
      setLoading(true);
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const res = await fetch(`${apiBase}/api/v1/auth/me`, {
        credentials: 'include',
      });

      if (!res.ok) {
        if (res.status === 401) {
          router.push('/login');
          return;
        }
        throw new Error('Failed to load profile');
      }

      const data = await res.json();
      const user: UserProfile = data.user;

      setProfile(user);
      setDisplayName(user.displayName);
      setEmail(user.email);
      // Issue #2755: Fix TypeError when user properties are undefined
      // Hydrate preferences from profile with defensive defaults
      setPreferences({
        language: user.language || 'en',
        theme: (user.theme as 'light' | 'dark' | 'system') || 'system',
        emailNotifications: user.emailNotifications ?? true,
        dataRetentionDays: user.dataRetentionDays ?? 90,
      });
      setError(null);
    } catch (err) {
      logger.error(
        'Failed to load profile',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'loadProfile', { operation: 'fetch_profile' })
      );
      setError('Failed to load profile');
    } finally {
      setLoading(false);
    }
  };

  const load2FAStatus = async () => {
    try {
      const status = await api.auth.getTwoFactorStatus();
      setTwoFactorStatus(status);
    } catch (err) {
      logger.error(
        'Failed to load 2FA status',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'load2FAStatus', { operation: 'fetch_2fa_status' })
      );
    }
  };

  const loadOAuthAccounts = async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const res = await fetch(`${apiBase}/api/v1/users/me/oauth-accounts`, {
        credentials: 'include',
      });

      if (res.ok) {
        const accounts = await res.json();
        setLinkedAccounts(accounts);
      }
    } catch (error) {
      logger.error(
        'Failed to load OAuth accounts',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'loadOAuthAccounts', {
          operation: 'fetch_oauth_accounts',
        })
      );
    }
  };

  const loadBadges = async () => {
    try {
      const userBadges = await api.badges.getMyBadges();
      setBadges(userBadges);
    } catch (err) {
      logger.error(
        'Failed to load badges',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'loadBadges', { operation: 'fetch_badges' })
      );
    }
  };

  const loadLibraryStats = async () => {
    try {
      const stats = await api.library.getStats();
      setLibraryStats(stats);
    } catch (err) {
      logger.error(
        'Failed to load library stats',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'loadLibraryStats', { operation: 'fetch_library_stats' })
      );
    }
  };

  // Profile update handler
  const handleUpdateProfile = async () => {
    setSuccess(null);
    setError(null);

    if (!displayName.trim()) {
      setError('Display name cannot be empty');
      return;
    }

    try {
      setLoading(true);
      await api.auth.updateProfile({
        displayName: displayName.trim(),
        email: email.trim(),
      });
      setSuccess('Profile updated successfully');
      await loadProfile();
    } catch (err) {
      const errorMsg = getErrorMessage(err);
      logger.error(
        'Failed to update profile',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleUpdateProfile', {
          operation: 'update_profile',
          displayName,
          email,
        })
      );
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  // Password change handler
  const handleChangePassword = async () => {
    setSuccess(null);
    setError(null);

    if (!currentPassword || !newPassword || !confirmPassword) {
      setError('All password fields are required');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('New passwords do not match');
      return;
    }

    if (newPassword.length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }

    try {
      setLoading(true);
      await api.auth.changePassword({
        currentPassword,
        newPassword,
      });
      setSuccess('Password changed successfully');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err) {
      const errorMsg = getErrorMessage(err);
      logger.error(
        'Failed to change password',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleChangePassword', {
          operation: 'change_password',
        })
      );
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  // Preferences update handler
  const handleUpdatePreferences = async () => {
    setSuccess(null);
    setError(null);

    try {
      setLoading(true);
      await api.auth.updatePreferences(preferences);
      setSuccess('Preferences saved successfully');
    } catch (err) {
      const errorMsg = getErrorMessage(err);
      logger.error(
        'Failed to update preferences',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleUpdatePreferences', {
          operation: 'update_preferences',
        })
      );
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  // Privacy preferences update (local for now)
  const handleUpdatePrivacy = () => {
    setSuccess('Privacy settings saved locally');
    // TODO: Call API when privacy endpoint is implemented
  };

  // 2FA handlers
  const handleSetup2FA = async () => {
    try {
      setLoading(true);
      const setupResponse = await api.auth.setup2FA();
      setSetup(setupResponse);
      setShowBackupCodes(true);
      setError(null);
    } catch (err) {
      logger.error(
        'Failed to setup 2FA',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleSetup2FA', { operation: 'setup_2fa' })
      );
      setError('Failed to setup 2FA');
    } finally {
      setLoading(false);
    }
  };

  const handleEnable2FA = async () => {
    try {
      setLoading(true);
      await api.auth.enable2FA(verificationCode);
      setSuccess('Two-factor authentication enabled successfully!');
      setSetup(null);
      setShowBackupCodes(false);
      setVerificationCode('');
      await load2FAStatus();
    } catch (err) {
      logger.error(
        'Failed to enable 2FA',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleEnable2FA', { operation: 'enable_2fa' })
      );
      setError('Invalid verification code. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleDisable2FA = async () => {
    if (!confirm('Are you sure you want to disable 2FA? Your account will be less secure.')) {
      return;
    }

    try {
      setLoading(true);
      await api.auth.disable2FA(disablePassword, disableCode);
      setSuccess('Two-factor authentication disabled.');
      setDisablePassword('');
      setDisableCode('');
      await load2FAStatus();
    } catch (err) {
      logger.error(
        'Failed to disable 2FA',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SettingsPage', 'handleDisable2FA', { operation: 'disable_2fa' })
      );
      setError('Failed to disable 2FA. Check your password and code.');
    } finally {
      setLoading(false);
    }
  };

  const downloadBackupCodes = () => {
    if (!setup?.backupCodes) return;

    const text = `MeepleAI Backup Codes\n\n${setup.backupCodes.join('\n')}\n\nKeep these codes in a secure location. Each code can only be used once.`;
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'meepleai-backup-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
  };

  // OAuth handlers
  const handleLinkOAuth = (provider: string) => {
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    window.location.href = `${apiBase}/api/v1/auth/oauth/${provider}/login`;
  };

  const handleUnlinkOAuth = async (provider: string) => {
    if (!confirm(`Unlink your ${provider} account?`)) {
      return;
    }

    setUnlinking(provider);

    try {
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const res = await fetch(`${apiBase}/api/v1/auth/oauth/${provider}/unlink`, {
        method: 'DELETE',
        credentials: 'include',
      });

      if (res.ok) {
        setLinkedAccounts(linkedAccounts.filter(a => a.provider !== provider));
        setSuccess(`${provider} account unlinked successfully`);
      } else {
        setError('Failed to unlink account. Please try again.');
      }
    } catch (error) {
      logger.error(
        'Failed to unlink OAuth account',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleUnlinkOAuth', {
          provider,
          operation: 'unlink_oauth',
        })
      );
      setError('Failed to unlink account.');
    } finally {
      setUnlinking(null);
    }
  };

  const isLinked = (providerId: string) => linkedAccounts.some(a => a.provider === providerId);

  // API Key authentication handlers
  const handleApiKeyLogin = async () => {
    if (!apiKeyInput.trim()) {
      setError('Please enter an API key');
      return;
    }

    setApiKeyLoading(true);
    setError(null);
    setSuccess(null);

    try {
      await api.auth.loginWithApiKey(apiKeyInput);
      setApiKeyAuthenticated(true);
      setApiKeyInput('');
      setSuccess('API key stored for this browser session.');
    } catch (error) {
      logger.error(
        'API key login failed',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleApiKeyLogin', { operation: 'api_key_login' })
      );
      setError(getErrorMessage(error, 'Failed to authenticate with API key'));
      setApiKeyAuthenticated(false);
    } finally {
      setApiKeyLoading(false);
    }
  };

  const handleApiKeyLogout = async () => {
    if (!confirm('Remove the stored API key for this browser session?')) {
      return;
    }

    setApiKeyLoading(true);
    setError(null);

    try {
      await api.auth.logoutApiKey();
      setApiKeyAuthenticated(false);
      setSuccess('API key removed from this session');
    } catch (error) {
      logger.error(
        'API key logout failed',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleApiKeyLogout', { operation: 'api_key_logout' })
      );
      setError(getErrorMessage(error, 'Failed to remove API key authentication'));
    } finally {
      setApiKeyLoading(false);
    }
  };

  // Active sessions handlers
  const loadUserSessions = async () => {
    try {
      setSessionsLoading(true);
      const userSessions = await api.auth.getUserSessions();
      setSessions(userSessions);
    } catch (error) {
      logger.error(
        'Failed to load user sessions',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'loadUserSessions', { operation: 'fetch_sessions' })
      );
    } finally {
      setSessionsLoading(false);
    }
  };

  const handleRevokeSession = async (sessionId: string) => {
    if (!confirm('Revoke this session? The device using this session will be logged out.')) {
      return;
    }

    setRevokingSessionId(sessionId);
    setError(null);
    setSuccess(null);

    try {
      await api.auth.revokeSession(sessionId);
      setSuccess('Session revoked successfully');
      await loadUserSessions();
    } catch (error) {
      logger.error(
        'Failed to revoke session',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleRevokeSession', {
          sessionId,
          operation: 'revoke_session',
        })
      );
      setError(getErrorMessage(error, 'Failed to revoke session'));
    } finally {
      setRevokingSessionId(null);
    }
  };

  // Revoke all sessions handler (Issue #2056)
  const handleRevokeAllSessions = async () => {
    setRevokeAllLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const result = await api.auth.revokeAllSessions({
        includeCurrentSession: revokeAllIncludeCurrent,
        password: revokeAllPassword || null,
      });

      setSuccess(
        `Successfully logged out of ${result.revokedCount} device${result.revokedCount !== 1 ? 's' : ''}.${
          result.currentSessionRevoked ? ' You will need to log in again.' : ''
        }`
      );

      setShowRevokeAllDialog(false);
      setRevokeAllIncludeCurrent(false);
      setRevokeAllPassword('');

      if (result.currentSessionRevoked) {
        setTimeout(() => {
          router.push('/login');
        }, 2000);
      } else {
        await loadUserSessions();
      }
    } catch (error) {
      logger.error(
        'Failed to revoke all sessions',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleRevokeAllSessions', {
          operation: 'revoke_all_sessions',
          includeCurrentSession: revokeAllIncludeCurrent,
        })
      );
      setError(getErrorMessage(error, 'Failed to logout from all devices'));
    } finally {
      setRevokeAllLoading(false);
    }
  };

  const formatDateTime = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const getDeviceInfo = (userAgent: string | null) => {
    if (!userAgent) return 'Unknown device';

    if (userAgent.includes('Mobile')) return 'Mobile device';
    if (userAgent.includes('Tablet')) return 'Tablet';
    if (userAgent.includes('Windows')) return 'Windows PC';
    if (userAgent.includes('Mac')) return 'Mac';
    if (userAgent.includes('Linux')) return 'Linux';

    return 'Unknown device';
  };

  const isCurrentSession = (session: UserSessionInfo) => {
    const now = new Date();
    const lastSeen = session.lastSeenAt
      ? new Date(session.lastSeenAt)
      : new Date(session.createdAt);
    const diffMinutes = (now.getTime() - lastSeen.getTime()) / (1000 * 60);
    return diffMinutes < 5;
  };

  const getBadgeTierColor = (tier: string) => {
    const colors: Record<string, string> = {
      Bronze: 'bg-amber-600',
      Silver: 'bg-gray-400',
      Gold: 'bg-yellow-500',
      Platinum: 'bg-cyan-400',
      Diamond: 'bg-purple-500',
    };
    // eslint-disable-next-line security/detect-object-injection -- tier is from typed API response, not user input
    return colors[tier] || 'bg-gray-400';
  };

  if (loading && !profile) {
    return (
      <div className="min-h-dvh flex items-center justify-center bg-background">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-orange-500" />
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-5xl mx-auto">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-foreground" data-testid="settings-heading">
            Impostazioni
          </h1>
          <p className="text-muted-foreground mt-2">
            Gestisci il tuo account, le preferenze e la privacy
          </p>
        </div>

        {/* Global alerts */}
        {error && (
          <Alert variant="destructive" className="mb-6">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {success && (
          <Alert className="mb-6 border-green-500 bg-green-50 dark:bg-green-900/20">
            <AlertDescription className="text-green-800 dark:text-green-200">
              {success}
            </AlertDescription>
          </Alert>
        )}

        {/* Tabs with orange underline */}
        <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
          <TabsList className="grid w-full grid-cols-4 bg-transparent border-b border-border rounded-none p-0 h-auto">
            {TABS.map(tab => {
              const Icon = tab.icon;
              return (
                <TabsTrigger
                  key={tab.id}
                  value={tab.id}
                  className="relative flex items-center justify-center gap-2 py-3 px-4 rounded-none bg-transparent data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-orange-500 text-muted-foreground hover:text-foreground transition-colors after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:bg-orange-500 after:scale-x-0 data-[state=active]:after:scale-x-100 after:transition-transform"
                >
                  <Icon className="h-4 w-4" />
                  <span className="hidden sm:inline">{tab.label}</span>
                </TabsTrigger>
              );
            })}
          </TabsList>

          {/* Profile Tab */}
          <TabsContent value="profile" className="space-y-6">
            {/* Avatar and basic info */}
            <Card>
              <CardHeader>
                <CardTitle>Il tuo profilo</CardTitle>
                <CardDescription>Gestisci le informazioni del tuo profilo pubblico</CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Avatar section */}
                <div className="flex items-center gap-6">
                  <AvatarUpload
                    currentAvatarUrl={avatarUrl}
                    displayName={displayName || 'User'}
                    onUpload={async (file, previewUrl) => {
                      // Optimistic UI update
                      setAvatarUrl(previewUrl);
                      try {
                        // TODO: Upload to backend when API is available
                        // await api.auth.uploadAvatar(file);
                        setSuccess('Avatar aggiornato con successo!');
                      } catch (err) {
                        // Revert on error
                        setAvatarUrl(avatarUrl);
                        setError(getErrorMessage(err));
                      }
                    }}
                    disabled={loading}
                  />
                  <div className="flex-1">
                    <h3 className="text-lg font-semibold">{displayName || 'User'}</h3>
                    <p className="text-muted-foreground">{email}</p>
                    <p className="text-sm text-muted-foreground mt-1">
                      Membro dal{' '}
                      {profile?.createdAt ? new Date(profile.createdAt).toLocaleDateString() : '—'}
                    </p>
                  </div>
                </div>

                <Separator />

                {/* Profile form */}
                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="displayName">Nome visualizzato</Label>
                    <Input
                      id="displayName"
                      value={displayName}
                      onChange={e => setDisplayName(e.target.value)}
                      placeholder="Il tuo nome"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input
                      id="email"
                      type="email"
                      value={email}
                      onChange={e => setEmail(e.target.value)}
                      placeholder="your.email@example.com"
                      disabled
                    />
                    <p className="text-sm text-muted-foreground">
                      La modifica dell'email non è ancora supportata
                    </p>
                  </div>

                  <Button onClick={handleUpdateProfile} disabled={loading} className="bg-orange-500 hover:bg-orange-600" data-testid="save-profile-button">
                    Salva profilo
                  </Button>
                </div>
              </CardContent>
            </Card>

            {/* Badges section */}
            <Card>
              <CardHeader>
                <CardTitle>Badge e riconoscimenti</CardTitle>
                <CardDescription>I badge che hai guadagnato su MeepleAI</CardDescription>
              </CardHeader>
              <CardContent>
                {badges.length > 0 ? (
                  <div className="flex flex-wrap gap-3">
                    {badges.slice(0, 6).map(badge => (
                      <div
                        key={badge.id}
                        className="flex items-center gap-2 px-3 py-2 bg-muted rounded-lg"
                        title={badge.description}
                      >
                        <div
                          className={`w-3 h-3 rounded-full ${getBadgeTierColor(badge.tier)}`}
                        />
                        <span className="text-sm font-medium">{badge.name}</span>
                      </div>
                    ))}
                    {badges.length > 6 && (
                      <Button variant="ghost" size="sm" onClick={() => router.push('/badges')}>
                        +{badges.length - 6} altri
                      </Button>
                    )}
                  </div>
                ) : (
                  <p className="text-muted-foreground">
                    Non hai ancora guadagnato nessun badge. Continua a giocare per ottenerli!
                  </p>
                )}
              </CardContent>
            </Card>

            {/* Stats section */}
            <Card>
              <CardHeader>
                <CardTitle>Le tue statistiche</CardTitle>
                <CardDescription>Un riepilogo della tua attività su MeepleAI</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                  <div className="text-center p-4 bg-muted rounded-lg">
                    <div className="text-3xl font-bold text-orange-500">
                      {libraryStats?.totalGames ?? 0}
                    </div>
                    <div className="text-sm text-muted-foreground">Giochi in libreria</div>
                  </div>
                  <div className="text-center p-4 bg-muted rounded-lg">
                    <div className="text-3xl font-bold text-orange-500">
                      {libraryStats?.favoriteGames ?? 0}
                    </div>
                    <div className="text-sm text-muted-foreground">Preferiti</div>
                  </div>
                  <div className="text-center p-4 bg-muted rounded-lg">
                    <div className="text-3xl font-bold text-orange-500">{badges.length}</div>
                    <div className="text-sm text-muted-foreground">Badge</div>
                  </div>
                  <div className="text-center p-4 bg-muted rounded-lg">
                    <div className="text-3xl font-bold text-orange-500">—</div>
                    <div className="text-sm text-muted-foreground">Livello</div>
                  </div>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          {/* Preferences Tab */}
          <TabsContent value="preferences" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Preferenze utente</CardTitle>
                <CardDescription>
                  Personalizza la tua esperienza con lingua, tema e notifiche
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="space-y-2">
                  <Label htmlFor="language">Lingua</Label>
                  <Select
                    value={preferences.language}
                    onValueChange={value => setPreferences({ ...preferences, language: value })}
                  >
                    <SelectTrigger id="language">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="it">Italiano</SelectItem>
                      <SelectItem value="en">English</SelectItem>
                      <SelectItem value="es">Español</SelectItem>
                      <SelectItem value="fr">Français</SelectItem>
                      <SelectItem value="de">Deutsch</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="theme">Tema</Label>
                  <Select
                    value={preferences.theme}
                    onValueChange={(value: 'light' | 'dark' | 'system') =>
                      setPreferences({ ...preferences, theme: value })
                    }
                  >
                    <SelectTrigger id="theme">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="light">Chiaro</SelectItem>
                      <SelectItem value="dark">Scuro</SelectItem>
                      <SelectItem value="system">Sistema</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <Separator />

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="emailNotifications">Notifiche email</Label>
                    <p className="text-sm text-muted-foreground">
                      Ricevi aggiornamenti via email sulla tua attività
                    </p>
                  </div>
                  <Switch
                    id="emailNotifications"
                    checked={preferences.emailNotifications}
                    onCheckedChange={checked =>
                      setPreferences({ ...preferences, emailNotifications: checked })
                    }
                  />
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="animations">Animazioni</Label>
                    <p className="text-sm text-muted-foreground">
                      Abilita le animazioni nell'interfaccia
                    </p>
                  </div>
                  <Switch
                    id="animations"
                    checked={animationsEnabled}
                    onCheckedChange={setAnimationsEnabled}
                  />
                </div>

                <Button onClick={handleUpdatePreferences} className="bg-orange-500 hover:bg-orange-600" data-testid="save-preferences-button">
                  Salva preferenze
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          {/* Privacy Tab */}
          <TabsContent value="privacy" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Profilo pubblico</CardTitle>
                <CardDescription>
                  Controlla cosa gli altri utenti possono vedere del tuo profilo
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="publicProfile">Profilo visibile</Label>
                    <p className="text-sm text-muted-foreground">
                      Permetti ad altri utenti di vedere il tuo profilo
                    </p>
                  </div>
                  <Switch
                    id="publicProfile"
                    checked={privacyPrefs.publicProfile}
                    onCheckedChange={checked =>
                      setPrivacyPrefs({ ...privacyPrefs, publicProfile: checked })
                    }
                  />
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="showLibrary">Mostra libreria</Label>
                    <p className="text-sm text-muted-foreground">
                      Mostra la tua collezione di giochi agli altri
                    </p>
                  </div>
                  <Switch
                    id="showLibrary"
                    checked={privacyPrefs.showLibrary}
                    onCheckedChange={checked =>
                      setPrivacyPrefs({ ...privacyPrefs, showLibrary: checked })
                    }
                  />
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="showActivity">Mostra attività</Label>
                    <p className="text-sm text-muted-foreground">
                      Mostra la tua attività recente nel tuo profilo
                    </p>
                  </div>
                  <Switch
                    id="showActivity"
                    checked={privacyPrefs.showActivity}
                    onCheckedChange={checked =>
                      setPrivacyPrefs({ ...privacyPrefs, showActivity: checked })
                    }
                  />
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label htmlFor="showBadges">Mostra badge</Label>
                    <p className="text-sm text-muted-foreground">
                      Mostra i tuoi badge nel profilo pubblico
                    </p>
                  </div>
                  <Switch
                    id="showBadges"
                    checked={privacyPrefs.showBadges}
                    onCheckedChange={checked =>
                      setPrivacyPrefs({ ...privacyPrefs, showBadges: checked })
                    }
                  />
                </div>

                <Button onClick={handleUpdatePrivacy} className="bg-orange-500 hover:bg-orange-600">
                  Salva impostazioni privacy
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Conservazione dati</CardTitle>
                <CardDescription>
                  Quanto tempo conservare la tua cronologia chat e i dati di gioco
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="dataRetention">Periodo di conservazione</Label>
                  <Select
                    value={preferences.dataRetentionDays.toString()}
                    onValueChange={value =>
                      setPreferences({ ...preferences, dataRetentionDays: parseInt(value) })
                    }
                  >
                    <SelectTrigger id="dataRetention">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="30">30 giorni</SelectItem>
                      <SelectItem value="90">90 giorni</SelectItem>
                      <SelectItem value="180">180 giorni</SelectItem>
                      <SelectItem value="365">1 anno</SelectItem>
                      <SelectItem value="-1">Per sempre</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <Button onClick={handleUpdatePreferences} variant="outline">
                  Aggiorna conservazione dati
                </Button>
              </CardContent>
            </Card>

            {/* OAuth Section */}
            <Card>
              <CardHeader>
                <CardTitle>Account collegati</CardTitle>
                <CardDescription>
                  Collega i tuoi account social per un accesso più facile
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {OAUTH_PROVIDERS.map(provider => {
                  const linked = isLinked(provider.id);
                  const isCurrentlyUnlinking = unlinking === provider.id;

                  return (
                    <div
                      key={provider.id}
                      className="flex items-center justify-between p-4 border rounded-lg"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-muted flex items-center justify-center">
                          <span className="text-sm font-semibold">{provider.name[0]}</span>
                        </div>
                        <div>
                          <div className="font-medium">{provider.name}</div>
                          <div className="text-sm text-muted-foreground">
                            {linked ? 'Collegato' : 'Non collegato'}
                          </div>
                        </div>
                      </div>

                      {linked ? (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => handleUnlinkOAuth(provider.id)}
                          disabled={isCurrentlyUnlinking}
                        >
                          {isCurrentlyUnlinking ? 'Scollegamento...' : 'Scollega'}
                        </Button>
                      ) : (
                        <Button
                          size="sm"
                          onClick={() => handleLinkOAuth(provider.id)}
                          className={provider.color}
                        >
                          Collega
                        </Button>
                      )}
                    </div>
                  );
                })}
              </CardContent>
            </Card>
          </TabsContent>

          {/* Account Tab */}
          <TabsContent value="account" className="space-y-6">
            {/* Password change */}
            <Card>
              <CardHeader>
                <CardTitle>Cambia password</CardTitle>
                <CardDescription>Aggiorna la tua password per mantenere sicuro il tuo account</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="currentPassword">Password attuale</Label>
                  <Input
                    id="currentPassword"
                    type="password"
                    value={currentPassword}
                    onChange={e => setCurrentPassword(e.target.value)}
                    placeholder="Inserisci la password attuale"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="newPassword">Nuova password</Label>
                  <Input
                    id="newPassword"
                    type="password"
                    value={newPassword}
                    onChange={e => setNewPassword(e.target.value)}
                    placeholder="Inserisci la nuova password (min 8 caratteri)"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="confirmPassword">Conferma nuova password</Label>
                  <Input
                    id="confirmPassword"
                    type="password"
                    value={confirmPassword}
                    onChange={e => setConfirmPassword(e.target.value)}
                    placeholder="Conferma la nuova password"
                  />
                </div>

                <Button onClick={handleChangePassword} disabled={loading} className="bg-orange-500 hover:bg-orange-600" data-testid="change-password-button">
                  Cambia password
                </Button>
              </CardContent>
            </Card>

            {/* 2FA Section */}
            <Card>
              <CardHeader>
                <CardTitle>Autenticazione a due fattori</CardTitle>
                <CardDescription>Aggiungi un livello extra di sicurezza al tuo account</CardDescription>
              </CardHeader>
              <CardContent>
                {twoFactorStatus?.isEnabled ? (
                  <div className="space-y-4">
                    <Alert className="border-green-500 bg-green-50 dark:bg-green-900/20">
                      <AlertDescription className="text-green-800 dark:text-green-200">
                        ✓ L'autenticazione a due fattori è attiva
                        <br />
                        Codici di backup rimanenti: {twoFactorStatus.unusedBackupCodesCount}
                      </AlertDescription>
                    </Alert>

                    {twoFactorStatus.unusedBackupCodesCount < 3 && (
                      <Alert variant="destructive">
                        <AlertDescription>
                          ⚠️ Attenzione: hai solo {twoFactorStatus.unusedBackupCodesCount} codici di
                          backup rimasti. Considera di disabilitare e riabilitare il 2FA per
                          generare nuovi codici.
                        </AlertDescription>
                      </Alert>
                    )}

                    <Separator />

                    <div className="space-y-4">
                      <h4 className="font-medium">Disabilita autenticazione a due fattori</h4>
                      <div className="space-y-2">
                        <Label htmlFor="disablePassword">Password attuale</Label>
                        <Input
                          id="disablePassword"
                          type="password"
                          value={disablePassword}
                          onChange={e => setDisablePassword(e.target.value)}
                          placeholder="Inserisci la tua password"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="disableCode">Codice TOTP o codice di backup</Label>
                        <Input
                          id="disableCode"
                          type="text"
                          value={disableCode}
                          onChange={e => setDisableCode(e.target.value)}
                          placeholder="000000 o XXXX-XXXX"
                        />
                      </div>
                      <Button
                        variant="destructive"
                        onClick={handleDisable2FA}
                        disabled={loading || !disablePassword || !disableCode}
                      >
                        {loading ? 'Disabilitazione...' : 'Disabilita 2FA'}
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div>
                    {!setup ? (
                      <div className="space-y-4">
                        <p className="text-muted-foreground">
                          L'autenticazione a due fattori aggiunge un livello extra di sicurezza al
                          tuo account. Avrai bisogno della tua password e di un codice dalla tua app
                          di autenticazione per accedere.
                        </p>
                        <LoadingButton
                          onClick={handleSetup2FA}
                          isLoading={loading}
                          loadingText="Configurazione..."
                          className="bg-orange-500 hover:bg-orange-600"
                          data-testid="enable-2fa-button"
                        >
                          Abilita autenticazione a due fattori
                        </LoadingButton>
                      </div>
                    ) : (
                      <div className="space-y-6">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-lg">Passo 1: Scansiona il QR Code</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-4">
                            <p className="text-sm text-muted-foreground">
                              Scansiona questo QR code con la tua app di autenticazione (Google
                              Authenticator, Authy, etc.)
                            </p>
                            <div className="flex justify-center bg-card p-4 rounded border">
                              <QRCodeSVG value={setup.qrCodeUrl} size={256} />
                            </div>
                            <details className="text-sm">
                              <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                                Non riesci a scansionare? Inserisci manualmente
                              </summary>
                              <div className="mt-2 p-3 bg-muted rounded">
                                <code className="text-xs font-mono">{setup.secret}</code>
                              </div>
                            </details>
                          </CardContent>
                        </Card>

                        {showBackupCodes && (
                          <Alert variant="destructive">
                            <AlertDescription>
                              <h4 className="font-semibold mb-2">Passo 2: Salva i codici di backup</h4>
                              <p className="mb-3">
                                ⚠️ Salva questi codici in un luogo sicuro. Ogni codice può essere
                                usato una sola volta.
                              </p>
                              <div className="grid grid-cols-2 gap-2 mb-4">
                                {setup.backupCodes?.map((code: string, i: number) => (
                                  <div
                                    key={i}
                                    className="bg-card px-3 py-2 rounded font-mono text-sm text-center text-foreground"
                                  >
                                    {code}
                                  </div>
                                )) || null}
                              </div>
                              <div className="flex gap-2">
                                <Button onClick={downloadBackupCodes} variant="secondary" size="sm">
                                  Scarica codici
                                </Button>
                                <Button onClick={() => setShowBackupCodes(false)} size="sm">
                                  Ho salvato i miei codici
                                </Button>
                              </div>
                            </AlertDescription>
                          </Alert>
                        )}

                        {!showBackupCodes && (
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-lg">Passo 3: Verifica e abilita</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                              <p className="text-sm text-muted-foreground">
                                Inserisci il codice a 6 cifre dalla tua app di autenticazione
                              </p>
                              <div className="flex items-center gap-2">
                                <Input
                                  type="text"
                                  placeholder="000000"
                                  value={verificationCode}
                                  onChange={e =>
                                    setVerificationCode(
                                      e.target.value.replace(/\D/g, '').slice(0, 6)
                                    )
                                  }
                                  maxLength={6}
                                  className="w-32 text-center font-mono text-lg"
                                />
                                <Button
                                  onClick={handleEnable2FA}
                                  disabled={loading || verificationCode.length !== 6}
                                  className="bg-orange-500 hover:bg-orange-600"
                                  data-testid="verify-enable-2fa-button"
                                >
                                  {loading ? 'Verifica...' : 'Verifica e abilita'}
                                </Button>
                              </div>
                            </CardContent>
                          </Card>
                        )}

                        <Button
                          variant="ghost"
                          onClick={() => {
                            setSetup(null);
                            setShowBackupCodes(false);
                            setVerificationCode('');
                          }}
                        >
                          Annulla configurazione
                        </Button>
                      </div>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Active sessions */}
            <Card>
              <CardHeader>
                <CardTitle>Sessioni attive</CardTitle>
                <CardDescription>
                  Visualizza e gestisci le tue sessioni di login attive su tutti i dispositivi
                </CardDescription>
              </CardHeader>
              <CardContent>
                {sessionsLoading ? (
                  <div className="flex justify-center py-8">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-500" />
                  </div>
                ) : sessions.length === 0 ? (
                  <Alert>
                    <AlertDescription>Nessuna sessione attiva trovata</AlertDescription>
                  </Alert>
                ) : (
                  <div className="space-y-4">
                    {sessions.map(session => {
                      const isCurrent = isCurrentSession(session);
                      const isRevoking = revokingSessionId === session.id;

                      return (
                        <div
                          key={session.id}
                          className="flex items-start justify-between p-4 border rounded-lg"
                        >
                          <div className="flex-1 space-y-2">
                            <div className="flex items-center gap-2">
                              <div className="font-medium">
                                {getDeviceInfo(session.userAgent)}
                              </div>
                              {isCurrent && (
                                <span className="px-2 py-0.5 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 rounded">
                                  Sessione attuale
                                </span>
                              )}
                            </div>

                            <div className="text-sm text-muted-foreground space-y-1">
                              <div>
                                <span className="font-medium">IP:</span>{' '}
                                {session.ipAddress || 'Sconosciuto'}
                              </div>
                              <div>
                                <span className="font-medium">Creata:</span>{' '}
                                {formatDateTime(session.createdAt)}
                              </div>
                              <div>
                                <span className="font-medium">Scade:</span>{' '}
                                {formatDateTime(session.expiresAt)}
                              </div>
                            </div>
                          </div>

                          <Button
                            variant="destructive"
                            size="sm"
                            onClick={() => handleRevokeSession(session.id)}
                            disabled={isRevoking || isCurrent}
                            className="ml-4"
                          >
                            {isRevoking ? 'Revoca...' : 'Revoca'}
                          </Button>
                        </div>
                      );
                    })}

                    <div className="pt-4 border-t flex gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={loadUserSessions}
                        disabled={sessionsLoading}
                      >
                        {sessionsLoading ? 'Aggiornamento...' : 'Aggiorna sessioni'}
                      </Button>
                      {sessions.length > 1 && (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => setShowRevokeAllDialog(true)}
                          disabled={sessionsLoading}
                        >
                          Disconnetti tutti i dispositivi
                        </Button>
                      )}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Logout All Devices Dialog */}
            <Dialog open={showRevokeAllDialog} onOpenChange={setShowRevokeAllDialog}>
              <DialogContent className="sm:max-w-[425px]">
                <DialogHeader>
                  <DialogTitle>Disconnetti da tutti i dispositivi</DialogTitle>
                  <DialogDescription>
                    Questa operazione revocherà tutte le sessioni attive su tutti i dispositivi.
                    Dovrai effettuare nuovamente l'accesso su quei dispositivi.
                  </DialogDescription>
                </DialogHeader>
                <div className="space-y-4 py-4">
                  <div className="flex items-start space-x-3">
                    <Checkbox
                      id="includeCurrentSession"
                      checked={revokeAllIncludeCurrent}
                      onCheckedChange={(checked: boolean) => setRevokeAllIncludeCurrent(checked)}
                    />
                    <div className="grid gap-1.5 leading-none">
                      <label
                        htmlFor="includeCurrentSession"
                        className="text-sm font-medium cursor-pointer"
                      >
                        Includi sessione corrente
                      </label>
                      <p className="text-xs text-muted-foreground">
                        Se selezionato, verrai disconnesso immediatamente e dovrai effettuare
                        nuovamente l'accesso.
                      </p>
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="revokeAllPassword">Password (opzionale)</Label>
                    <Input
                      id="revokeAllPassword"
                      type="password"
                      placeholder="Inserisci la tua password per maggiore sicurezza"
                      value={revokeAllPassword}
                      onChange={e => setRevokeAllPassword(e.target.value)}
                      disabled={revokeAllLoading}
                    />
                  </div>
                </div>
                <DialogFooter>
                  <Button
                    variant="outline"
                    onClick={() => {
                      setShowRevokeAllDialog(false);
                      setRevokeAllIncludeCurrent(false);
                      setRevokeAllPassword('');
                    }}
                    disabled={revokeAllLoading}
                  >
                    Annulla
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={handleRevokeAllSessions}
                    disabled={revokeAllLoading}
                  >
                    {revokeAllLoading ? 'Disconnessione...' : 'Disconnetti tutti'}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>

            {/* API Key section */}
            <Card>
              <CardHeader>
                <CardTitle>Autenticazione API Key</CardTitle>
                <CardDescription>
                  Memorizza una API key per questa sessione del browser
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {apiKeyAuthenticated ? (
                  <div className="space-y-4">
                    <Alert className="bg-green-50 dark:bg-green-900/20 border-green-200 dark:border-green-800">
                      <AlertDescription className="text-green-800 dark:text-green-200">
                        ✓ Autenticazione API key attiva. Le richieste includeranno{' '}
                        <code>Authorization: ApiKey ****</code>.
                      </AlertDescription>
                    </Alert>
                    <Button variant="outline" onClick={handleApiKeyLogout} disabled={apiKeyLoading}>
                      {apiKeyLoading ? 'Rimozione...' : 'Rimuovi autenticazione API Key'}
                    </Button>
                  </div>
                ) : (
                  <div className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="api-key-input">API Key</Label>
                      <Input
                        id="api-key-input"
                        type="password"
                        placeholder="mpl_live_xxxxxxxxxxxxxxxx"
                        value={apiKeyInput}
                        onChange={e => setApiKeyInput(e.target.value)}
                        disabled={apiKeyLoading}
                      />
                      <p className="text-xs text-muted-foreground">
                        La tua API key sarà validata e memorizzata solo per questa sessione del
                        browser.
                      </p>
                    </div>

                    <Button
                      onClick={handleApiKeyLogin}
                      disabled={apiKeyLoading || !apiKeyInput.trim()}
                      className="bg-orange-500 hover:bg-orange-600"
                    >
                      {apiKeyLoading ? 'Autenticazione...' : 'Autentica con API Key'}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Danger Zone */}
            <Card className="border-red-200 dark:border-red-900">
              <CardHeader>
                <CardTitle className="text-red-600">Zona pericolosa</CardTitle>
                <CardDescription>Azioni irreversibili per il tuo account</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <Alert variant="destructive">
                  <AlertDescription>
                    L'eliminazione dell'account è permanente e non può essere annullata. Tutti i
                    tuoi dati verranno eliminati definitivamente.
                  </AlertDescription>
                </Alert>
                <Button
                  variant="destructive"
                  onClick={() => setShowDeleteDialog(true)}
                >
                  Elimina account
                </Button>
              </CardContent>
            </Card>

            {/* Delete Account Dialog */}
            <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
              <DialogContent className="sm:max-w-[425px]">
                <DialogHeader>
                  <DialogTitle className="text-red-600">Elimina account</DialogTitle>
                  <DialogDescription>
                    Questa azione è irreversibile. Tutti i tuoi dati, inclusa la libreria di giochi,
                    le chat e le preferenze verranno eliminati permanentemente.
                  </DialogDescription>
                </DialogHeader>
                <div className="space-y-4 py-4">
                  <div className="space-y-2">
                    <Label htmlFor="deleteConfirm">
                      Scrivi <strong>ELIMINA</strong> per confermare
                    </Label>
                    <Input
                      id="deleteConfirm"
                      value={deleteConfirmText}
                      onChange={e => setDeleteConfirmText(e.target.value)}
                      placeholder="ELIMINA"
                    />
                  </div>
                </div>
                <DialogFooter>
                  <Button
                    variant="outline"
                    onClick={() => {
                      setShowDeleteDialog(false);
                      setDeleteConfirmText('');
                    }}
                  >
                    Annulla
                  </Button>
                  <Button
                    variant="destructive"
                    disabled={deleteConfirmText !== 'ELIMINA'}
                    onClick={() => {
                      setShowDeleteDialog(false);
                      setSuccess('Funzionalità di eliminazione account in arrivo!');
                    }}
                  >
                    Elimina account
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </TabsContent>
        </Tabs>

        {/* Back to Home */}
        <div className="mt-8">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← Torna alla home
          </Button>
        </div>
      </div>
    </div>
  );
}
