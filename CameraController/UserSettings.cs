using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Xeno.Framework.Camera.Core;
using Xeno.Framework.Camera.UI;

namespace CameraController
{
    /// <summary>
    /// Persists CameraControl slot configurations between launches.
    /// Stored as INI under %LocalAppData%\CameraController\settings.ini.
    /// </summary>
    internal sealed class UserSettings
    {
        public int SlotCount { get; set; } = 3;
        public List<CameraSlotConfig> Slots { get; } = new List<CameraSlotConfig>();

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CameraController");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static UserSettings Load()
        {
            var s = new UserSettings
            {
                SlotCount = ParseInt(ConfigurationManager.AppSettings["DefaultSlotCount"], 3)
            };

            try
            {
                if (!File.Exists(FilePath)) return s;
                var lines = File.ReadAllLines(FilePath);
                int currentSlot = -1;
                CameraSlotConfig cur = null;
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    if (line.StartsWith("[Slot", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]"))
                    {
                        if (cur != null && currentSlot >= 0) EnsureSlot(s, currentSlot, cur);
                        var num = line.Substring(5, line.Length - 6).Trim();
                        int idx;
                        currentSlot = int.TryParse(num, out idx) ? idx : -1;
                        cur = new CameraSlotConfig();
                        continue;
                    }
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1);

                    if (cur == null)
                    {
                        if (string.Equals(key, "SlotCount", StringComparison.OrdinalIgnoreCase))
                            s.SlotCount = ParseInt(value, s.SlotCount);
                        continue;
                    }
                    switch (key.ToLowerInvariant())
                    {
                        case "model":      cur.Model = value; break;
                        case "transport":  cur.Transport = ParseEnum(value, CameraTransportKind.Rs232); break;
                        case "comport":    cur.ComPort = value; break;
                        case "baudrate":   cur.BaudRate = ParseInt(value, 0); break;
                        case "address":    cur.Address = ParseInt(value, 1); break;
                        case "host":       cur.Host = value; break;
                        case "port":       cur.Port = ParseInt(value, 0); break;
                        case "homepreset": cur.HomePreset = ParseInt(value, 0); break;
                    }
                }
                if (cur != null && currentSlot >= 0) EnsureSlot(s, currentSlot, cur);
            }
            catch { /* ignore corrupt settings */ }
            return s;
        }

        public void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "SlotCount=" + SlotCount.ToString()
                };
                for (int i = 0; i < Slots.Count; i++)
                {
                    var c = Slots[i] ?? new CameraSlotConfig();
                    lines.Add("");
                    lines.Add("[Slot" + i + "]");
                    lines.Add("Model="      + (c.Model ?? string.Empty));
                    lines.Add("Transport="  + c.Transport.ToString());
                    lines.Add("ComPort="    + (c.ComPort ?? string.Empty));
                    lines.Add("BaudRate="   + c.BaudRate.ToString());
                    lines.Add("Address="    + c.Address.ToString());
                    lines.Add("Host="       + (c.Host ?? string.Empty));
                    lines.Add("Port="       + c.Port.ToString());
                    lines.Add("HomePreset=" + c.HomePreset.ToString());
                }
                File.WriteAllLines(FilePath, lines);
            }
            catch { /* best effort */ }
        }

        private static void EnsureSlot(UserSettings s, int idx, CameraSlotConfig cfg)
        {
            while (s.Slots.Count <= idx) s.Slots.Add(new CameraSlotConfig());
            s.Slots[idx] = cfg;
        }

        private static int ParseInt(string v, int fallback)
        {
            int x;
            return int.TryParse(v, out x) ? x : fallback;
        }

        private static T ParseEnum<T>(string v, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(v)) return fallback;
            try { return (T)Enum.Parse(typeof(T), v, true); } catch { return fallback; }
        }
    }
}
