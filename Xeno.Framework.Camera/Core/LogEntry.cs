using System;

namespace Xeno.Framework.Camera.Core
{
    public enum LogDirection
    {
        Tx,
        Rx,
        Info,
        Error
    }

    /// <summary>
    /// Single log line emitted by an <see cref="ICameraService"/>.
    /// Hosts decide presentation (textbox, file, console, …).
    /// </summary>
    public sealed class LogEntry
    {
        public LogDirection Direction { get; }
        public string Text { get; }
        public DateTime TimestampUtc { get; }

        public LogEntry(LogDirection direction, string text)
        {
            Direction = direction;
            Text = text ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
