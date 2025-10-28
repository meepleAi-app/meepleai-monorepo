using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace Api.Services.Pdf;

/// <summary>
/// Custom extraction strategy for positioned text
/// </summary>
public sealed class PositionedTextExtractionStrategy : IEventListener
{
    private readonly List<PositionedCharacter> _characters = new();

    public void EventOccurred(IEventData data, EventType type)
    {
        if (type != EventType.RENDER_TEXT)
        {
            return;
        }

        if (data is TextRenderInfo renderInfo)
        {
            foreach (var characterInfo in renderInfo.GetCharacterRenderInfos())
            {
                var text = characterInfo.GetText();

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var baseline = characterInfo.GetBaseline();
                var startPoint = baseline.GetStartPoint();
                var endPoint = baseline.GetEndPoint();
                var x = (float)startPoint.Get(Vector.I1);
                var y = (float)startPoint.Get(Vector.I2);
                var width = (float)Math.Abs(endPoint.Get(Vector.I1) - startPoint.Get(Vector.I1));

                if (width <= 0)
                {
                    width = characterInfo.GetSingleSpaceWidth();
                }

                _characters.Add(new PositionedCharacter(text, x, y, width));
            }
        }
    }

    public ICollection<EventType> GetSupportedEvents() => new HashSet<EventType> { EventType.RENDER_TEXT };

    public List<PositionedTextLine> GetLines()
    {
        const float yTolerance = 2.5f;

        var orderedCharacters = _characters
            .OrderByDescending(c => c.Y)
            .ToList();

        var lines = new List<PositionedTextLine>();

        foreach (var character in orderedCharacters)
        {
            var line = lines.FirstOrDefault(existing => Math.Abs(existing.Y - character.Y) <= yTolerance);

            if (line == null)
            {
                line = new PositionedTextLine(character.Y);
                lines.Add(line);
            }

            line.AddCharacter(character);
        }

        foreach (var line in lines)
        {
            line.SortCharacters();
        }

        lines.Sort((a, b) => b.Y.CompareTo(a.Y));

        return lines;
    }
}
