/**
 * /invites/[token]/unsubscribe — stop game night emails at the address behind an invitation (#4846).
 *
 * Every email to a game night guest ends with a link here. The guest is an address, not an account: there is no
 * profile to open, so the invitation token is the credential and the page does one thing.
 *
 * Server component shell, like its sibling `/invites/[token]`:
 *   - the token is in the path and must NEVER be indexed or shared via OpenGraph, so the metadata is static and
 *     `robots: { index: false, follow: false }`;
 *   - nothing is fetched here. Opening the link does not unsubscribe anyone: mail scanners and link previews
 *     open links before the person does, so the client asks first and the tap on the button is what counts.
 */

import type { JSX } from 'react';

import { UnsubscribePageClient } from './page-client';

import type { Metadata } from 'next';

export const dynamic = 'force-dynamic';

interface UnsubscribeRouteParams {
  readonly token: string;
}

interface UnsubscribePageProps {
  readonly params: Promise<UnsubscribeRouteParams>;
}

export async function generateMetadata({ params: _ }: UnsubscribePageProps): Promise<Metadata> {
  return {
    title: 'Disiscrizione dagli inviti — MeepleAI',
    description: 'Smetti di ricevere le email sulle serate di gioco a questo indirizzo.',
    robots: { index: false, follow: false },
    openGraph: { title: 'Disiscrizione dagli inviti — MeepleAI' },
  };
}

export default async function UnsubscribePage({
  params,
}: UnsubscribePageProps): Promise<JSX.Element> {
  const { token } = await params;

  return <UnsubscribePageClient token={token} />;
}
