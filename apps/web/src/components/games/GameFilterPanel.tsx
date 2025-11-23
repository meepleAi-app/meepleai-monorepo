import React, { useCallback } from 'react';
import { GameFilters } from '@/lib/api';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Separator } from '@/components/ui/separator';
import { RotateCcw, ChevronDown, ChevronUp } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface GameFilterPanelProps {
  filters: GameFilters;
  onChange: (filters: GameFilters) => void;
  collapsible?: boolean;
  defaultCollapsed?: boolean;
}

const PLAYER_OPTIONS = Array.from({ length: 12 }, (_, i) => i + 1);
const PLAYTIME_OPTIONS = [15, 30, 60, 90, 120, 180, 240];
const YEAR_OPTIONS = Array.from({ length: 2025 - 1990 + 1 }, (_, i) => 2025 - i);

export function GameFilterPanel({
  filters,
  onChange,
  collapsible = false,
  defaultCollapsed = false
}: GameFilterPanelProps) {
  const [isCollapsed, setIsCollapsed] = React.useState(defaultCollapsed);

  const handleFilterChange = useCallback((key: keyof GameFilters, value: any) => {
    onChange({ ...filters, [key]: value });
  }, [filters, onChange]);

  const handleReset = useCallback(() => {
    onChange({});
  }, [onChange]);

  const hasActiveFilters = Object.keys(filters).length > 0;

  return (
    <Card>
      <CardHeader>
        <div className="flex justify-between items-center">
          <CardTitle className="text-lg">Filters</CardTitle>
          {collapsible && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsCollapsed(!isCollapsed)}
              aria-label={isCollapsed ? 'Expand filters' : 'Collapse filters'}
              aria-expanded={!isCollapsed}
            >
              {isCollapsed ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
            </Button>
          )}
        </div>
      </CardHeader>

      {(!collapsible || !isCollapsed) && (
        <CardContent className="space-y-4">
          {/* Player Count */}
          <div className="space-y-2">
            <Label className="text-sm font-medium">Players</Label>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label htmlFor="minPlayers" className="text-xs text-muted-foreground">Min</Label>
                <Select
                  value={filters.minPlayers?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('minPlayers', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="minPlayers">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {PLAYER_OPTIONS.map(num => (
                      <SelectItem key={num} value={num.toString()}>{num}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1">
                <Label htmlFor="maxPlayers" className="text-xs text-muted-foreground">Max</Label>
                <Select
                  value={filters.maxPlayers?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('maxPlayers', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="maxPlayers">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {PLAYER_OPTIONS.map(num => (
                      <SelectItem key={num} value={num.toString()}>{num}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          <Separator />

          {/* Play Time */}
          <div className="space-y-2">
            <Label className="text-sm font-medium">Play Time (minutes)</Label>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label htmlFor="minPlayTime" className="text-xs text-muted-foreground">Min</Label>
                <Select
                  value={filters.minPlayTime?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('minPlayTime', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="minPlayTime">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {PLAYTIME_OPTIONS.map(time => (
                      <SelectItem key={time} value={time.toString()}>{time}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1">
                <Label htmlFor="maxPlayTime" className="text-xs text-muted-foreground">Max</Label>
                <Select
                  value={filters.maxPlayTime?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('maxPlayTime', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="maxPlayTime">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {PLAYTIME_OPTIONS.map(time => (
                      <SelectItem key={time} value={time.toString()}>{time}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          <Separator />

          {/* Year Published */}
          <div className="space-y-2">
            <Label className="text-sm font-medium">Year Published</Label>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label htmlFor="yearFrom" className="text-xs text-muted-foreground">From</Label>
                <Select
                  value={filters.yearFrom?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('yearFrom', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="yearFrom">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {YEAR_OPTIONS.map(year => (
                      <SelectItem key={year} value={year.toString()}>{year}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1">
                <Label htmlFor="yearTo" className="text-xs text-muted-foreground">To</Label>
                <Select
                  value={filters.yearTo?.toString() || 'none'}
                  onValueChange={(value) => handleFilterChange('yearTo', value === 'none' ? undefined : parseInt(value))}
                >
                  <SelectTrigger id="yearTo">
                    <SelectValue placeholder="Any" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any</SelectItem>
                    {YEAR_OPTIONS.map(year => (
                      <SelectItem key={year} value={year.toString()}>{year}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          <Separator />

          {/* BGG Only */}
          <div className="flex items-center space-x-2">
            <Checkbox
              id="bggOnly"
              checked={filters.bggOnly || false}
              onCheckedChange={(checked) => handleFilterChange('bggOnly', checked)}
            />
            <Label
              htmlFor="bggOnly"
              className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 cursor-pointer"
            >
              Show only BGG games
            </Label>
          </div>

          <Separator />

          {/* Reset Button */}
          <Button
            variant="outline"
            size="sm"
            onClick={handleReset}
            disabled={!hasActiveFilters}
            className="w-full"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            Reset Filters
          </Button>
        </CardContent>
      )}
    </Card>
  );
}