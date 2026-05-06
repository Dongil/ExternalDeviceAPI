namespace Xeno.Framework.Camera.Core
{
    public enum PanTiltDirection
    {
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Stop
    }

    public enum ZoomDirection
    {
        Tele,
        Wide,
        Stop
    }

    public enum FocusDirection
    {
        Far,
        Near,
        Stop
    }

    public enum PresetMode
    {
        Recall,
        Set
    }

    public enum ConnectionMode
    {
        Persistent = 0,
        PerCommand = 1
    }
}
