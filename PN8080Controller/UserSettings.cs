using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Xeno.Framework.Matrix.Core;

namespace PN8080Controller
{
    internal enum UiMode
    {
        Grid = 0,
        AliasPanel = 1
    }

    /// <summary>
    /// Application-level persisted settings for the test harness (not part of the framework).
    /// </summary>
    internal sealed class UserSettings
    {
        private const string AliasesPrefix = "Aliases_";

        public DeviceKind Device { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public ConnectionMode Mode { get; set; }
        public bool AutoReconnect { get; set; }
        public int MaxInputs { get; set; }
        public int MaxOutputs { get; set; }
        public bool AutoTake { get; set; }
        public UiMode UiMode { get; set; }

        // In-memory cache of aliases keyed by "Host_Port_Inputs" or "Host_Port_Outputs"
        private readonly Dictionary<string, string[]> _aliasCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        private static string FilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PN8080Controller");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static UserSettings Load()
        {
            var s = new UserSettings
            {
                Device = ParseEnum(ConfigurationManager.AppSettings["DefaultDevice"], DeviceKind.Pn8080),
                Host = ConfigurationManager.AppSettings["DefaultHost"] ?? "192.168.1.100",
                Port = ParseInt(ConfigurationManager.AppSettings["DefaultPort"], 8000),
                Mode = ParseEnum(ConfigurationManager.AppSettings["DefaultMode"], ConnectionMode.Persistent),
                AutoReconnect = ParseBool(ConfigurationManager.AppSettings["DefaultAutoReconnect"], true),
                MaxInputs = ParseInt(ConfigurationManager.AppSettings["DefaultMaxInputs"], 8),
                MaxOutputs = ParseInt(ConfigurationManager.AppSettings["DefaultMaxOutputs"], 8),
                AutoTake = ParseBool(ConfigurationManager.AppSettings["DefaultAutoTake"], false),
                UiMode = ParseEnum(ConfigurationManager.AppSettings["DefaultUiMode"], UiMode.Grid)
            };

            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var kvp in ReadIni(FilePath))
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    if (key.StartsWith(AliasesPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        s._aliasCache[key] = SplitAliasString(value);
                        continue;
                    }

                    switch (key.ToLowerInvariant())
                    {
                        case "device": s.Device = ParseEnum(value, s.Device); break;
                        case "host": s.Host = value; break;
                        case "port": s.Port = ParseInt(value, s.Port); break;
                        case "mode": s.Mode = ParseEnum(value, s.Mode); break;
                        case "autoreconnect": s.AutoReconnect = ParseBool(value, s.AutoReconnect); break;
                        case "maxinputs": s.MaxInputs = ParseInt(value, s.MaxInputs); break;
                        case "maxoutputs": s.MaxOutputs = ParseInt(value, s.MaxOutputs); break;
                        case "autotake": s.AutoTake = ParseBool(value, s.AutoTake); break;
                        case "uimode": s.UiMode = ParseEnum(value, s.UiMode); break;
                    }
                }
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
                    "Device=" + Device.ToString(),
                    "Host=" + (Host ?? string.Empty),
                    "Port=" + Port.ToString(),
                    "Mode=" + Mode.ToString(),
                    "AutoReconnect=" + (AutoReconnect ? "true" : "false"),
                    "MaxInputs=" + MaxInputs.ToString(),
                    "MaxOutputs=" + MaxOutputs.ToString(),
                    "AutoTake=" + (AutoTake ? "true" : "false"),
                    "UiMode=" + UiMode.ToString()
                };
                foreach (var kvp in _aliasCache)
                {
                    lines.Add(kvp.Key + "=" + JoinAliasArray(kvp.Value));
                }
                File.WriteAllLines(FilePath, lines);
            }
            catch { /* best effort */ }
        }

        public AliasSettings GetAliasesFor(string host, int port, int inputs, int outputs)
        {
            var key = BuildKey(host, port);
            var inputsArr = _aliasCache.TryGetValue(key + "_Inputs", out var ia) ? ia : null;
            var outputsArr = _aliasCache.TryGetValue(key + "_Outputs", out var oa) ? oa : null;

            var aliases = AliasSettings.CreateDefault(inputs, outputs);
            if (inputsArr != null)
            {
                int copy = Math.Min(inputsArr.Length, inputs);
                for (int i = 0; i < copy; i++) aliases.InputAliases[i] = string.IsNullOrEmpty(inputsArr[i]) ? null : inputsArr[i];
            }
            if (outputsArr != null)
            {
                int copy = Math.Min(outputsArr.Length, outputs);
                for (int i = 0; i < copy; i++) aliases.OutputAliases[i] = string.IsNullOrEmpty(outputsArr[i]) ? null : outputsArr[i];
            }
            return aliases;
        }

        public void SaveAliases(string host, int port, AliasSettings aliases)
        {
            if (aliases == null) return;
            var key = BuildKey(host, port);
            if (aliases.InputAliases != null) _aliasCache[key + "_Inputs"] = (string[])aliases.InputAliases.Clone();
            if (aliases.OutputAliases != null) _aliasCache[key + "_Outputs"] = (string[])aliases.OutputAliases.Clone();
            Save();
        }

        private static string BuildKey(string host, int port)
        {
            return AliasesPrefix + (host ?? "unknown") + "_" + port.ToString();
        }

        private static string[] SplitAliasString(string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[0];
            return value.Split('|');
        }

        private static string JoinAliasArray(string[] values)
        {
            if (values == null) return string.Empty;
            return string.Join("|", values);
        }

        private static IEnumerable<KeyValuePair<string, string>> ReadIni(string path)
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                yield return new KeyValuePair<string, string>(
                    line.Substring(0, idx).Trim(), line.Substring(idx + 1));
            }
        }

        private static int ParseInt(string v, int fallback) { int x; return int.TryParse(v, out x) ? x : fallback; }
        private static bool ParseBool(string v, bool fallback) { bool x; return bool.TryParse(v, out x) ? x : fallback; }

        private static T ParseEnum<T>(string v, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(v)) return fallback;
            try { return (T)Enum.Parse(typeof(T), v, true); } catch { return fallback; }
        }
    }
}
