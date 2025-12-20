/**
 * MeepleAI Dashboard - Redesigned
 * Editorial Playful aesthetic with asymmetric layouts
 * Game-inspired micro-interactions
 *
 * Uses App Router (Next.js 16) with Client Components
 */

'use client';

import React, { useState } from 'react';

import Link from 'next/link';

import { AppShell } from '@/components/layout/app-shell';

interface GameSession {
  id: string;
  gameName: string;
  players: number;
  duration: string;
  status: 'active' | 'paused' | 'completed';
  lastPlayed: string;
  cover?: string;
}

interface QuickAction {
  label: string;
  icon: string;
  href: string;
  color: string;
  description: string;
}

const mockGameSessions: GameSession[] = [
  {
    id: '1',
    gameName: 'Terraforming Mars',
    players: 4,
    duration: '2h 15m',
    status: 'active',
    lastPlayed: '5 min ago',
    cover: '🪐',
  },
  {
    id: '2',
    gameName: 'Wingspan',
    players: 2,
    duration: '1h 30m',
    status: 'paused',
    lastPlayed: '2 hours ago',
    cover: '🦅',
  },
  {
    id: '3',
    gameName: 'Gloomhaven',
    players: 3,
    duration: '3h 45m',
    status: 'completed',
    lastPlayed: 'Yesterday',
    cover: '⚔️',
  },
];

const quickActions: QuickAction[] = [
  {
    label: 'Ask AI',
    icon: '💬',
    href: '/chat',
    color: 'var(--color-blue)',
    description: 'Get rules clarifications',
  },
  {
    label: 'New Game',
    icon: '🎲',
    href: '/games/new',
    color: 'var(--color-green)',
    description: 'Start a session',
  },
  {
    label: 'Library',
    icon: '📚',
    href: '/library',
    color: 'var(--color-secondary-500)',
    description: 'Browse collection',
  },
  {
    label: 'Upload PDF',
    icon: '📄',
    href: '/upload',
    color: 'var(--color-red)',
    description: 'Add new rulebook',
  },
];

