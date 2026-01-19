/**
 * MeepleAI App Shell
 * Modern application layout with sidebar navigation
 * Asymmetric design with playful interactions
 */

'use client';

import React, { useState } from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { MeepleLogo } from '../ui/meeple/meeple-logo';

interface AppShellProps {
  children: React.ReactNode;
}

interface NavItem {
  label: string;
  href: string;
  icon: string;
  badge?: number;
  color?: string;
}

const navigationItems: NavItem[] = [
  {
    label: 'Dashboard',
    href: '/dashboard',
    icon: '🎲',
    color: 'var(--color-primary-500)',
  },
  { label: 'Ask AI', href: '/chat', icon: '💬', color: 'var(--color-blue)' },
  { label: 'My Games', href: '/games', icon: '🎮', color: 'var(--color-green)' },
  { label: 'Library', href: '/library', icon: '📚', color: 'var(--color-secondary-500)' },
  { label: 'Settings', href: '/settings', icon: '⚙️', color: 'var(--color-neutral-600)' },
];

const adminNavItems: NavItem[] = [
  { label: 'Admin', href: '/admin', icon: '👑', color: 'var(--color-red)' },
  { label: 'Analytics', href: '/admin/analytics', icon: '📊' },
  { label: 'Users', href: '/admin/users', icon: '👥' },
];

