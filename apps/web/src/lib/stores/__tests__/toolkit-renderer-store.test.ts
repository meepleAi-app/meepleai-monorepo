import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useToolkitRendererStore } from '../toolkit-renderer-store';

describe('useToolkitRendererStore', () => {
  beforeEach(() => {
    useToolkitRendererStore.getState().reset();
    vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('hydrate parses backend widgets into typed config', () => {
    useToolkitRendererStore.getState().hydrate({
      widgets: [
        {
          id: 'w1',
          type: 'NoteManager',
          isEnabled: true,
          displayOrder: 0,
          config: JSON.stringify({ text: 'hello' }),
        },
      ],
    });
    const { widgets } = useToolkitRendererStore.getState();
    expect(widgets.length).toBe(1);
    expect(widgets[0]?.type).toBe('NoteManager');
    if (widgets[0]?.type === 'NoteManager') {
      expect(widgets[0].config.text).toBe('hello');
    }
  });

  it('setOpenWidget updates openWidgetId', () => {
    useToolkitRendererStore.getState().setOpenWidget('w1');
    expect(useToolkitRendererStore.getState().openWidgetId).toBe('w1');
    useToolkitRendererStore.getState().setOpenWidget(null);
    expect(useToolkitRendererStore.getState().openWidgetId).toBeNull();
  });

  it('updateWidgetConfig optimistically updates + PATCH success keeps it', async () => {
    useToolkitRendererStore.getState().hydrate({
      widgets: [
        {
          id: 'w1',
          type: 'NoteManager',
          isEnabled: true,
          displayOrder: 0,
          config: JSON.stringify({ text: '' }),
        },
      ],
    });
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
    } as Response);

    await useToolkitRendererStore.getState().updateWidgetConfig('w1', { text: 'updated' });

    const { widgets } = useToolkitRendererStore.getState();
    if (widgets[0]?.type === 'NoteManager') {
      expect(widgets[0].config.text).toBe('updated');
    }
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/toolkits/widgets/w1',
      expect.objectContaining({ method: 'PATCH' })
    );

    fetchMock.mockRestore();
  });

  it('updateWidgetConfig rolls back on PATCH failure', async () => {
    useToolkitRendererStore.getState().hydrate({
      widgets: [
        {
          id: 'w1',
          type: 'NoteManager',
          isEnabled: true,
          displayOrder: 0,
          config: JSON.stringify({ text: 'original' }),
        },
      ],
    });
    vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: false,
      status: 500,
    } as Response);

    await useToolkitRendererStore.getState().updateWidgetConfig('w1', { text: 'changed' });

    const { widgets } = useToolkitRendererStore.getState();
    if (widgets[0]?.type === 'NoteManager') {
      expect(widgets[0].config.text).toBe('original'); // rolled back
    }
  });

  it('updateWidgetConfig handles network error with rollback', async () => {
    useToolkitRendererStore.getState().hydrate({
      widgets: [
        {
          id: 'w1',
          type: 'NoteManager',
          isEnabled: true,
          displayOrder: 0,
          config: JSON.stringify({ text: 'original' }),
        },
      ],
    });
    vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Network fail'));

    await useToolkitRendererStore.getState().updateWidgetConfig('w1', { text: 'changed' });

    const { widgets } = useToolkitRendererStore.getState();
    if (widgets[0]?.type === 'NoteManager') {
      expect(widgets[0].config.text).toBe('original'); // rolled back
    }
  });
});
