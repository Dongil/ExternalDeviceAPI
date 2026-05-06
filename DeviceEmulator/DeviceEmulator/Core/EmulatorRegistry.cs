using System;
using System.Collections.Generic;

namespace DeviceEmulator.Core
{
    public static class EmulatorRegistry
    {
        public sealed class Entry
        {
            public string DeviceType;
            public string Brand;
            public string Model;
            public string[] SupportedTransports;
            public string Description;
            public Func<IDeviceEmulator> Create;
        }

        public static IReadOnlyList<Entry> All { get { return _all; } }

        private static readonly Entry[] _all = new[]
        {
            new Entry {
                DeviceType = "Camera", Brand = "Sony", Model = "EVI-H100",
                SupportedTransports = new[] { "Serial" },
                Description = "Sony EVI-H100 PTZ (RS-232/422 VISCA)",
                Create = () => new Emulators.EviH100Emulator()
            },
            new Entry {
                DeviceType = "Camera", Brand = "Sony", Model = "SRG-300H",
                SupportedTransports = new[] { "Udp", "Tcp" },
                Description = "Sony SRG-300H PTZ (VISCA over IP)",
                Create = () => new Emulators.ViscaIpEmulator("Sony", "SRG-300H")
            },
            new Entry {
                DeviceType = "Camera", Brand = "FR", Model = "FR-H50SN",
                SupportedTransports = new[] { "Tcp", "Udp" },
                Description = "FR-H50SN (raw VISCA over TCP/UDP)",
                Create = () => new Emulators.ViscaIpEmulator("FR", "FR-H50SN")
            },
            new Entry {
                DeviceType = "Camera", Brand = "Canon", Model = "CR-N300",
                SupportedTransports = new[] { "Udp" },
                Description = "Canon CR-N300 (VISCA over IP)",
                Create = () => new Emulators.ViscaIpEmulator("Canon", "CR-N300")
            },
            new Entry {
                DeviceType = "Camera", Brand = "Pelco", Model = "Pelco-D Generic",
                SupportedTransports = new[] { "Serial", "Udp", "Tcp" },
                Description = "Generic Pelco-D (RS-232/422/485 + UDP/TCP raw)",
                Create = () => new Emulators.PelcoDGenericEmulator()
            }
        };
    }
}
