/**
 * Unit tests for ToolRail camera integration (Issue #5371)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ToolRail, BASE_TOOLS, type ToolItem, type ToolRailProps } from '../ToolRail';

describe('ToolRail - Camera integration', () => {
  afterEach(() => {
    cleanup();
  });

  const defaultProps: ToolRailProps = {
    tools: BASE_TOOLS,
    activeTool: 'scoreboard',
    onToolChange: vi.fn(),
  };

  // ================================================================
  // Camera tool in BASE_TOOLS
  // ================================================================

  it('includes camera tool in BASE_TOOLS', () => {
    const cameraTool = BASE_TOOLS.find(t => t.id === 'camera');
    expect(cameraTool).toBeDefined();
    expect(cameraTool!.label).toBe('Foto');
    expect(cameraTool!.shortLabel).toBe('Foto');
    expect(cameraTool!.type).toBe('base');
  });

  it('renders camera tool button', () => {
    render(<ToolRail {...defaultProps} />);
    const cameraButton = screen.getAllByRole('tab', { name: 'Foto' });
    expect(cameraButton.length).toBeGreaterThanOrEqual(1);
  });

  it('calls onToolChange with "camera" when camera tool clicked', async () => {
    const user = userEvent.setup();
    const onToolChange = vi.fn();
    render(<ToolRail {...defaultProps} onToolChange={onToolChange} />);

    const cameraButtons = screen.getAllByRole('tab', { name: 'Foto' });
    await user.click(cameraButtons[0]);

    expect(onToolChange).toHaveBeenCalledWith('camera');
  });

  it('shows camera tool as active when activeTool is camera', () => {
    render(<ToolRail {...defaultProps} activeTool="camera" />);
    const cameraButtons = screen.getAllByRole('tab', { name: 'Foto' });
    expect(cameraButtons[0]).toHaveAttribute('aria-selected', 'true');
  });

  // ================================================================
  // Badge support
  // ================================================================

  it('shows badge when tool has badge > 0', () => {
    const toolsWithBadge: ToolItem[] = BASE_TOOLS.map(t =>
      t.id === 'camera' ? { ...t, badge: 5 } : t
    );
    render(<ToolRail {...defaultProps} tools={toolsWithBadge} />);

    const badges = screen.getAllByTestId('tool-badge-camera');
    expect(badges.length).toBeGreaterThanOrEqual(1);
    expect(badges[0]).toHaveTextContent('5');
  });

  it('does not show badge when badge is 0', () => {
    const toolsWithBadge: ToolItem[] = BASE_TOOLS.map(t =>
      t.id === 'camera' ? { ...t, badge: 0 } : t
    );
    render(<ToolRail {...defaultProps} tools={toolsWithBadge} />);

    expect(screen.queryByTestId('tool-badge-camera')).not.toBeInTheDocument();
  });

  it('does not show badge when badge is undefined', () => {
    render(<ToolRail {...defaultProps} />);
    expect(screen.queryByTestId('tool-badge-camera')).not.toBeInTheDocument();
  });

  it('shows 99+ for badge values over 99', () => {
    const toolsWithBadge: ToolItem[] = BASE_TOOLS.map(t =>
      t.id === 'camera' ? { ...t, badge: 150 } : t
    );
    render(<ToolRail {...defaultProps} tools={toolsWithBadge} />);

    const badges = screen.getAllByTestId('tool-badge-camera');
    expect(badges[0]).toHaveTextContent('99+');
  });

  // ================================================================
  // Tool count
  // ================================================================

  it('has 5 base tools including camera', () => {
    expect(BASE_TOOLS).toHaveLength(5);
    expect(BASE_TOOLS.map(t => t.id)).toEqual([
      'scoreboard',
      'turn-order',
      'dice',
      'whiteboard',
      'camera',
    ]);
  });
});
