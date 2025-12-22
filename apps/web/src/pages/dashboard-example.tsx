/**
 * Dashboard Example Page (Issue #1834: UI-007)
 *
 * Demonstrates QuickActions component integration
 * Remove this file after integrating into actual dashboard
 */

import React from 'react';

import Head from 'next/head';

import { QuickActions } from '@/components/dashboard';

export default function DashboardExample() {
  return (
    <>
      <Head>
        <title>Dashboard Example - MeepleAI</title>
        <meta name="description" content="QuickActions component example" />
      </Head>

      <main className="container mx-auto max-w-5xl px-4 py-8">
        <div className="space-y-8">
          {/* Header */}
          <div>
            <h1 className="text-3xl font-bold font-quicksand mb-2">Dashboard</h1>
            <p className="text-muted-foreground">Welcome back! Quick actions to get started:</p>
          </div>

          {/* QuickActions Component */}
          <section>
            <h2 className="text-xl font-semibold font-quicksand mb-4">Quick Actions</h2>
            <QuickActions />
          </section>

          {/* Placeholder for other dashboard content */}
          <section className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            <div className="rounded-lg border p-6">
              <h3 className="font-semibold mb-2">Recent Games</h3>
              <p className="text-sm text-muted-foreground">
                Your recently added games will appear here
              </p>
            </div>
            <div className="rounded-lg border p-6">
              <h3 className="font-semibold mb-2">Recent Chats</h3>
              <p className="text-sm text-muted-foreground">Your recent chat conversations</p>
            </div>
            <div className="rounded-lg border p-6">
              <h3 className="font-semibold mb-2">Activity</h3>
              <p className="text-sm text-muted-foreground">Your recent activity feed</p>
            </div>
          </section>
        </div>
      </main>
    </>
  );
}
