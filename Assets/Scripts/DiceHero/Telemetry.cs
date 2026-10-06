using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Play log for measuring how understandable the game is (campaign plan, section 08). One JSON object per line in
    /// persistentDataPath/telemetry/session-*.jsonl. Nothing leaves the machine: playtesters send the file by hand.
    /// Off when Settings.Telemetry is off. Batch tests and the demo capture don't write it.
    /// </summary>
    public static class Telemetry
    {
        static string path;
        static float t0;
        public static bool Enabled => Settings.Telemetry && Application.isPlaying && !Application.isBatchMode
                                      && (!DemoDirector.Requested || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-telemetrytest") >= 0);

        public static string Folder => Path.Combine(Application.persistentDataPath, "telemetry");

        /// <summary>Starts a new file the first time something is logged in this game session.</summary>
        static void Open()
        {
            if (path != null) return;
            Directory.CreateDirectory(Folder);
            path = Path.Combine(Folder, "session-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
            t0 = Time.realtimeSinceStartup;
            Write("session", "version", Application.version, "platform", Application.platform.ToString(),
                "controls", DiceController.ButtonMode ? "button" : "bump", "hints", Settings.ShowTutorial);
        }

        /// <summary>Logs an event with key/value pairs: Log("roll", "dir", "up", "useful", true).</summary>
        public static void Log(string ev, params object[] kv)
        {
            if (!Enabled) return;
            try { Open(); Write(ev, kv); }
            catch (System.Exception e) { Debug.LogWarning("[RollPower] telemetry off: " + e.Message); Settings.Telemetry = false; }
        }

        static void Write(string ev, params object[] kv)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"t\":").Append((Time.realtimeSinceStartup - t0).ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(",\"ev\":\"").Append(ev).Append('"');
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                sb.Append(",\"").Append(kv[i]).Append("\":");
                object v = kv[i + 1];
                switch (v)
                {
                    case null: sb.Append("null"); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case int n: sb.Append(n); break;
                    case float f: sb.Append(f.ToString("0.###", CultureInfo.InvariantCulture)); break;
                    default: sb.Append('"').Append(v.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                }
            }
            sb.Append("}\n");
            File.AppendAllText(path, sb.ToString());
        }
    }
}
