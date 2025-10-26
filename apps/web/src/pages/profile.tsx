/**
 * User Profile Page (AUTH-06-P3)
 *
 * Displays user information and linked OAuth accounts.
 * Allows linking/unlinking OAuth providers.
 */

import { useEffect, useState } from 'react';
import { useRouter } from 'next/router';
import Head from 'next/head';
import Link from 'next/link';

interface OAuthAccount {
  provider: string;
  createdAt: string;
}

interface UserInfo {
  id: string;
  email: string;
  displayName: string | null;
  role: string;
}

const PROVIDERS = [
  { id: 'google', name: 'Google', color: 'bg-blue-600 hover:bg-blue-700' },
  { id: 'discord', name: 'Discord', color: 'bg-indigo-600 hover:bg-indigo-700' },
  { id: 'github', name: 'GitHub', color: 'bg-slate-800 hover:bg-slate-900' },
];

export default function ProfilePage() {
  const router = useRouter();
  const [user, setUser] = useState<UserInfo | null>(null);
  const [linkedAccounts, setLinkedAccounts] = useState<OAuthAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [unlinking, setUnlinking] = useState<string | null>(null);

  useEffect(() => {
    async function loadProfile() {
      try {
        const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

        // Load user info
        const userRes = await fetch(`${apiBase}/api/v1/auth/me`, {
          credentials: 'include',
        });

        if (!userRes.ok) {
          router.push('/login');
          return;
        }

        const userData = await userRes.json();
        setUser(userData.user);

        // Load linked OAuth accounts
        const oauthRes = await fetch(`${apiBase}/api/v1/users/me/oauth-accounts`, {
          credentials: 'include',
        });

        if (oauthRes.ok) {
          const accounts = await oauthRes.json();
          setLinkedAccounts(accounts);
        }
      } catch (error) {
        console.error('Failed to load profile:', error);
      } finally {
        setLoading(false);
      }
    }

    loadProfile();
  }, [router]);

  const handleLink = (provider: string) => {
    const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    window.location.href = `${apiBase}/api/v1/auth/oauth/${provider}/login`;
  };

  const handleUnlink = async (provider: string) => {
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
        setLinkedAccounts(linkedAccounts.filter((a) => a.provider !== provider));
      } else {
        alert('Failed to unlink account. Please try again.');
      }
    } catch (error) {
      console.error('Unlink error:', error);
      alert('Failed to unlink account.');
    } finally {
      setUnlinking(null);
    }
  };

  const isLinked = (providerId: string) =>
    linkedAccounts.some((a) => a.provider === providerId);

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  if (!user) {
    return null;
  }

  return (
    <>
      <Head>
        <title>Profile - MeepleAI</title>
      </Head>

      <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 py-12 px-4">
        <div className="max-w-2xl mx-auto space-y-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">
              Profile
            </h1>
            <Link
              href="/"
              className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
            >
              Back to Home
            </Link>
          </div>

          {/* User Info Card */}
          <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6">
            <h2 className="text-xl font-semibold text-slate-900 dark:text-white mb-4">
              Account Information
            </h2>
            <dl className="space-y-3">
              <div>
                <dt className="text-sm font-medium text-slate-500 dark:text-slate-400">
                  Email
                </dt>
                <dd className="mt-1 text-base text-slate-900 dark:text-white">
                  {user.email}
                </dd>
              </div>
              {user.displayName && (
                <div>
                  <dt className="text-sm font-medium text-slate-500 dark:text-slate-400">
                    Display Name
                  </dt>
                  <dd className="mt-1 text-base text-slate-900 dark:text-white">
                    {user.displayName}
                  </dd>
                </div>
              )}
              <div>
                <dt className="text-sm font-medium text-slate-500 dark:text-slate-400">
                  Role
                </dt>
                <dd className="mt-1 text-base text-slate-900 dark:text-white capitalize">
                  {user.role.toLowerCase()}
                </dd>
              </div>
            </dl>
          </div>

          {/* Linked OAuth Accounts Card */}
          <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6">
            <h2 className="text-xl font-semibold text-slate-900 dark:text-white mb-4">
              Linked Accounts
            </h2>
            <p className="text-sm text-slate-600 dark:text-slate-400 mb-6">
              Connect your accounts to login with social providers.
            </p>

            <div className="space-y-4">
              {PROVIDERS.map((provider) => {
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
                      <button
                        onClick={() => handleUnlink(provider.id)}
                        disabled={isCurrentlyUnlinking}
                        className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-red-400 text-white text-sm font-medium rounded-lg transition-colors focus:outline-none focus:ring-2 focus:ring-red-500"
                      >
                        {isCurrentlyUnlinking ? 'Unlinking...' : 'Unlink'}
                      </button>
                    ) : (
                      <button
                        onClick={() => handleLink(provider.id)}
                        className={`px-4 py-2 ${provider.color} text-white text-sm font-medium rounded-lg transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2`}
                      >
                        Link
                      </button>
                    )}
                  </div>
                );
              })}
            </div>

            <p className="mt-6 text-xs text-slate-500 dark:text-slate-400">
              Linking an account allows you to login without a password.
            </p>
          </div>
        </div>
      </div>
    </>
  );
}
