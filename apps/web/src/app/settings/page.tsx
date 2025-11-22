/**
 * Settings Page - 4 Tabs Implementation (SPRINT-1, Issue #848)
 *
 * Comprehensive user settings page with:
 * - Profile tab: Display name, email, password change
 * - Preferences tab: Language, notifications, theme, data retention
 * - Privacy tab: 2FA management, OAuth account linking
 * - Advanced tab: API keys, sessions, account deletion
 *
 * Uses Authentication.Application layer via CQRS:
 * - GetUserProfileQuery for data
 * - UpdateUserProfileCommand for changes (placeholder)
 * - Enable2FACommand/Disable2FACommand for 2FA
 */

'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { QRCodeSVG } from 'qrcode.react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { LoadingButton } from '@/components/loading/LoadingButton';
import {
  api,
  type UserProfile,
  type UserPreferences,
  type TotpSetupResponse,
  type TwoFactorStatusDto,
  type UserSessionInfo
} from '@/lib/api';
import { hasStoredApiKey } from '@/lib/api/core/apiKeyStore';
import { getErrorMessage } from '@/lib/utils/errorHandler';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

// OAuth provider configuration
const OAUTH_PROVIDERS = [
  { id: 'google', name: 'Google', color: 'bg-blue-600 hover:bg-blue-700' },
  { id: 'discord', name: 'Discord', color: 'bg-indigo-600 hover:bg-indigo-700' },
  { id: 'github', name: 'GitHub', color: 'bg-slate-800 hover:bg-slate-900' },
];

interface OAuthAccount {
  provider: string;
  createdAt: string;
}

