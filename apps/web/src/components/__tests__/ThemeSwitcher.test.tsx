import type { Mock } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeSwitcher } from '../ThemeSwitcher';
import { useTheme } from 'next-themes';

vi.mock('next-themes', () => ({
  useTheme: vi.fn(),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>
}));

const mockUseTheme = useTheme as Mock<typeof useTheme>;

describe('ThemeSwitcher', () => {
  const mockSetTheme = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state before mounting', () => {
    mockUseTheme.mockReturnValue({
      theme: 'light',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    const { container } = render(<ThemeSwitcher />);

    // Should show loading skeleton initially
    const skeleton = container.querySelector('[aria-hidden="true"]');
    expect(skeleton).toBeInTheDocument();
  });

  it('renders light theme icon and label when theme is light', async () => {
    mockUseTheme.mockReturnValue({
      theme: 'light',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    // Should show Sun icon and "Light" label
    expect(screen.getByText('Light')).toBeInTheDocument();
  });

  it('renders dark theme icon and label when theme is dark', async () => {
    mockUseTheme.mockReturnValue({
      theme: 'dark',
      setTheme: mockSetTheme,
      systemTheme: 'dark',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'dark',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    // Should show Moon icon and "Dark" label
    expect(screen.getByText('Dark')).toBeInTheDocument();
  });

  it('renders system theme icon and label when theme is system', async () => {
    mockUseTheme.mockReturnValue({
      theme: 'system',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    // Should show Monitor icon and "System" label
    expect(screen.getByText('System')).toBeInTheDocument();
  });

  it('resolves system theme to dark when systemTheme is dark', async () => {
    mockUseTheme.mockReturnValue({
      theme: 'system',
      setTheme: mockSetTheme,
      systemTheme: 'dark',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'dark',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    // Should still show "System" label
    expect(screen.getByText('System')).toBeInTheDocument();
  });

  it('switches to light theme when light option is clicked', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'dark',
      setTheme: mockSetTheme,
      systemTheme: 'dark',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'dark',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    const lightOption = screen.getByLabelText('Switch to light theme');
    await user.click(lightOption);

    expect(mockSetTheme).toHaveBeenCalledWith('light');
  });

  it('switches to dark theme when dark option is clicked', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'light',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    const darkOption = screen.getByLabelText('Switch to dark theme');
    await user.click(darkOption);

    expect(mockSetTheme).toHaveBeenCalledWith('dark');
  });

  it('switches to system theme when system option is clicked', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'light',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    const systemOption = screen.getByLabelText('Use system theme preference');
    await user.click(systemOption);

    expect(mockSetTheme).toHaveBeenCalledWith('system');
  });

  it('shows checkmark on current theme option when light is selected', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'light',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    // Should show checkmark on light theme
    const checkmarks = screen.getAllByLabelText('Current theme');
    expect(checkmarks).toHaveLength(1);
  });

  it('shows checkmark on current theme option when dark is selected', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'dark',
      setTheme: mockSetTheme,
      systemTheme: 'dark',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'dark',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    // Should show checkmark on dark theme
    const checkmarks = screen.getAllByLabelText('Current theme');
    expect(checkmarks).toHaveLength(1);
  });

  it('shows checkmark on current theme option when system is selected', async () => {
    const user = userEvent.setup();

    mockUseTheme.mockReturnValue({
      theme: 'system',
      setTheme: mockSetTheme,
      systemTheme: 'light',
      themes: ['light', 'dark', 'system'],
      resolvedTheme: 'light',
      forcedTheme: undefined
    });

    render(<ThemeSwitcher />);

    await waitFor(() => {
      expect(screen.getByLabelText('Theme switcher')).toBeInTheDocument();
    });

    const trigger = screen.getByLabelText('Theme switcher');
    await user.click(trigger);

    // Should show checkmark on system theme
    const checkmarks = screen.getAllByLabelText('Current theme');
    expect(checkmarks).toHaveLength(1);
  });
});
