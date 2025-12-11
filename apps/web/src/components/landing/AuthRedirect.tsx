/**
 * Client-only helper that sends authenticated visitors to l'app privata.
 * È innocuo per SEO perché non rende nulla sul server.
 */
'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

const SESSION_COOKIE = 'meepleai_session';

const hasSessionCookie = () =>
  typeof document !== 'undefined' &&
  document.cookie.split(';').some(c => c.trim().startsWith(`${SESSION_COOKIE}=`));

export function AuthRedirect() {
  const router = useRouter();

  useEffect(() => {
    if (hasSessionCookie()) {
      router.replace('/app');
    }
  }, [router]);

  return null;
}
