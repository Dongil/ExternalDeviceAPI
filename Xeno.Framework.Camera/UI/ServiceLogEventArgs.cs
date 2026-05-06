using System;
using Xeno.Framework.Camera.Core;

namespace Xeno.Framework.Camera.UI
{
    /// <summary>
    /// Carries a log entry forwarded from one of the slot services in <see cref="CameraControl"/>.
    /// </summary>
    public sealed class ServiceLogEventArgs : EventArgs
    {
        public int SlotIndex { get; }
        public LogEntry Entry { get; }

        public ServiceLogEventArgs(int slotIndex, LogEntry entry)
        {
            SlotIndex = slotIndex;
            Entry = entry;
        }
    }
}
