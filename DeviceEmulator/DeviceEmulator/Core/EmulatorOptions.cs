namespace DeviceEmulator.Core
{
    public sealed class EmulatorOptions
    {
        public bool InjectNak { get; set; } = false;
        public bool InjectTimeout { get; set; } = false;
        public bool InjectMalformed { get; set; } = false;
        public int LatencyMs { get; set; } = 5;
        public bool ConsumeOnNextCommand { get; set; } = true;
    }
}