export default function DashboardRedesign() {
  const [activeTab, setActiveTab] = useState<'recent' | 'favorites' | 'all'>('recent');

  return (
    <AppShell>
      <div className="dashboard-redesign">
        {/* Hero Section - Asymmetric with large typography */}
        <section className="hero-section">
          <div className="hero-content">
            <h1 className="hero-title">
              Welcome back, <br />
              <span className="text-gradient">Player!</span>
            </h1>
            <p className="hero-subtitle">Ready for your next game night? 🎲</p>

            {/* Stats Cards - Playful floating layout */}
            <div className="stats-grid">
              <div className="stat-card stat-card-primary">
                <div className="stat-icon">🎮</div>
                <div className="stat-content">
                  <p className="stat-label">Games Played</p>
                  <p className="stat-value">127</p>
                </div>
                <div className="stat-decoration"></div>
              </div>

              <div className="stat-card stat-card-secondary">
                <div className="stat-icon">⏱️</div>
                <div className="stat-content">
                  <p className="stat-label">Hours Played</p>
                  <p className="stat-value">342h</p>
                </div>
                <div className="stat-decoration"></div>
              </div>

              <div className="stat-card stat-card-accent">
                <div className="stat-icon">🏆</div>
                <div className="stat-content">
                  <p className="stat-label">Win Rate</p>
                  <p className="stat-value">64%</p>
                </div>
                <div className="stat-decoration"></div>
              </div>
            </div>
          </div>

          {/* Quick Actions - Asymmetric grid */}
          <div className="quick-actions">
            <h2 className="section-heading">Quick Actions</h2>
            <div className="actions-grid">
              {quickActions.map((action, index) => (
                <Link
                  key={action.href}
                  href={action.href}
                  className="action-card"
                  style={
                    {
                      '--action-color': action.color,
                      '--action-delay': `${index * 50}ms`,
                    } as React.CSSProperties
                  }
                >
                  <div className="action-icon">{action.icon}</div>
                  <div className="action-content">
                    <h3 className="action-label">{action.label}</h3>
                    <p className="action-description">{action.description}</p>
                  </div>
                  <div className="action-arrow">→</div>
                </Link>
              ))}
            </div>
          </div>
        </section>

        {/* Game Sessions Section */}
        <section className="sessions-section">
          <div className="section-header">
            <h2 className="section-heading">Game Sessions</h2>

            {/* Tab Navigation - Playful chips */}
            <div className="tab-navigation">
              {(['recent', 'favorites', 'all'] as const).map(tab => (
                <button
                  key={tab}
                  className={`tab-chip ${activeTab === tab ? 'active' : ''}`}
                  onClick={() => setActiveTab(tab)}
                >
                  {tab.charAt(0).toUpperCase() + tab.slice(1)}
                </button>
              ))}
            </div>
          </div>

          {/* Sessions Grid - Card-based layout with hover effects */}
          <div className="sessions-grid">
            {mockGameSessions.map((session, index) => (
              <article
                key={session.id}
                className={`session-card session-${session.status}`}
                style={{ '--card-index': index } as React.CSSProperties}
              >
                {/* Game Cover */}
                <div className="session-cover">
                  <span className="cover-emoji">{session.cover}</span>
                  <div className={`status-badge status-${session.status}`}>{session.status}</div>
                </div>

                {/* Game Info */}
                <div className="session-info">
                  <h3 className="session-title">{session.gameName}</h3>

                  <div className="session-meta">
                    <span className="meta-item">👥 {session.players} players</span>
                    <span className="meta-item">⏱️ {session.duration}</span>
                  </div>

                  <p className="session-timestamp">Last played: {session.lastPlayed}</p>
                </div>

                {/* Action Buttons */}
                <div className="session-actions">
                  {session.status === 'active' && (
                    <button className="btn btn-primary">Resume Game</button>
                  )}
                  {session.status === 'paused' && (
                    <button className="btn btn-secondary">Continue</button>
                  )}
                  {session.status === 'completed' && (
                    <button className="btn btn-ghost">View Stats</button>
                  )}
                </div>
              </article>
            ))}
          </div>
        </section>

        <style jsx>{`
          .dashboard-redesign {
            max-width: 1400px;
            margin: 0 auto;
          }

          /* ============================================
             HERO SECTION - Editorial Layout
             ============================================ */

          .hero-section {
            display: grid;
            grid-template-columns: 1.2fr 1fr;
            gap: var(--space-8);
            margin-bottom: var(--space-12);
            align-items: start;
          }

          .hero-content {
            padding: var(--space-6) 0;
          }

          .hero-title {
            font-family: var(--font-display);
            font-size: var(--font-size-4xl);
            line-height: var(--line-height-tight);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            margin-bottom: var(--space-4);
            letter-spacing: var(--letter-spacing-tight);
          }

          .text-gradient {
            background: linear-gradient(
              135deg,
              var(--color-primary-500),
              var(--color-secondary-500)
            );
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            display: inline-block;
            animation: gradient-shift 3s ease-in-out infinite;
          }

          @keyframes gradient-shift {
            0%,
            100% {
              filter: hue-rotate(0deg);
            }
            50% {
              filter: hue-rotate(10deg);
            }
          }

          .hero-subtitle {
            font-size: var(--font-size-xl);
            color: var(--text-secondary);
            margin-bottom: var(--space-8);
            font-weight: var(--font-weight-medium);
          }

          /* Stats Grid - Floating Cards */

          .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
            gap: var(--space-4);
          }

          .stat-card {
            position: relative;
            background: var(--bg-elevated);
            border: 1px solid var(--border-primary);
            border-radius: var(--radius-xl);
            padding: var(--space-5);
            overflow: hidden;
            transition: all var(--transition-base);
            animation: float-in 0.6s ease-out backwards;
            animation-delay: calc(var(--card-index, 0) * 100ms);
          }

          @keyframes float-in {
            from {
              opacity: 0;
              transform: translateY(20px);
            }
          }

          .stat-card:hover {
            transform: translateY(-4px) rotate(-1deg);
            box-shadow: var(--shadow-xl);
            border-color: var(--color-primary-300);
          }

          .stat-icon {
            font-size: 32px;
            margin-bottom: var(--space-3);
            display: inline-block;
            animation: bounce-in 0.8s cubic-bezier(0.68, -0.55, 0.265, 1.55) backwards;
            animation-delay: calc(var(--card-index, 0) * 100ms + 200ms);
          }

          @keyframes bounce-in {
            from {
              opacity: 0;
              transform: scale(0);
            }
          }

          .stat-label {
            font-size: var(--font-size-sm);
            color: var(--text-tertiary);
            margin-bottom: var(--space-1);
            font-weight: var(--font-weight-medium);
          }

          .stat-value {
            font-family: var(--font-display);
            font-size: var(--font-size-2xl);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            line-height: 1;
          }

          .stat-decoration {
            position: absolute;
            bottom: -20px;
            right: -20px;
            width: 80px;
            height: 80px;
            border-radius: var(--radius-full);
            opacity: 0.1;
          }

          .stat-card-primary .stat-decoration {
            background: var(--color-primary-500);
          }

          .stat-card-secondary .stat-decoration {
            background: var(--color-secondary-500);
          }

          .stat-card-accent .stat-decoration {
            background: var(--color-green);
          }

          /* ============================================
             QUICK ACTIONS - Asymmetric Grid
             ============================================ */

          .quick-actions {
            position: sticky;
            top: var(--space-8);
          }

          .section-heading {
            font-family: var(--font-display);
            font-size: var(--font-size-2xl);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            margin-bottom: var(--space-6);
            letter-spacing: var(--letter-spacing-tight);
          }

          .actions-grid {
            display: grid;
            gap: var(--space-3);
          }

          .action-card {
            display: flex;
            align-items: center;
            gap: var(--space-4);
            padding: var(--space-4);
            background: var(--bg-elevated);
            border: 1px solid var(--border-primary);
            border-radius: var(--radius-lg);
            text-decoration: none;
            transition: all var(--transition-base);
            position: relative;
            overflow: hidden;
            animation: slide-in 0.5s ease-out backwards;
            animation-delay: var(--action-delay, 0ms);
          }

          @keyframes slide-in {
            from {
              opacity: 0;
              transform: translateX(-20px);
            }
          }

          .action-card:hover {
            transform: translateX(8px);
            border-color: var(--action-color);
            box-shadow: var(--shadow-lg);
          }

          .action-card::before {
            content: '';
            position: absolute;
            left: 0;
            top: 0;
            bottom: 0;
            width: 4px;
            background: var(--action-color);
            transform: scaleY(0);
            transition: transform var(--transition-base);
          }

          .action-card:hover::before {
            transform: scaleY(1);
          }

          .action-icon {
            font-size: 28px;
            width: 48px;
            height: 48px;
            display: flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, var(--action-color), transparent);
            border-radius: var(--radius-md);
            flex-shrink: 0;
            transition: transform var(--transition-bounce);
          }

          .action-card:hover .action-icon {
            transform: scale(1.1) rotate(-5deg);
          }

          .action-content {
            flex: 1;
          }

          .action-label {
            font-size: var(--font-size-base);
            font-weight: var(--font-weight-semibold);
            color: var(--text-primary);
            margin-bottom: var(--space-1);
          }

          .action-description {
            font-size: var(--font-size-sm);
            color: var(--text-tertiary);
            margin: 0;
          }

          .action-arrow {
            font-size: 20px;
            color: var(--text-tertiary);
            transition: transform var(--transition-base);
          }

          .action-card:hover .action-arrow {
            transform: translateX(4px);
            color: var(--action-color);
          }

          /* ============================================
             SESSIONS SECTION
             ============================================ */

          .sessions-section {
            margin-top: var(--space-12);
          }

          .section-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: var(--space-6);
          }

          /* Tab Navigation - Playful Chips */

          .tab-navigation {
            display: flex;
            gap: var(--space-2);
            background: var(--bg-secondary);
            padding: var(--space-1);
            border-radius: var(--radius-full);
          }

          .tab-chip {
            padding: var(--space-2) var(--space-4);
            border: none;
            background: transparent;
            border-radius: var(--radius-full);
            font-size: var(--font-size-sm);
            font-weight: var(--font-weight-medium);
            color: var(--text-secondary);
            cursor: pointer;
            transition: all var(--transition-fast);
          }

          .tab-chip:hover {
            background: var(--bg-tertiary);
            color: var(--text-primary);
          }

          .tab-chip.active {
            background: var(--color-primary-500);
            color: var(--text-inverse);
            box-shadow: var(--shadow-md);
          }

          /* Sessions Grid - Card Layout */

          .sessions-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
            gap: var(--space-6);
          }

          .session-card {
            background: var(--bg-elevated);
            border: 1px solid var(--border-primary);
            border-radius: var(--radius-xl);
            overflow: hidden;
            transition: all var(--transition-base);
            animation: fade-in-up 0.6s ease-out backwards;
            animation-delay: calc(var(--card-index) * 100ms);
          }

          @keyframes fade-in-up {
            from {
              opacity: 0;
              transform: translateY(30px);
            }
          }

          .session-card:hover {
            transform: translateY(-8px);
            box-shadow: var(--shadow-xl);
            border-color: var(--color-primary-300);
          }

          /* Session Cover */

          .session-cover {
            position: relative;
            height: 160px;
            background: linear-gradient(
              135deg,
              var(--color-primary-200),
              var(--color-secondary-200)
            );
            display: flex;
            align-items: center;
            justify-content: center;
          }

          .session-active .session-cover {
            background: linear-gradient(135deg, var(--color-green), var(--color-blue));
          }

          .session-paused .session-cover {
            background: linear-gradient(135deg, var(--color-yellow), var(--color-secondary-500));
          }

          .session-completed .session-cover {
            background: linear-gradient(135deg, var(--color-neutral-300), var(--color-neutral-400));
          }

          .cover-emoji {
            font-size: 64px;
            filter: drop-shadow(0 4px 12px rgba(0, 0, 0, 0.2));
          }

          .status-badge {
            position: absolute;
            top: var(--space-3);
            right: var(--space-3);
            padding: var(--space-1) var(--space-3);
            background: var(--bg-elevated);
            border-radius: var(--radius-full);
            font-size: var(--font-size-xs);
            font-weight: var(--font-weight-semibold);
            text-transform: uppercase;
            letter-spacing: var(--letter-spacing-wide);
          }

          .status-active {
            color: var(--color-green);
            border: 1px solid var(--color-green);
          }

          .status-paused {
            color: var(--color-yellow);
            border: 1px solid var(--color-yellow);
          }

          .status-completed {
            color: var(--text-tertiary);
            border: 1px solid var(--border-secondary);
          }

          /* Session Info */

          .session-info {
            padding: var(--space-5);
          }

          .session-title {
            font-family: var(--font-display);
            font-size: var(--font-size-xl);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            margin-bottom: var(--space-3);
            line-height: var(--line-height-tight);
          }

          .session-meta {
            display: flex;
            gap: var(--space-4);
            margin-bottom: var(--space-3);
          }

          .meta-item {
            font-size: var(--font-size-sm);
            color: var(--text-secondary);
            display: flex;
            align-items: center;
            gap: var(--space-1);
          }

          .session-timestamp {
            font-size: var(--font-size-xs);
            color: var(--text-tertiary);
            margin: 0;
          }

          /* Session Actions */

          .session-actions {
            padding: 0 var(--space-5) var(--space-5);
          }

          .btn {
            width: 100%;
            padding: var(--space-3) var(--space-4);
            border-radius: var(--radius-lg);
            font-size: var(--font-size-base);
            font-weight: var(--font-weight-semibold);
            cursor: pointer;
            transition: all var(--transition-fast);
            border: none;
          }

          .btn-primary {
            background: var(--color-primary-500);
            color: var(--text-inverse);
          }

          .btn-primary:hover {
            background: var(--color-primary-600);
            transform: translateY(-2px);
            box-shadow: var(--shadow-lg);
          }

          .btn-secondary {
            background: var(--color-secondary-500);
            color: var(--text-inverse);
          }

          .btn-secondary:hover {
            background: var(--color-secondary-600);
            transform: translateY(-2px);
            box-shadow: var(--shadow-lg);
          }

          .btn-ghost {
            background: transparent;
            color: var(--text-secondary);
            border: 1px solid var(--border-primary);
          }

          .btn-ghost:hover {
            background: var(--bg-secondary);
            color: var(--text-primary);
            border-color: var(--border-secondary);
          }

          /* ============================================
             RESPONSIVE DESIGN
             ============================================ */

          @media (max-width: 1024px) {
            .hero-section {
              grid-template-columns: 1fr;
              gap: var(--space-6);
            }

            .quick-actions {
              position: static;
            }

            .actions-grid {
              grid-template-columns: repeat(2, 1fr);
            }
          }

          @media (max-width: 640px) {
            .stats-grid {
              grid-template-columns: 1fr;
            }

            .actions-grid {
              grid-template-columns: 1fr;
            }

            .sessions-grid {
              grid-template-columns: 1fr;
            }

            .section-header {
              flex-direction: column;
              align-items: flex-start;
              gap: var(--space-4);
            }
          }
        `}</style>
      </div>
    </AppShell>
  );
}
