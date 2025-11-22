import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Dialog, DialogTrigger, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';

/**
 * Shadcn/UI Demo Page
 *
 * Demonstrates the installed shadcn/ui components:
 * - Button (with variants)
 * - Card (with all subcomponents)
 * - Input
 * - Select
 * - Dialog
 */
export default function ShadcnDemo() {
  const [inputValue, setInputValue] = useState('');
  const [selectValue, setSelectValue] = useState('');

  return (
    <div className="min-h-dvh bg-slate-950 text-white p-8">
      <div className="max-w-6xl mx-auto space-y-8">
        {/* Header */}
        <div className="text-center space-y-4">
          <h1 className="text-4xl font-bold bg-gradient-to-r from-primary via-secondary to-accent bg-clip-text text-transparent">
            Shadcn/UI Components Demo
          </h1>
          <p className="text-slate-400 text-lg">
            Demonstration of installed shadcn/ui components for MeepleAI
          </p>
        </div>

        {/* Button Variants */}
        <Card>
          <CardHeader>
            <CardTitle>Button Component</CardTitle>
            <CardDescription>Various button styles and sizes</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-400">Variants</h3>
              <div className="flex flex-wrap gap-2">
                <Button variant="default">Default</Button>
                <Button variant="secondary">Secondary</Button>
                <Button variant="destructive">Destructive</Button>
                <Button variant="outline">Outline</Button>
                <Button variant="ghost">Ghost</Button>
                <Button variant="link">Link</Button>
              </div>
            </div>
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-400">Sizes</h3>
              <div className="flex flex-wrap items-center gap-2">
                <Button size="sm">Small</Button>
                <Button size="default">Default</Button>
                <Button size="lg">Large</Button>
                <Button size="icon">🎲</Button>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Input Component */}
        <Card>
          <CardHeader>
            <CardTitle>Input Component</CardTitle>
            <CardDescription>Text input field with styling</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-400">
                Enter text:
              </label>
              <Input
                type="text"
                placeholder="Type something..."
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
              />
              {inputValue && (
                <p className="text-sm text-slate-400">
                  You typed: <span className="text-primary">{inputValue}</span>
                </p>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Select Component */}
        <Card>
          <CardHeader>
            <CardTitle>Select Component</CardTitle>
            <CardDescription>Dropdown selection with options</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-400">
                Choose a board game:
              </label>
              <Select value={selectValue} onValueChange={setSelectValue}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a game..." />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="catan">Catan</SelectItem>
                  <SelectItem value="ticket-to-ride">Ticket to Ride</SelectItem>
                  <SelectItem value="pandemic">Pandemic</SelectItem>
                  <SelectItem value="wingspan">Wingspan</SelectItem>
                  <SelectItem value="azul">Azul</SelectItem>
                </SelectContent>
              </Select>
              {selectValue && (
                <p className="text-sm text-slate-400">
                  Selected: <span className="text-secondary">{selectValue}</span>
                </p>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Dialog Component */}
        <Card>
          <CardHeader>
            <CardTitle>Dialog Component</CardTitle>
            <CardDescription>Modal dialog with trigger button</CardDescription>
          </CardHeader>
          <CardContent>
            <Dialog>
              <DialogTrigger asChild>
                <Button>Open Dialog</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Welcome to MeepleAI</DialogTitle>
                  <DialogDescription>
                    This is a demonstration of the shadcn/ui Dialog component.
                    It can be used for confirmations, forms, or displaying information.
                  </DialogDescription>
                </DialogHeader>
                <div className="py-4">
                  <p className="text-sm text-slate-400">
                    Shadcn/ui provides beautiful, accessible components built with Radix UI and Tailwind CSS.
                  </p>
                </div>
                <DialogFooter>
                  <Button variant="outline">Cancel</Button>
                  <Button>Confirm</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </CardContent>
        </Card>

        {/* Nested Cards Example */}
        <div className="grid md:grid-cols-2 gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Card with Footer</CardTitle>
              <CardDescription>Example with actions</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-400">
                This card demonstrates the CardFooter component for action buttons.
              </p>
            </CardContent>
            <CardFooter className="flex justify-between">
              <Button variant="outline" size="sm">Cancel</Button>
              <Button size="sm">Save</Button>
            </CardFooter>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Installation Complete</CardTitle>
              <CardDescription>Issue #927 - FRONTEND-2 ✅</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-400">
                ✅ Shadcn/ui successfully installed<br />
                ✅ 16 components configured<br />
                ✅ Tailwind CSS 4 compatible<br />
                ✅ TypeScript types working<br />
                ✅ Theme integration complete
              </p>
            </CardContent>
          </Card>
        </div>

        {/* Footer Info */}
        <Card className="bg-gradient-to-br from-primary/10 to-secondary/10 border-primary/20">
          <CardHeader>
            <CardTitle>Installed Components</CardTitle>
            <CardDescription>Ready to use in your application</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="grid grid-cols-2 md:grid-cols-4 gap-2 text-sm">
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Button
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Card
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Input
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Select
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Dialog
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Dropdown Menu
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Table
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Avatar
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Badge
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Progress
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Skeleton
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Switch
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Textarea
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Toggle
              </li>
              <li className="flex items-center gap-2">
                <span className="text-primary">✓</span> Toggle Group
              </li>
              <li className="flex items-center gap-2">
                <span className="text-secondary">✓</span> Sonner (Toast)
              </li>
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

