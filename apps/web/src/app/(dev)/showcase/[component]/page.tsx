'use client';

/**
 * Showcase Component View
 *
 * Individual component page with:
 * - 3-panel layout (sidebar / canvas / controls)
 * - Live prop editing via controls panel
 * - Preset selector
 * - Background and zoom controls
 */

import { use, useState, useCallback } from 'react';

import { notFound } from 'next/navigation';

import { ShowcaseCanvas } from '@/components/showcase/showcase-canvas';
import { ShowcaseControls } from '@/components/showcase/showcase-controls';
import { ShowcaseLayout } from '@/components/showcase/showcase-layout';
import { ShowcaseSidebar } from '@/components/showcase/showcase-sidebar';
import { ShowcaseStoryPicker } from '@/components/showcase/showcase-story-picker';
import { ALL_STORIES, STORY_MAP } from '@/components/showcase/stories';
import type { PropsState } from '@/components/showcase/types';

type BgMode = 'light' | 'dark' | 'grid';

interface PageProps {
  params: Promise<{ component: string }>;
}

export default function ShowcaseComponentPage({ params }: PageProps) {
  const { component: slug } = use(params);
  const story = STORY_MAP[slug];

  if (!story) notFound();

  // Build initial props state from story defaultProps

  const [propsState, setPropsState] = useState<PropsState>(() => {
    const state: PropsState = {};
    for (const [key, value] of Object.entries(story.defaultProps as Record<string, unknown>)) {
      if (typeof value === 'string' || typeof value === 'boolean' || typeof value === 'number') {
        state[key] = value;
      }
    }
    return state;
  });

  const [bgMode, setBgMode] = useState<BgMode>('light');

  const [zoom, setZoom] = useState(1);

  const [activePreset, setActivePreset] = useState<string>(() => {
    const presets = story.presets ?? {};
    return Object.keys(presets)[0] ?? '';
  });

  const handleControlChange = useCallback((key: string, value: string | boolean | number) => {
    setPropsState(prev => ({ ...prev, [key]: value }));
    setActivePreset(''); // clear preset when user manually changes
  }, []);

  const handlePresetChange = useCallback(
    (presetKey: string) => {
      const preset = story.presets?.[presetKey];
      if (!preset) return;
      setActivePreset(presetKey);
      setPropsState(prev => {
        const next = { ...prev };
        for (const [k, v] of Object.entries(preset.props as Record<string, unknown>)) {
          if (typeof v === 'string' || typeof v === 'boolean' || typeof v === 'number') {
            next[k] = v;
          }
        }
        return next;
      });
    },
    [story.presets]
  );

  // Merge propsState with defaultProps (for non-primitive props)
  const mergedProps = {
    ...story.defaultProps,
    ...propsState,
  };

  const Component = story.component as React.ComponentType<Record<string, unknown>>;
  const Decorator = story.decorator;

  const rendered = Decorator ? (
    <Decorator>
      <Component {...mergedProps} />
    </Decorator>
  ) : (
    <Component {...mergedProps} />
  );

  const presetLabels = Object.fromEntries(
    Object.entries(story.presets ?? {}).map(([k, v]) => [k, { label: v.label }])
  );

  return (
    <ShowcaseLayout
      sidebar={<ShowcaseSidebar stories={ALL_STORIES} />}
      canvas={
        <ShowcaseCanvas
          bgMode={bgMode}
          zoom={zoom}
          onBgChange={setBgMode}
          onZoomChange={setZoom}
          title={story.title}
          description={story.description}
          presetPicker={
            Object.keys(presetLabels).length > 0 ? (
              <ShowcaseStoryPicker
                presets={presetLabels}
                activePreset={activePreset}
                onChange={handlePresetChange}
              />
            ) : undefined
          }
        >
          {rendered}
        </ShowcaseCanvas>
      }
      controls={
        <ShowcaseControls
          controls={story.controls}
          values={propsState}
          onChange={handleControlChange}
        />
      }
    />
  );
}
