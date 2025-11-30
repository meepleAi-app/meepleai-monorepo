/**
 * Component Showcase Page
 * Demonstrates all redesigned components with live examples
 * Useful for design review and development reference
 */

'use client';

import React, { useState } from 'react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button-redesign';
import { Input } from '@/components/ui/input-redesign';
import { Textarea } from '@/components/ui/textarea-redesign';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select-redesign';

export default function ComponentsShowcase() {
  const [inputValue, setInputValue] = useState('');
  const [textareaValue, setTextareaValue] = useState('');
  const [selectValue, setSelectValue] = useState('');

  return (
    <AppShell>
      <div className="components-showcase max-w-6xl">
        {/* Header */}
        <header className="mb-12">
          <h1 className="hero-title mb-4">
            Component <span className="text-gradient">Showcase</span>
          </h1>
          <p className="text-[var(--text-secondary)] text-[var(--font-size-lg)]">
            Design System 2.0 - Editorial Playful Components
          </p>
        </header>

        {/* Buttons Section */}
        <section className="component-section">
          <h2 className="section-title">Buttons</h2>
          <p className="section-description">
            Interactive buttons with playful hover effects and multiple variants
          </p>

          <div className="examples-grid">
            {/* Variants */}
            <div className="example-group">
              <h3 className="example-title">Variants</h3>
              <div className="flex flex-wrap gap-3">
                <Button variant="primary">Primary</Button>
                <Button variant="secondary">Secondary</Button>
                <Button variant="outline">Outline</Button>
                <Button variant="ghost">Ghost</Button>
                <Button variant="danger">Danger</Button>
                <Button variant="link">Link</Button>
              </div>
            </div>

            {/* Sizes */}
            <div className="example-group">
              <h3 className="example-title">Sizes</h3>
              <div className="flex flex-wrap items-center gap-3">
                <Button size="sm">Small</Button>
                <Button size="md">Medium</Button>
                <Button size="lg">Large</Button>
                <Button size="xl">Extra Large</Button>
              </div>
            </div>

            {/* With Icons */}
            <div className="example-group">
              <h3 className="example-title">With Icons</h3>
              <div className="flex flex-wrap gap-3">
                <Button leftIcon="🎲">New Game</Button>
                <Button variant="secondary" rightIcon="→">
                  Continue
                </Button>
                <Button variant="outline" leftIcon="📚" rightIcon="→">
                  Browse Library
                </Button>
              </div>
            </div>

            {/* States */}
            <div className="example-group">
              <h3 className="example-title">States</h3>
              <div className="flex flex-wrap gap-3">
                <Button loading>Loading</Button>
                <Button disabled>Disabled</Button>
                <Button playful={false}>No Playful</Button>
              </div>
            </div>
          </div>
        </section>

        {/* Inputs Section */}
        <section className="component-section">
          <h2 className="section-title">Inputs</h2>
          <p className="section-description">
            Text inputs with smooth focus transitions and validation states
          </p>

          <div className="examples-grid">
            {/* Variants */}
            <div className="example-group">
              <h3 className="example-title">Variants</h3>
              <div className="space-y-4">
                <Input variant="default" placeholder="Default variant" />
                <Input variant="filled" placeholder="Filled variant" />
                <Input variant="ghost" placeholder="Ghost variant" />
              </div>
            </div>

            {/* With Icons */}
            <div className="example-group">
              <h3 className="example-title">With Icons</h3>
              <div className="space-y-4">
                <Input
                  leftIcon="🔍"
                  placeholder="Search games..."
                  value={inputValue}
                  onChange={e => setInputValue(e.target.value)}
                />
                <Input variant="filled" rightIcon="@" placeholder="Email address" />
                <Input leftIcon="🎲" rightIcon="→" placeholder="Game name" />
              </div>
            </div>

            {/* States */}
            <div className="example-group">
              <h3 className="example-title">Validation States</h3>
              <div className="space-y-4">
                <Input
                  state="error"
                  placeholder="Email"
                  error="Please enter a valid email address"
                />
                <Input state="success" placeholder="Username" helperText="Username is available" />
                <Input
                  state="warning"
                  placeholder="Password"
                  helperText="Password strength: Medium"
                />
              </div>
            </div>

            {/* Sizes */}
            <div className="example-group">
              <h3 className="example-title">Sizes</h3>
              <div className="space-y-4">
                <Input inputSize="sm" placeholder="Small input" />
                <Input inputSize="md" placeholder="Medium input (default)" />
                <Input inputSize="lg" placeholder="Large input" />
              </div>
            </div>
          </div>
        </section>

        {/* Textarea Section */}
        <section className="component-section">
          <h2 className="section-title">Textarea</h2>
          <p className="section-description">Multi-line text input with auto-resize option</p>

          <div className="examples-grid">
            {/* Basic */}
            <div className="example-group">
              <h3 className="example-title">Basic</h3>
              <Textarea placeholder="Enter game rules..." rows={4} />
            </div>

            {/* Auto-resize */}
            <div className="example-group">
              <h3 className="example-title">Auto-resize</h3>
              <Textarea
                autoResize
                placeholder="Type to see auto-resize..."
                value={textareaValue}
                onChange={e => setTextareaValue(e.target.value)}
                helperText="Textarea will expand as you type"
              />
            </div>

            {/* With Error */}
            <div className="example-group">
              <h3 className="example-title">With Error</h3>
              <Textarea
                variant="filled"
                placeholder="Description"
                error="Description must be at least 10 characters"
                rows={3}
              />
            </div>
          </div>
        </section>

        {/* Select Section */}
        <section className="component-section">
          <h2 className="section-title">Select</h2>
          <p className="section-description">
            Dropdown select with smooth animations and custom styling
          </p>

          <div className="examples-grid">
            {/* Basic */}
            <div className="example-group">
              <h3 className="example-title">Basic Select</h3>
              <Select value={selectValue} onValueChange={setSelectValue}>
                <SelectTrigger variant="default" size="md">
                  <SelectValue placeholder="Select a game" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="terraforming">Terraforming Mars</SelectItem>
                  <SelectItem value="wingspan">Wingspan</SelectItem>
                  <SelectItem value="gloomhaven">Gloomhaven</SelectItem>
                  <SelectItem value="ticket">Ticket to Ride</SelectItem>
                  <SelectItem value="catan">Catan</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Filled Variant */}
            <div className="example-group">
              <h3 className="example-title">Filled Variant</h3>
              <Select>
                <SelectTrigger variant="filled" size="md">
                  <SelectValue placeholder="Player count" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">1 Player</SelectItem>
                  <SelectItem value="2">2 Players</SelectItem>
                  <SelectItem value="3">3 Players</SelectItem>
                  <SelectItem value="4">4 Players</SelectItem>
                  <SelectItem value="5+">5+ Players</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Sizes */}
            <div className="example-group">
              <h3 className="example-title">Sizes</h3>
              <div className="space-y-4">
                <Select>
                  <SelectTrigger size="sm">
                    <SelectValue placeholder="Small" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Option 1</SelectItem>
                    <SelectItem value="2">Option 2</SelectItem>
                  </SelectContent>
                </Select>

                <Select>
                  <SelectTrigger size="md">
                    <SelectValue placeholder="Medium (default)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Option 1</SelectItem>
                    <SelectItem value="2">Option 2</SelectItem>
                  </SelectContent>
                </Select>

                <Select>
                  <SelectTrigger size="lg">
                    <SelectValue placeholder="Large" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Option 1</SelectItem>
                    <SelectItem value="2">Option 2</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
        </section>

        {/* Design Tokens Reference */}
        <section className="component-section">
          <h2 className="section-title">Design Tokens</h2>
          <p className="section-description">Color palette and spacing system reference</p>

          <div className="examples-grid">
            {/* Colors */}
            <div className="example-group">
              <h3 className="example-title">Primary Colors</h3>
              <div className="grid grid-cols-3 gap-3">
                <div className="color-swatch">
                  <div
                    className="swatch-preview"
                    style={{ background: 'var(--color-primary-500)' }}
                  />
                  <p className="swatch-label">Meeple Purple</p>
                </div>
                <div className="color-swatch">
                  <div
                    className="swatch-preview"
                    style={{ background: 'var(--color-secondary-500)' }}
                  />
                  <p className="swatch-label">Game Table Amber</p>
                </div>
                <div className="color-swatch">
                  <div className="swatch-preview" style={{ background: 'var(--color-blue)' }} />
                  <p className="swatch-label">Player Blue</p>
                </div>
              </div>
            </div>

            {/* Accent Colors */}
            <div className="example-group">
              <h3 className="example-title">Accent Colors</h3>
              <div className="grid grid-cols-4 gap-3">
                <div className="color-swatch">
                  <div className="swatch-preview" style={{ background: 'var(--color-red)' }} />
                  <p className="swatch-label">Red</p>
                </div>
                <div className="color-swatch">
                  <div className="swatch-preview" style={{ background: 'var(--color-green)' }} />
                  <p className="swatch-label">Green</p>
                </div>
                <div className="color-swatch">
                  <div className="swatch-preview" style={{ background: 'var(--color-yellow)' }} />
                  <p className="swatch-label">Yellow</p>
                </div>
                <div className="color-swatch">
                  <div className="swatch-preview" style={{ background: 'var(--color-blue)' }} />
                  <p className="swatch-label">Blue</p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <style jsx>{`
          .components-showcase {
            padding: var(--space-8) 0;
          }

          .hero-title {
            font-family: var(--font-display);
            font-size: var(--font-size-4xl);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            line-height: var(--line-height-tight);
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
          }

          .component-section {
            margin-bottom: var(--space-16);
            padding-bottom: var(--space-12);
            border-bottom: 1px solid var(--border-primary);
          }

          .component-section:last-child {
            border-bottom: none;
          }

          .section-title {
            font-family: var(--font-display);
            font-size: var(--font-size-3xl);
            font-weight: var(--font-weight-bold);
            color: var(--text-primary);
            margin-bottom: var(--space-3);
            letter-spacing: var(--letter-spacing-tight);
          }

          .section-description {
            font-size: var(--font-size-base);
            color: var(--text-secondary);
            margin-bottom: var(--space-8);
          }

          .examples-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: var(--space-8);
          }

          .example-group {
            background: var(--bg-elevated);
            border: 1px solid var(--border-primary);
            border-radius: var(--radius-xl);
            padding: var(--space-6);
          }

          .example-title {
            font-size: var(--font-size-lg);
            font-weight: var(--font-weight-semibold);
            color: var(--text-primary);
            margin-bottom: var(--space-4);
          }

          .color-swatch {
            text-align: center;
          }

          .swatch-preview {
            width: 100%;
            height: 80px;
            border-radius: var(--radius-lg);
            margin-bottom: var(--space-2);
            box-shadow: var(--shadow-md);
          }

          .swatch-label {
            font-size: var(--font-size-sm);
            color: var(--text-secondary);
            margin: 0;
          }

          @media (max-width: 768px) {
            .examples-grid {
              grid-template-columns: 1fr;
            }
          }
        `}</style>
      </div>
    </AppShell>
  );
}