export default function SettingsPage() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState('profile');

  // Global state
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Profile state
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');

  // Password change state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Preferences state (mock data - backend not implemented)
  const [preferences, setPreferences] = useState<UserPreferences>({
    language: 'en',
    emailNotifications: true,
    theme: 'system',
    dataRetentionDays: 90,
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

  // Load initial data
  useEffect(() => {
    loadProfile();
    load2FAStatus();
    loadOAuthAccounts();
    setApiKeyAuthenticated(hasStoredApiKey());
    loadUserSessions();
  }, []);

  const loadProfile = async () => {
    try {
      setLoading(true);
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';
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
      setDisplayName(user.DisplayName);
      setEmail(user.Email);
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
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';
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
        createErrorContext('SettingsPage', 'loadOAuthAccounts', { operation: 'fetch_oauth_accounts' })
      );
    }
  };

  // Profile update handler (placeholder - backend not implemented)
  const handleUpdateProfile = async () => {
    setSuccess(null);
    setError(null);

    if (!displayName.trim()) {
      setError('Display name cannot be empty');
      return;
    }

    // TODO: Implement UpdateUserProfileCommand when backend is ready
    setError('Profile update not yet implemented (backend pending)');
  };

  // Password change handler (placeholder - backend not implemented)
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

    // TODO: Implement ChangePasswordCommand when backend is ready
    setError('Password change not yet implemented (backend pending)');
  };

  // Preferences update handler (mock - backend not implemented)
  const handleUpdatePreferences = async () => {
    setSuccess(null);
    setError(null);

    // Mock success for now
    setSuccess('Preferences saved successfully (mock - backend pending)');

    // TODO: Implement UpdatePreferencesCommand when backend is ready
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
    if (!setup?.BackupCodes) return;

    const text = `MeepleAI Backup Codes\n\n${setup.BackupCodes.join('\n')}\n\nKeep these codes in a secure location. Each code can only be used once.`;
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
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';
    window.location.href = `${apiBase}/api/v1/auth/oauth/${provider}/login`;
  };

  const handleUnlinkOAuth = async (provider: string) => {
    if (!confirm(`Unlink your ${provider} account?`)) {
      return;
    }

    setUnlinking(provider);

    try {
      const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';
      const res = await fetch(`${apiBase}/api/v1/auth/oauth/${provider}/unlink`, {
        method: 'DELETE',
        credentials: 'include',
      });

      if (res.ok) {
        setLinkedAccounts(linkedAccounts.filter((a) => a.provider !== provider));
        setSuccess(`${provider} account unlinked successfully`);
      } else {
        setError('Failed to unlink account. Please try again.');
      }
    } catch (error) {
      logger.error(
        'Failed to unlink OAuth account',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleUnlinkOAuth', { provider, operation: 'unlink_oauth' })
      );
      setError('Failed to unlink account.');
    } finally {
      setUnlinking(null);
    }
  };

  const isLinked = (providerId: string) =>
    linkedAccounts.some((a) => a.provider === providerId);

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
      setApiKeyInput(''); // Clear input for security
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
      // Reload sessions list
      await loadUserSessions();
    } catch (error) {
      logger.error(
        'Failed to revoke session',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('SettingsPage', 'handleRevokeSession', { sessionId, operation: 'revoke_session' })
      );
      setError(getErrorMessage(error, 'Failed to revoke session'));
    } finally {
      setRevokingSessionId(null);
    }
  };

  const formatDateTime = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const getDeviceInfo = (userAgent: string | null) => {
    if (!userAgent) return 'Unknown device';

    // Simple device detection
    if (userAgent.includes('Mobile')) return 'Mobile device';
    if (userAgent.includes('Tablet')) return 'Tablet';
    if (userAgent.includes('Windows')) return 'Windows PC';
    if (userAgent.includes('Mac')) return 'Mac';
    if (userAgent.includes('Linux')) return 'Linux';

    return 'Unknown device';
  };

  const isCurrentSession = (session: UserSessionInfo) => {
    // Simple heuristic: the most recently seen session is likely the current one
    const now = new Date();
    const lastSeen = session.LastSeenAt ? new Date(session.LastSeenAt) : new Date(session.CreatedAt);
    const diffMinutes = (now.getTime() - lastSeen.getTime()) / (1000 * 60);
    return diffMinutes < 5; // Consider sessions active within last 5 minutes as "current"
  };

  if (loading && !profile) {
    return (
      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-slate-50 dark:bg-slate-900 py-8 px-4">
        <div className="max-w-5xl mx-auto">
          {/* Header */}
          <div className="mb-8">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">Settings</h1>
            <p className="text-slate-600 dark:text-slate-400 mt-2">
              Manage your account settings and preferences
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

          {/* Tabs */}
          <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="profile">Profile</TabsTrigger>
              <TabsTrigger value="preferences">Preferences</TabsTrigger>
              <TabsTrigger value="privacy">Privacy</TabsTrigger>
              <TabsTrigger value="advanced">Advanced</TabsTrigger>
            </TabsList>

            {/* Profile Tab */}
            <TabsContent value="profile" className="space-y-6">
              <Card>
                <CardHeader>
                  <CardTitle>Profile Information</CardTitle>
                  <CardDescription>
                    Update your display name and email address
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="displayName">Display Name</Label>
                    <Input
                      id="displayName"
                      value={displayName}
                      onChange={(e) => setDisplayName(e.target.value)}
                      placeholder="Your display name"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input
                      id="email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="your.email@example.com"
                      disabled
                    />
                    <p className="text-sm text-slate-500">
                      Email changes not yet supported
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="role">Role</Label>
                    <Input id="role" value={profile?.Role || ''} disabled />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="memberSince">Member Since</Label>
                    <Input
                      id="memberSince"
                      value={profile?.CreatedAt ? new Date(profile.CreatedAt).toLocaleDateString() : ''}
                      disabled
                    />
                  </div>

                  <Button onClick={handleUpdateProfile} disabled={true}>
                    Update Profile (Coming Soon)
                  </Button>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Change Password</CardTitle>
                  <CardDescription>
                    Update your password to keep your account secure
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="currentPassword">Current Password</Label>
                    <Input
                      id="currentPassword"
                      type="password"
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="Enter current password"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="newPassword">New Password</Label>
                    <Input
                      id="newPassword"
                      type="password"
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="Enter new password (min 8 characters)"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="confirmPassword">Confirm New Password</Label>
                    <Input
                      id="confirmPassword"
                      type="password"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="Confirm new password"
                    />
                  </div>

                  <Button onClick={handleChangePassword} disabled={true}>
                    Change Password (Coming Soon)
                  </Button>
                </CardContent>
              </Card>
            </TabsContent>

            {/* Preferences Tab */}
            <TabsContent value="preferences" className="space-y-6">
              <Card>
                <CardHeader>
                  <CardTitle>User Preferences</CardTitle>
                  <CardDescription>
                    Customize your experience with language, theme, and notification settings
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-6">
                  <div className="space-y-2">
                    <Label htmlFor="language">Language</Label>
                    <Select
                      value={preferences.language}
                      onValueChange={(value) => setPreferences({...preferences, language: value})}
                    >
                      <SelectTrigger id="language">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="en">English</SelectItem>
                        <SelectItem value="it">Italiano</SelectItem>
                        <SelectItem value="es">Español</SelectItem>
                        <SelectItem value="fr">Français</SelectItem>
                        <SelectItem value="de">Deutsch</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="theme">Theme</Label>
                    <Select
                      value={preferences.theme}
                      onValueChange={(value: 'light' | 'dark' | 'system') =>
                        setPreferences({...preferences, theme: value})
                      }
                    >
                      <SelectTrigger id="theme">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="light">Light</SelectItem>
                        <SelectItem value="dark">Dark</SelectItem>
                        <SelectItem value="system">System</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="flex items-center justify-between">
                    <div className="space-y-0.5">
                      <Label htmlFor="emailNotifications">Email Notifications</Label>
                      <p className="text-sm text-slate-500">
                        Receive email updates about your account activity
                      </p>
                    </div>
                    <Switch
                      id="emailNotifications"
                      checked={preferences.emailNotifications}
                      onCheckedChange={(checked) =>
                        setPreferences({...preferences, emailNotifications: checked})
                      }
                    />
                  </div>

                  <Separator />

                  <div className="space-y-2">
                    <Label htmlFor="dataRetention">Data Retention (days)</Label>
                    <Select
                      value={preferences.dataRetentionDays.toString()}
                      onValueChange={(value) =>
                        setPreferences({...preferences, dataRetentionDays: parseInt(value)})
                      }
                    >
                      <SelectTrigger id="dataRetention">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="30">30 days</SelectItem>
                        <SelectItem value="90">90 days</SelectItem>
                        <SelectItem value="180">180 days</SelectItem>
                        <SelectItem value="365">1 year</SelectItem>
                        <SelectItem value="-1">Forever</SelectItem>
                      </SelectContent>
                    </Select>
                    <p className="text-sm text-slate-500">
                      How long to keep your chat history and game data
                    </p>
                  </div>

                  <Button onClick={handleUpdatePreferences}>
                    Save Preferences
                  </Button>
                </CardContent>
              </Card>
            </TabsContent>

            {/* Privacy Tab */}
            <TabsContent value="privacy" className="space-y-6">
              {/* 2FA Section */}
              <Card>
                <CardHeader>
                  <CardTitle>Two-Factor Authentication</CardTitle>
                  <CardDescription>
                    Add an extra layer of security to your account
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {twoFactorStatus?.IsEnabled ? (
                    <div className="space-y-4">
                      <Alert className="border-green-500 bg-green-50 dark:bg-green-900/20">
                        <AlertDescription className="text-green-800 dark:text-green-200">
                          ✓ Two-factor authentication is enabled
                          <br />
                          Backup codes remaining: {twoFactorStatus.UnusedBackupCodesCount}
                        </AlertDescription>
                      </Alert>

                      {twoFactorStatus.UnusedBackupCodesCount < 3 && (
                        <Alert variant="destructive">
                          <AlertDescription>
                            ⚠️ Warning: You have only {twoFactorStatus.UnusedBackupCodesCount} backup codes remaining.
                            Consider disabling and re-enabling 2FA to generate new backup codes.
                          </AlertDescription>
                        </Alert>
                      )}

                      <Separator />

                      <div className="space-y-4">
                        <h4 className="font-medium">Disable Two-Factor Authentication</h4>
                        <div className="space-y-2">
                          <Label htmlFor="disablePassword">Current Password</Label>
                          <Input
                            id="disablePassword"
                            type="password"
                            value={disablePassword}
                            onChange={(e) => setDisablePassword(e.target.value)}
                            placeholder="Enter your password"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="disableCode">TOTP Code or Backup Code</Label>
                          <Input
                            id="disableCode"
                            type="text"
                            value={disableCode}
                            onChange={(e) => setDisableCode(e.target.value)}
                            placeholder="000000 or XXXX-XXXX"
                          />
                        </div>
                        <Button
                          variant="destructive"
                          onClick={handleDisable2FA}
                          disabled={loading || !disablePassword || !disableCode}
                        >
                          {loading ? 'Disabling...' : 'Disable 2FA'}
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <div>
                      {!setup ? (
                        <div className="space-y-4">
                          <p className="text-slate-600 dark:text-slate-400">
                            Two-factor authentication adds an extra layer of security to your account.
                            You'll need your password and a code from your authenticator app to log in.
                          </p>
                          <LoadingButton
                            onClick={handleSetup2FA}
                            isLoading={loading}
                            loadingText="Setting up..."
                          >
                            Enable Two-Factor Authentication
                          </LoadingButton>
                        </div>
                      ) : (
                        <div className="space-y-6">
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-lg">Step 1: Scan QR Code</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                              <p className="text-sm text-slate-600 dark:text-slate-400">
                                Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)
                              </p>
                              <div className="flex justify-center bg-white p-4 rounded border">
                                <QRCodeSVG value={setup.QrCodeUrl} size={256} />
                              </div>
                              <details className="text-sm">
                                <summary className="cursor-pointer text-slate-600 hover:text-slate-900">
                                  Can't scan? Enter manually
                                </summary>
                                <div className="mt-2 p-3 bg-slate-100 dark:bg-slate-800 rounded">
                                  <code className="text-xs font-mono">{setup.Secret}</code>
                                </div>
                              </details>
                            </CardContent>
                          </Card>

                          {showBackupCodes && (
                            <Alert variant="destructive">
                              <AlertDescription>
                                <h4 className="font-semibold mb-2">Step 2: Save Backup Codes</h4>
                                <p className="mb-3">
                                  ⚠️ Save these codes in a secure location. Each code can only be used once.
                                </p>
                                <div className="grid grid-cols-2 gap-2 mb-4">
                                  {setup.BackupCodes.map((code: string, i: number) => (
                                    <div
                                      key={i}
                                      className="bg-white px-3 py-2 rounded font-mono text-sm text-center text-black"
                                    >
                                      {code}
                                    </div>
                                  ))}
                                </div>
                                <div className="flex gap-2">
                                  <Button onClick={downloadBackupCodes} variant="secondary" size="sm">
                                    Download Codes
                                  </Button>
                                  <Button onClick={() => setShowBackupCodes(false)} size="sm">
                                    I've Saved My Codes
                                  </Button>
                                </div>
                              </AlertDescription>
                            </Alert>
                          )}

                          {!showBackupCodes && (
                            <Card>
                              <CardHeader>
                                <CardTitle className="text-lg">Step 3: Verify & Enable</CardTitle>
                              </CardHeader>
                              <CardContent className="space-y-4">
                                <p className="text-sm text-slate-600 dark:text-slate-400">
                                  Enter the 6-digit code from your authenticator app
                                </p>
                                <div className="flex items-center gap-2">
                                  <Input
                                    type="text"
                                    placeholder="000000"
                                    value={verificationCode}
                                    onChange={(e) => setVerificationCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                                    maxLength={6}
                                    className="w-32 text-center font-mono text-lg"
                                  />
                                  <Button
                                    onClick={handleEnable2FA}
                                    disabled={loading || verificationCode.length !== 6}
                                  >
                                    {loading ? 'Verifying...' : 'Verify & Enable'}
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
                            Cancel Setup
                          </Button>
                        </div>
                      )}
                    </div>
                  )}
                </CardContent>
              </Card>

              {/* OAuth Section */}
              <Card>
                <CardHeader>
                  <CardTitle>Linked Accounts</CardTitle>
                  <CardDescription>
                    Connect your accounts to login with social providers
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {OAUTH_PROVIDERS.map((provider) => {
                    const linked = isLinked(provider.id);
                    const isCurrentlyUnlinking = unlinking === provider.id;

                    return (
                      <div
                        key={provider.id}
                        className="flex items-center justify-between p-4 border border-slate-200 dark:border-slate-700 rounded-lg"
                      >
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center">
                            <span className="text-sm font-semibold text-slate-700 dark:text-slate-300">
                              {provider.name[0]}
                            </span>
                          </div>
                          <div>
                            <div className="font-medium text-slate-900 dark:text-white">
                              {provider.name}
                            </div>
                            <div className="text-sm text-slate-500 dark:text-slate-400">
                              {linked ? 'Connected' : 'Not connected'}
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
                            {isCurrentlyUnlinking ? 'Unlinking...' : 'Unlink'}
                          </Button>
                        ) : (
                          <Button
                            size="sm"
                            onClick={() => handleLinkOAuth(provider.id)}
                            className={provider.color}
                          >
                            Link
                          </Button>
                        )}
                      </div>
                    );
                  })}
                </CardContent>
              </Card>
            </TabsContent>

            {/* Advanced Tab */}
            <TabsContent value="advanced" className="space-y-6">
              <Card>
                <CardHeader>
                  <CardTitle>API Key Authentication</CardTitle>
                  <CardDescription>
                    Store an API key for this browser session. We send it via the Authorization header on every request.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {apiKeyAuthenticated ? (
                    <div className="space-y-4">
                      <Alert className="bg-green-50 dark:bg-green-900/20 border-green-200 dark:border-green-800">
                        <AlertDescription className="text-green-800 dark:text-green-200">
                          ✓ API key authentication active. Requests now include <code>Authorization: ApiKey ****</code>.
                        </AlertDescription>
                      </Alert>
                      <Button
                        variant="outline"
                        onClick={handleApiKeyLogout}
                        disabled={apiKeyLoading}
                      >
                        {apiKeyLoading ? 'Removing...' : 'Remove API Key Authentication'}
                      </Button>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <Alert>
                        <AlertDescription>
                          <div className="space-y-2">
                            <p className="font-medium">Session-Scoped API Key Storage</p>
                            <ul className="text-sm space-y-1 ml-4 list-disc">
                              <li>The key lives in sessionStorage (cleared on tab/browser close)</li>
                              <li>Automatically added to the <code>Authorization</code> header</li>
                              <li>No cookies or localStorage usage</li>
                              <li>Great for testing admin tools or short-lived sessions</li>
                            </ul>
                          </div>
                        </AlertDescription>
                      </Alert>

                      <div className="space-y-2">
                        <Label htmlFor="api-key-input">API Key</Label>
                        <Input
                          id="api-key-input"
                          type="password"
                          placeholder="mpl_live_xxxxxxxxxxxxxxxx"
                          value={apiKeyInput}
                          onChange={(e) => setApiKeyInput(e.target.value)}
                          disabled={apiKeyLoading}
                        />
                        <p className="text-xs text-slate-500 dark:text-slate-400">
                          Your API key will be validated and stored for this browser session only.
                        </p>
                      </div>

                      <Button
                        onClick={handleApiKeyLogin}
                        disabled={apiKeyLoading || !apiKeyInput.trim()}
                      >
                        {apiKeyLoading ? 'Authenticating...' : 'Login with API Key'}
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Active Sessions</CardTitle>
                  <CardDescription>
                    View and manage your active login sessions across all devices
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {sessionsLoading ? (
                    <div className="flex justify-center py-8">
                      <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
                    </div>
                  ) : sessions.length === 0 ? (
                    <Alert>
                      <AlertDescription>
                        No active sessions found
                      </AlertDescription>
                    </Alert>
                  ) : (
                    <div className="space-y-4">
                      {sessions.map((session) => {
                        const isCurrent = isCurrentSession(session);
                        const isRevoking = revokingSessionId === session.Id;

                        return (
                          <div
                            key={session.Id}
                            className="flex items-start justify-between p-4 border border-slate-200 dark:border-slate-700 rounded-lg"
                          >
                            <div className="flex-1 space-y-2">
                              <div className="flex items-center gap-2">
                                <div className="font-medium text-slate-900 dark:text-white">
                                  {getDeviceInfo(session.UserAgent)}
                                </div>
                                {isCurrent && (
                                  <span className="px-2 py-0.5 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400 rounded">
                                    Current Session
                                  </span>
                                )}
                              </div>

                              <div className="text-sm text-slate-600 dark:text-slate-400 space-y-1">
                                <div>
                                  <span className="font-medium">IP Address:</span>{' '}
                                  {session.IpAddress || 'Unknown'}
                                </div>
                                <div>
                                  <span className="font-medium">Created:</span>{' '}
                                  {formatDateTime(session.CreatedAt)}
                                </div>
                                <div>
                                  <span className="font-medium">Last Seen:</span>{' '}
                                  {session.LastSeenAt
                                    ? formatDateTime(session.LastSeenAt)
                                    : 'Never'}
                                </div>
                                <div>
                                  <span className="font-medium">Expires:</span>{' '}
                                  {formatDateTime(session.ExpiresAt)}
                                </div>
                                {session.UserAgent && (
                                  <details className="mt-2">
                                    <summary className="cursor-pointer text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300">
                                      Show user agent
                                    </summary>
                                    <div className="mt-1 p-2 bg-slate-100 dark:bg-slate-800 rounded text-xs font-mono break-all">
                                      {session.UserAgent}
                                    </div>
                                  </details>
                                )}
                              </div>
                            </div>

                            <Button
                              variant="destructive"
                              size="sm"
                              onClick={() => handleRevokeSession(session.Id)}
                              disabled={isRevoking || isCurrent}
                              className="ml-4"
                            >
                              {isRevoking ? 'Revoking...' : 'Revoke'}
                            </Button>
                          </div>
                        );
                      })}

                      <div className="pt-4 border-t border-slate-200 dark:border-slate-700">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={loadUserSessions}
                          disabled={sessionsLoading}
                        >
                          {sessionsLoading ? 'Refreshing...' : 'Refresh Sessions'}
                        </Button>
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Danger Zone</CardTitle>
                  <CardDescription>
                    Irreversible actions for your account
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Alert variant="destructive">
                    <AlertDescription>
                      Account deletion is permanent and cannot be undone.
                      All your data will be permanently deleted.
                    </AlertDescription>
                  </Alert>
                  <Button variant="destructive" disabled>
                    Delete Account (Coming Soon)
                  </Button>
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>

          {/* Back to Home */}
          <div className="mt-8">
            <Button variant="ghost" onClick={() => router.push('/')}>
              ← Back to Home
            </Button>
          </div>
        </div>
      </div>
  );
}

