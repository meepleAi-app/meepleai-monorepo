import type { AppProps } from 'next/app';
import Head from 'next/head';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { ToastContainer } from '../components/Toast';
import { useToast } from '../hooks/useToast';
import { AccessibleSkipLink } from '@/components/accessible';
import '../styles/globals.css';

// Enable axe-core accessibility checks in development (UI-05)
if (typeof window !== 'undefined' && process.env.NODE_ENV === 'development') {
  // @ts-ignore - dynamic import
  import('@axe-core/react').then((axe) => {
    const React = require('react');
    const ReactDOM = require('react-dom');
    axe.default(React, ReactDOM, 1000);
  }).catch(() => {
    // Silently fail if axe-core fails to load
  });
}

function AppContent({ Component, pageProps }: AppProps) {
  const { toasts, dismiss } = useToast();

  return (
    <>
      {/* Default document title (UI-05 WCAG 2.1 AA requirement) */}
      <Head>
        <title>MeepleAI - AI-Powered Board Game Rules Assistant</title>
      </Head>
      {/* Skip to main content link (UI-05 WCAG 2.1 AA requirement) */}
      <AccessibleSkipLink href="#main-content" />
      <Component {...pageProps} />
      <ToastContainer toasts={toasts} onDismiss={dismiss} position="top-right" />
    </>
  );
}

export default function App(props: AppProps) {
  return (
    <ErrorBoundary
      componentName="App"
      showDetails={process.env.NODE_ENV === 'development'}
    >
      <AppContent {...props} />
    </ErrorBoundary>
  );
}
