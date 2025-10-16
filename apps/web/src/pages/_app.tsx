import type { AppProps } from 'next/app';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { ToastContainer } from '../components/Toast';
import { useToast } from '../hooks/useToast';
import '../styles/globals.css';

function AppContent({ Component, pageProps }: AppProps) {
  const { toasts, dismiss } = useToast();

  return (
    <>
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
