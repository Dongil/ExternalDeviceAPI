using System;
using System.Threading.Tasks;

namespace DeviceEmulator.Core
{
    public abstract class DeviceEmulatorBase : IDeviceEmulator
    {
        public string DeviceType { get; protected set; }
        public string Brand { get; protected set; }
        public string Model { get; protected set; }
        public string Description { get; protected set; }
        public EmulatorOptions Options { get; } = new EmulatorOptions();

        protected EmulatorConfig _cfg;
        protected volatile bool _listening;
        public bool IsListening { get { return _listening; } }

        public event EventHandler<string> Logged;

        public abstract void Configure(EmulatorConfig cfg);
        public abstract void Start();
        public abstract void Stop();

        protected async Task SendReplyAsync(Action sendAction)
        {
            int latency = Options.LatencyMs;
            if (latency > 0) await Task.Delay(latency).ConfigureAwait(false);

            if (Options.InjectTimeout)
            {
                if (Options.ConsumeOnNextCommand) Options.InjectTimeout = false;
                Log("INJECT timeout — skip reply");
                return;
            }

            try { sendAction(); } catch (Exception ex) { Log("Reply send failed: " + ex.Message); }
        }

        protected void Log(string msg)
        {
            var h = Logged;
            if (h != null) try { h(this, msg); } catch { }
        }

        public abstract void Dispose();
    }
}
