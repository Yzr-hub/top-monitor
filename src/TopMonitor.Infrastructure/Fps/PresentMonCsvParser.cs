using System.Globalization;
using System.Text;
using TopMonitor.Application.Fps;

namespace TopMonitor.Infrastructure.Fps;

public sealed class PresentMonCsvParser
{
    private int _processIdIndex = -1;
    private int _droppedIndex = -1;
    private int _timeIndex = -1;
    private int _presentModeIndex = -1;

    public bool TryReadHeader(string line)
    {
        if (!TryReadFields(line, out var fields))
        {
            return false;
        }

        _processIdIndex = FindColumn(fields, "ProcessID");
        _droppedIndex = FindColumn(fields, "Dropped");
        _timeIndex = FindColumn(fields, "TimeInSeconds");
        _presentModeIndex = FindColumn(fields, "PresentMode");
        return _processIdIndex >= 0 &&
               _droppedIndex >= 0 &&
               _timeIndex >= 0 &&
               _presentModeIndex >= 0;
    }

    public bool TryReadFrame(string line, out PresentedFrame frame)
    {
        frame = null!;
        if (_processIdIndex < 0 ||
            _droppedIndex < 0 ||
            _timeIndex < 0 ||
            _presentModeIndex < 0 ||
            !TryReadFields(line, out var fields))
        {
            return false;
        }

        var maximumIndex = Math.Max(
            Math.Max(_processIdIndex, _droppedIndex),
            Math.Max(_timeIndex, _presentModeIndex));
        if (fields.Count <= maximumIndex ||
            !int.TryParse(
                fields[_processIdIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var processId) ||
            processId <= 0 ||
            !double.TryParse(
                fields[_timeIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var timeSeconds) ||
            !double.IsFinite(timeSeconds) ||
            timeSeconds < 0 ||
            IsDropped(fields[_droppedIndex]))
        {
            return false;
        }

        frame = new PresentedFrame(
            processId,
            timeSeconds,
            fields[_presentModeIndex]);
        return true;
    }

    private static bool IsDropped(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    private static int FindColumn(IReadOnlyList<string> fields, string name)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(
                    fields[index],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadFields(string line, out IReadOnlyList<string> fields)
    {
        var result = new List<string>();
        var field = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (insideQuotes)
        {
            fields = [];
            return false;
        }

        result.Add(field.ToString());
        fields = result;
        return true;
    }
}
