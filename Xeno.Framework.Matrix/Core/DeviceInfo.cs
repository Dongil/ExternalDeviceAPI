namespace Xeno.Framework.Matrix.Core
{
    public sealed class DeviceInfo
    {
        public DeviceKind Kind { get; set; }
        public string Model { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public bool IsPresent { get; set; }

        public DeviceInfo Clone()
        {
            return new DeviceInfo
            {
                Kind = Kind,
                Model = Model,
                InputCount = InputCount,
                OutputCount = OutputCount,
                IsPresent = IsPresent
            };
        }
    }
}
