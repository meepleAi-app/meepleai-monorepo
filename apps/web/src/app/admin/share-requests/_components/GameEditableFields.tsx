import { Input } from '@/components/ui';
import { Textarea } from '@/components/ui';
import { Label } from '@/components/ui';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui';

/**
 * Game Editable Fields Component
 *
 * Displays editable fields for game title and description during admin review.
 * Allows admin to make corrections before approving the request.
 *
 * Fields:
 * - Title: Text input (max 255 chars)
 * - Description: Textarea (max 5000 chars)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface GameEditableFieldsProps {
  title: string;
  description: string;
  onTitleChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  disabled?: boolean;
}

export function GameEditableFields({
  title,
  description,
  onTitleChange,
  onDescriptionChange,
  disabled = false,
}: GameEditableFieldsProps){
  return (
    <Card>
      <CardHeader>
        <CardTitle>Game Details</CardTitle>
        <CardDescription>
          You can edit the title and description before approving.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Title */}
        <div className="space-y-2">
          <Label htmlFor="game-title">Title</Label>
          <Input
            id="game-title"
            value={title}
            onChange={(e) => onTitleChange(e.target.value)}
            disabled={disabled}
            maxLength={255}
          />
          <p className="text-xs text-muted-foreground">{title.length}/255 characters</p>
        </div>

        {/* Description */}
        <div className="space-y-2">
          <Label htmlFor="game-description">Description</Label>
          <Textarea
            id="game-description"
            value={description}
            onChange={(e) => onDescriptionChange(e.target.value)}
            disabled={disabled}
            rows={6}
            maxLength={5000}
          />
          <p className="text-xs text-muted-foreground">
            {description.length}/5000 characters
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
