import { render, screen } from '@testing-library/react';
import { BookOpen, MessageCircle } from 'lucide-react';
import { QuickActionsRow } from '../QuickActionsRow';

describe('QuickActionsRow', () => {
  const actions = [
    { icon: <BookOpen data-testid="icon-lib" />, label: 'Libreria', href: '/library' },
    { icon: <MessageCircle />, label: 'Chat AI', href: '/chat' },
  ];

  it('renders all action links', () => {
    render(<QuickActionsRow actions={actions} />);
    expect(screen.getByRole('link', { name: /libreria/i })).toHaveAttribute('href', '/library');
    expect(screen.getByRole('link', { name: /chat ai/i })).toHaveAttribute('href', '/chat');
  });

  it('renders labels', () => {
    render(<QuickActionsRow actions={actions} />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Chat AI')).toBeInTheDocument();
  });
});