export function AppShell({ children }: AppShellProps) {
  const pathname = usePathname();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  const isActive = (href: string) => pathname === href;

  return (
    <div className="app-shell">
      {/* Sidebar Navigation */}
      <aside className={`sidebar ${sidebarCollapsed ? 'collapsed' : ''}`}>
        {/* Logo Header */}
        <div className="sidebar-header">
          <Link href="/dashboard">
            <MeepleLogo variant={sidebarCollapsed ? 'icon' : 'full'} size="md" animated />
          </Link>

          <button
            className="collapse-btn"
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            aria-label={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {sidebarCollapsed ? '→' : '←'}
          </button>
        </div>

        {/* Main Navigation */}
        <nav className="sidebar-nav">
          <ul className="nav-list">
            {navigationItems.map(item => (
              <li key={item.href}>
                <Link
                  href={item.href}
                  className={`nav-item ${isActive(item.href) ? 'active' : ''}`}
                  style={{ '--item-color': item.color } as React.CSSProperties}
                >
                  <span className="nav-icon">{item.icon}</span>
                  {!sidebarCollapsed && (
                    <>
                      <span className="nav-label">{item.label}</span>
                      {item.badge && <span className="nav-badge">{item.badge}</span>}
                    </>
                  )}
                </Link>
              </li>
            ))}
          </ul>

          {/* Admin Section */}
          <div className="nav-section">
            {!sidebarCollapsed && <p className="section-label">Administration</p>}
            <ul className="nav-list">
              {adminNavItems.map(item => (
                <li key={item.href}>
                  <Link
                    href={item.href}
                    className={`nav-item ${isActive(item.href) ? 'active' : ''}`}
                    style={{ '--item-color': item.color } as React.CSSProperties}
                  >
                    <span className="nav-icon">{item.icon}</span>
                    {!sidebarCollapsed && <span className="nav-label">{item.label}</span>}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </nav>

        {/* User Profile */}
        <div className="sidebar-footer">
          <button className="user-profile" onClick={() => setUserMenuOpen(!userMenuOpen)}>
            <div className="user-avatar">
              <span>U</span>
            </div>
            {!sidebarCollapsed && (
              <div className="user-info">
                <p className="user-name">User Name</p>
                <p className="user-role">Player</p>
              </div>
            )}
          </button>

          {userMenuOpen && (
            <div className="user-menu">
              <Link href="/profile">Profile</Link>
              <Link href="/settings">Settings</Link>
              <hr />
              <button>Logout</button>
            </div>
          )}
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="main-content">{children}</main>

      <style jsx>{`
        .app-shell {
          display: grid;
          grid-template-columns: 280px 1fr;
          min-height: 100vh;
          background: var(--bg-primary);
          font-family: var(--font-body);
        }

        /* ============================================
           SIDEBAR STYLES
           ============================================ */

        .sidebar {
          position: fixed;
          left: 0;
          top: 0;
          bottom: 0;
          width: 280px;
          display: flex;
          flex-direction: column;
          background: var(--bg-elevated);
          border-right: 1px solid var(--border-primary);
          padding: var(--space-6) var(--space-4);
          transition: width var(--transition-base);
          z-index: var(--z-fixed);
        }

        .sidebar.collapsed {
          width: 80px;
        }

        .sidebar-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          margin-bottom: var(--space-8);
          padding-bottom: var(--space-4);
          border-bottom: 1px solid var(--border-primary);
        }

        .collapse-btn {
          background: none;
          border: 1px solid var(--border-primary);
          border-radius: var(--radius-md);
          width: 32px;
          height: 32px;
          display: flex;
          align-items: center;
          justify-content: center;
          cursor: pointer;
          color: var(--text-secondary);
          transition: all var(--transition-fast);
          font-size: 16px;
        }

        .collapse-btn:hover {
          background: var(--bg-secondary);
          color: var(--text-primary);
          transform: scale(1.1);
        }

        .sidebar.collapsed .collapse-btn {
          margin: 0 auto;
        }

        /* ============================================
           NAVIGATION STYLES
           ============================================ */

        .sidebar-nav {
          flex: 1;
          overflow-y: auto;
        }

        .nav-list {
          list-style: none;
          padding: 0;
          margin: 0;
        }

        .nav-item {
          display: flex;
          align-items: center;
          gap: var(--space-3);
          padding: var(--space-3) var(--space-4);
          border-radius: var(--radius-lg);
          color: var(--text-secondary);
          text-decoration: none;
          font-weight: var(--font-weight-medium);
          transition: all var(--transition-fast);
          position: relative;
          overflow: hidden;
        }

        .nav-item:hover {
          background: var(--bg-secondary);
          color: var(--text-primary);
          transform: translateX(4px);
        }

        .nav-item.active {
          background: linear-gradient(
            135deg,
            var(--item-color, var(--color-primary-500)),
            transparent
          );
          color: var(--text-primary);
          font-weight: var(--font-weight-semibold);
          box-shadow: var(--shadow-md);
        }

        .nav-item.active::before {
          content: '';
          position: absolute;
          left: 0;
          top: 0;
          bottom: 0;
          width: 4px;
          background: var(--item-color, var(--color-primary-500));
          border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
        }

        .sidebar.collapsed .nav-item {
          justify-content: center;
          padding: var(--space-3);
        }

        .nav-icon {
          font-size: 20px;
          flex-shrink: 0;
          transition: transform var(--transition-bounce);
        }

        .nav-item:hover .nav-icon {
          transform: scale(1.2) rotate(5deg);
        }

        .nav-label {
          flex: 1;
          white-space: nowrap;
        }

        .nav-badge {
          background: var(--color-primary-500);
          color: var(--text-inverse);
          padding: 2px 8px;
          border-radius: var(--radius-full);
          font-size: var(--font-size-xs);
          font-weight: var(--font-weight-bold);
        }

        .nav-section {
          margin-top: var(--space-6);
          padding-top: var(--space-6);
          border-top: 1px solid var(--border-primary);
        }

        .section-label {
          font-size: var(--font-size-xs);
          font-weight: var(--font-weight-semibold);
          text-transform: uppercase;
          letter-spacing: var(--letter-spacing-wider);
          color: var(--text-tertiary);
          margin-bottom: var(--space-3);
          padding: 0 var(--space-4);
        }

        /* ============================================
           USER PROFILE
           ============================================ */

        .sidebar-footer {
          margin-top: auto;
          padding-top: var(--space-4);
          border-top: 1px solid var(--border-primary);
          position: relative;
        }

        .user-profile {
          display: flex;
          align-items: center;
          gap: var(--space-3);
          width: 100%;
          padding: var(--space-3);
          background: none;
          border: 1px solid var(--border-primary);
          border-radius: var(--radius-lg);
          cursor: pointer;
          transition: all var(--transition-fast);
        }

        .user-profile:hover {
          background: var(--bg-secondary);
          box-shadow: var(--shadow-md);
        }

        .sidebar.collapsed .user-profile {
          justify-content: center;
          padding: var(--space-3);
        }

        .user-avatar {
          width: 40px;
          height: 40px;
          border-radius: var(--radius-full);
          background: linear-gradient(135deg, var(--color-primary-500), var(--color-secondary-500));
          display: flex;
          align-items: center;
          justify-content: center;
          color: var(--text-inverse);
          font-weight: var(--font-weight-bold);
          flex-shrink: 0;
        }

        .user-info {
          flex: 1;
          text-align: left;
        }

        .user-name {
          font-weight: var(--font-weight-semibold);
          color: var(--text-primary);
          margin: 0;
          font-size: var(--font-size-sm);
        }

        .user-role {
          font-size: var(--font-size-xs);
          color: var(--text-tertiary);
          margin: 0;
        }

        .user-menu {
          position: absolute;
          bottom: 100%;
          left: 0;
          right: 0;
          background: var(--bg-elevated);
          border: 1px solid var(--border-primary);
          border-radius: var(--radius-lg);
          box-shadow: var(--shadow-xl);
          padding: var(--space-2);
          margin-bottom: var(--space-2);
          display: flex;
          flex-direction: column;
          gap: var(--space-1);
        }

        .user-menu a,
        .user-menu button {
          padding: var(--space-2) var(--space-3);
          border-radius: var(--radius-md);
          text-decoration: none;
          color: var(--text-secondary);
          background: none;
          border: none;
          cursor: pointer;
          text-align: left;
          transition: all var(--transition-fast);
        }

        .user-menu a:hover,
        .user-menu button:hover {
          background: var(--bg-secondary);
          color: var(--text-primary);
        }

        .user-menu hr {
          border: none;
          border-top: 1px solid var(--border-primary);
          margin: var(--space-2) 0;
        }

        /* ============================================
           MAIN CONTENT AREA
           ============================================ */

        .main-content {
          margin-left: 280px;
          padding: var(--space-8);
          min-height: 100vh;
          transition: margin-left var(--transition-base);
        }

        .sidebar.collapsed ~ .main-content {
          margin-left: 80px;
        }

        /* ============================================
           RESPONSIVE DESIGN
           ============================================ */

        @media (max-width: 1024px) {
          .app-shell {
            grid-template-columns: 1fr;
          }

          .sidebar {
            transform: translateX(-100%);
          }

          .sidebar.open {
            transform: translateX(0);
          }

          .main-content {
            margin-left: 0;
          }
        }

        /* ============================================
           CUSTOM SCROLLBAR
           ============================================ */

        .sidebar-nav::-webkit-scrollbar {
          width: 6px;
        }

        .sidebar-nav::-webkit-scrollbar-track {
          background: transparent;
        }

        .sidebar-nav::-webkit-scrollbar-thumb {
          background: var(--border-secondary);
          border-radius: var(--radius-full);
        }

        .sidebar-nav::-webkit-scrollbar-thumb:hover {
          background: var(--text-tertiary);
        }
      `}</style>
    </div>
  );
}
