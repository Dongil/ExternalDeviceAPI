using System;

namespace Xeno.Framework.Matrix.Core
{
    public sealed class LogEntry
    {
        public DateTime Timestamp { get; }
        public LogDirection Direction { get; }
        public string Text { get; }

        public LogEntry(LogDirection direction, string text)
        {
            Timestamp = DateTime.Now;
            Direction = direction;
            Text = text ?? string.Empty;
        }

        public override string ToString()
        {
            return string.Format("{0:HH:mm:ss.fff}  {1,-4}  {2}",
                Timestamp, Direction.ToString().ToUpperInvariant(), Text);
        }
    }
}
