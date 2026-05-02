using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using static ModLoader.LogSystem;

namespace ChaosMod
{
    internal static class ChaosSettings
    {
        public static bool Enabled = true;
        public static float ChaosInterval = 30f;   // seconds between random effects
        public static float EffectDuration = 10f;  // duration of timed effects
        public static bool ShowNotifications = true;
        public static float NotificationDuration = 3f;

        // Set to true when the player picks "Chaos" on the difficulty screen.
        // Chaos effects only fire when this is true.
        public static bool IsChaosMode = false;
    }

    internal sealed class ChaosConfig
    {
        public static string FolderConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "!Mods", "ChaosMod", "ChaosMod.Config.ini");

        public static string ConfigPath => FolderConfigPath;

        public bool Enabled = true;
        public float ChaosInterval = 30f;
        public float EffectDuration = 10f;
        public bool ShowNotifications = true;
        public float NotificationDuration = 3f;

        public static ChaosConfig LoadOrCreate()
        {
            string path = ConfigPath;
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(path))
            {
                File.WriteAllLines(path, new[]
                {
                    "# ChaosMod config",
                    "",
                    "[General]",
                    "; Master toggle for chaos mod.",
                    "Enabled = true",
                    "",
                    "; Seconds between each random chaos effect.",
                    "ChaosInterval = 30",
                    "",
                    "; How long timed effects last (seconds).",
                    "EffectDuration = 10",
                    "",
                    "; Show effect name notification on screen.",
                    "ShowNotifications = true",
                    "",
                    "; How long the notification stays on screen (seconds).",
                    "NotificationDuration = 3",
                });
            }

            var ini = SimpleIni.Load(path);
            return new ChaosConfig
            {
                Enabled              = ini.GetBool("General",  "Enabled",              true),
                ChaosInterval        = Clamp(ini.GetFloat("General", "ChaosInterval",   30f),  1f, 3600f),
                EffectDuration       = Clamp(ini.GetFloat("General", "EffectDuration",  10f),  1f,  300f),
                ShowNotifications    = ini.GetBool("General",  "ShowNotifications",     true),
                NotificationDuration = Clamp(ini.GetFloat("General", "NotificationDuration", 3f), 0.5f, 30f),
            };
        }

        public static void LoadApply()
        {
            try
            {
                var cfg = LoadOrCreate();
                ChaosSettings.Enabled              = cfg.Enabled;
                ChaosSettings.ChaosInterval        = cfg.ChaosInterval;
                ChaosSettings.EffectDuration       = cfg.EffectDuration;
                ChaosSettings.ShowNotifications    = cfg.ShowNotifications;
                ChaosSettings.NotificationDuration = cfg.NotificationDuration;
                Log($"[ChaosMod] Config loaded. Enabled={cfg.Enabled}, Interval={cfg.ChaosInterval}s");
            }
            catch (Exception ex)
            {
                Log($"[ChaosMod] Config load failed: {ex.Message}");
            }
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    internal sealed class SimpleIni
    {
        private readonly Dictionary<string, Dictionary<string, string>> _data =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static SimpleIni Load(string path)
        {
            var ini = new SimpleIni();
            string section = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (!ini._data.ContainsKey(section))
                        ini._data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (!ini._data.TryGetValue(section, out var dict))
                {
                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    ini._data[section] = dict;
                }
                dict[key] = val;
            }
            return ini;
        }

        public string GetString(string section, string key, string def)
            => (_data.TryGetValue(section, out var d) && d.TryGetValue(key, out var v)) ? v : def;

        public int GetInt(string section, string key, int def)
            => int.TryParse(GetString(section, key, def.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;

        public float GetFloat(string section, string key, float def)
            => float.TryParse(GetString(section, key, def.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;

        public bool GetBool(string section, string key, bool def)
        {
            string s = GetString(section, key, def ? "true" : "false");
            if (bool.TryParse(s, out bool b)) return b;
            if (int.TryParse(s, out int i)) return i != 0;
            return def;
        }
    }
}
