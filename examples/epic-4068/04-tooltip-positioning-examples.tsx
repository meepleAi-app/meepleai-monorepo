/**
 * Example 4: Smart Tooltip Positioning
 * Epic #4068 - Issues #4186, #4180
 *
 * Demonstrates auto-flip tooltip positioning and accessibility features
 */

import React, { useState } from 'react';
import { SmartTooltip } from '@/components/ui/overlays/SmartTooltip';
import { AccessibleTooltip } from '@/components/ui/overlays/AccessibleTooltip';
import { Button } from '@/components/ui/button';
import { Info, HelpCircle, Settings } from 'lucide-react';

export function TooltipPositioningExample() {
  const [tooltipCount, setTooltipCount] = useState(0);

  return (
    <div className="min-h-screen p-8">
      <section className="mb-12">
        <h1 className="text-2xl font-bold mb-4">Smart Tooltip Positioning</h1>
        <p className="text-muted-foreground">
          Auto-flip positioning with viewport boundary detection and WCAG 2.1 AA accessibility
        </p>
      </section>

      <section className="mb-12">
        <h2 className="text-xl font-semibold mb-4">Viewport Edge Detection</h2>

        <div className="grid grid-cols-3 gap-8 min-h-[400px]">
          {/* Top-left corner */}
          <div>
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Top-Left</Button>}
              content={<p>Tooltip auto-flips to bottom/right when trigger near top-left edge</p>}
              open={true}
              preferredPlacement="bottom"
            />
          </div>

          {/* Top-center */}
          <div className="justify-self-center">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Top-Center</Button>}
              content={<p>Tooltip appears below (preferred placement)</p>}
              open={true}
              preferredPlacement="bottom"
            />
          </div>

          {/* Top-right corner */}
          <div className="justify-self-end">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Top-Right</Button>}
              content={<p>Tooltip auto-flips to bottom/left when trigger near top-right edge</p>}
              open={true}
              preferredPlacement="bottom"
            />
          </div>

          {/* Middle-left */}
          <div className="self-center">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Middle-Left</Button>}
              content={<p>Tooltip appears to the right when trigger on left edge</p>}
              open={true}
              preferredPlacement="right"
            />
          </div>

          {/* Center */}
          <div className="self-center justify-self-center">
            <SmartTooltip
              trigger={<Button><Info className="mr-2" />Center</Button>}
              content={<p>Tooltip can appear in any direction (most space available)</p>}
              open={true}
            />
          </div>

          {/* Middle-right */}
          <div className="self-center justify-self-end">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Middle-Right</Button>}
              content={<p>Tooltip appears to the left when trigger on right edge</p>}
              open={true}
              preferredPlacement="left"
            />
          </div>

          {/* Bottom-left */}
          <div className="self-end">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Bottom-Left</Button>}
              content={<p>Tooltip auto-flips to top/right when trigger near bottom-left edge</p>}
              open={true}
              preferredPlacement="top"
            />
          </div>

          {/* Bottom-center */}
          <div className="self-end justify-self-center">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Bottom-Center</Button>}
              content={<p>Tooltip appears above (flipped from preferred bottom)</p>}
              open={true}
              preferredPlacement="top"
            />
          </div>

          {/* Bottom-right */}
          <div className="self-end justify-self-end">
            <SmartTooltip
              trigger={<Button variant="outline"><Info className="mr-2" />Bottom-Right</Button>}
              content={<p>Tooltip auto-flips to top/left when trigger near bottom-right edge</p>}
              open={true}
              preferredPlacement="top"
            />
          </div>
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-xl font-semibold mb-4">Keyboard Navigation (WCAG 2.1 AA)</h2>

        <div className="space-y-4">
          <div className="p-4 border rounded">
            <p className="mb-4 text-sm">
              <strong>Instructions:</strong> Use keyboard only:
            </p>
            <ol className="list-decimal list-inside space-y-2 text-sm">
              <li>Press Tab to focus buttons below</li>
              <li>Press Enter or Space to show tooltip</li>
              <li>Press Escape to hide tooltip</li>
              <li>Focus should be visible (ring indicator)</li>
            </ol>
          </div>

          <div className="flex gap-4">
            <AccessibleTooltip
              trigger={<Button><HelpCircle className="mr-2" />Help</Button>}
              content={
                <div>
                  <p className="font-semibold mb-1">Keyboard Accessible</p>
                  <p className="text-sm">This tooltip supports full keyboard navigation.</p>
                </div>
              }
            />

            <AccessibleTooltip
              trigger={<Button variant="outline"><Settings className="mr-2" />Settings</Button>}
              content={
                <div>
                  <p className="font-semibold mb-1">Settings Info</p>
                  <ul className="text-sm space-y-1">
                    <li>• Tab to focus</li>
                    <li>• Enter to show</li>
                    <li>• Escape to hide</li>
                  </ul>
                </div>
              }
            />

            <AccessibleTooltip
              trigger={<Button variant="secondary"><Info className="mr-2" />Information</Button>}
              content={<p className="text-sm">Screen readers announce this content via aria-describedby</p>}
            />
          </div>
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-xl font-semibold mb-4">Mobile Touch Support</h2>

        <div className="p-4 border rounded bg-muted/50">
          <p className="mb-4">
            <strong>Mobile Behavior:</strong> On touch devices, tooltips show on tap (not hover).
            Tap outside or press X button to dismiss.
          </p>

          <div className="flex gap-4">
            <AccessibleTooltip
              trigger={<Button size="lg">Tap Me (Mobile)</Button>}
              content={
                <div>
                  <p className="font-semibold mb-1">Mobile Tooltip</p>
                  <p className="text-sm">On mobile, this shows on tap.</p>
                  <p className="text-sm">Tap outside or press X to close.</p>
                </div>
              }
            />
          </div>

          <p className="mt-4 text-xs text-muted-foreground">
            Touch detection: <code>'ontouchstart' in window || navigator.maxTouchPoints > 0</code>
          </p>
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-xl font-semibold mb-4">Large Tooltip Content</h2>

        <div className="flex gap-4">
          <SmartTooltip
            trigger={<Button>Small Content</Button>}
            content={<p>Short tooltip</p>}
            open={true}
          />

          <SmartTooltip
            trigger={<Button>Medium Content</Button>}
            content={
              <div className="max-w-xs">
                <h4 className="font-semibold mb-2">Feature Description</h4>
                <p className="text-sm">
                  This is a medium-sized tooltip with multiple paragraphs.
                  The positioning algorithm accounts for larger tooltips and adjusts placement accordingly.
                </p>
              </div>
            }
            open={true}
          />

          <SmartTooltip
            trigger={<Button>Large Content</Button>}
            content={
              <div className="max-w-md">
                <h4 className="font-semibold mb-2">Detailed Information</h4>
                <p className="text-sm mb-2">
                  This is a large tooltip with extensive content.
                  The smart positioning system ensures it stays within viewport bounds.
                </p>
                <ul className="text-xs space-y-1 list-disc list-inside">
                  <li>Feature A: Description of feature A</li>
                  <li>Feature B: Description of feature B</li>
                  <li>Feature C: Description of feature C</li>
                </ul>
                <div className="mt-2 pt-2 border-t">
                  <Button size="sm">Learn More</Button>
                </div>
              </div>
            }
            open={true}
          />
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-xl font-semibold mb-4">Dynamic Tooltip Content</h2>

        <div className="flex gap-4 items-start">
          <Button
            onClick={() => setTooltipCount(c => c + 1)}
            variant="outline"
          >
            Increment Counter ({tooltipCount})
          </Button>

          <SmartTooltip
            trigger={<Button>Show Count</Button>}
            content={
              <div>
                <p className="font-semibold mb-1">Current Count</p>
                <p className="text-2xl font-bold">{tooltipCount}</p>
                <p className="text-xs text-muted-foreground mt-1">
                  Tooltip repositions when content changes size
                </p>
              </div>
            }
            open={true}
          />
        </div>

        <p className="mt-4 text-sm text-muted-foreground">
          Click "Increment Counter" to change tooltip content size. Tooltip auto-repositions.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Performance Test</h2>

        <div className="p-4 border rounded">
          <p className="mb-4">
            <strong>Performance Target:</strong> Tooltip positioning must complete in &lt;16ms (60fps requirement)
          </p>

          <div className="flex gap-4 flex-wrap">
            {Array.from({ length: 20 }, (_, i) => (
              <SmartTooltip
                key={i}
                trigger={<Button size="sm" variant="ghost">Button {i + 1}</Button>}
                content={<p>Tooltip {i + 1}</p>}
              />
            ))}
          </div>

          <p className="mt-4 text-xs text-muted-foreground">
            Hover multiple tooltips quickly. All should position smoothly without lag.
          </p>

          <div className="mt-4 p-3 bg-muted rounded">
            <p className="text-xs font-mono">
              Performance measurement:<br />
              <code>
                const start = performance.now();<br />
                calculateOptimalPosition(trigger, tooltip);<br />
                const duration = performance.now() - start;<br />
                // Expected: duration &lt; 16ms
              </code>
            </p>
          </div>
        </div>
      </section>

      <section className="mt-12">
        <h2 className="text-xl font-semibold mb-4">Implementation Code</h2>

        <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { SmartTooltip } from '@/components/ui/overlays/SmartTooltip';

<SmartTooltip
  trigger={<button>Hover me</button>}
  content={<p>Tooltip content</p>}
  preferredPlacement="bottom"
  gap={12}
  detectCollisions={true}
/>

// Or use hook directly
import { useSmartTooltip } from '@/hooks/useSmartTooltip';

function MyTooltip() {
  const [isOpen, setIsOpen] = useState(false);
  const { position, triggerRef, tooltipRef } = useSmartTooltip({
    enabled: isOpen,
    preferredPlacement: 'bottom'
  });

  return (
    <>
      <button
        ref={triggerRef}
        onMouseEnter={() => setIsOpen(true)}
        onMouseLeave={() => setIsOpen(false)}
      >
        Trigger
      </button>

      {isOpen && position && (
        <div
          ref={tooltipRef}
          style={{
            position: 'absolute',
            top: position.top,
            left: position.left
          }}
        >
          Tooltip
        </div>
      )}
    </>
  );
}`}
        </pre>
      </section>
    </div>
  );
}

export default TooltipPositioningExample;
