using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Stadium3D.Telemetry
{
    /// <summary>
    /// Append-only JSON-lines log of operator interactions — attention changes, goal issues, overrides.
    /// Written now but <b>not</b> wired to Hermes as a training signal yet; that closed loop is a
    /// post-POC extension (design plan §3.4). One JSON object per line at
    /// <c>&lt;persistentDataPath&gt;/care_interactions/&lt;prefix&gt;_&lt;utcstamp&gt;.jsonl</c>.
    /// </summary>
    public class InteractionLog : MonoBehaviour
    {
        [SerializeField, Tooltip("Sub-folder under Application.persistentDataPath.")]
        string m_Folder = "care_interactions";

        [SerializeField] string m_FilePrefix = "session";

        StreamWriter m_Writer;

        /// <summary>Path of the file being written, or null if the log failed to open.</summary>
        public string Path { get; private set; }

        void Awake()
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, m_Folder);
                Directory.CreateDirectory(dir);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                Path = System.IO.Path.Combine(dir, $"{m_FilePrefix}_{stamp}.jsonl");
                m_Writer = new StreamWriter(Path, append: true) { AutoFlush = true };
                Debug.Log($"[InteractionLog] Logging interactions to {Path}", this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[InteractionLog] Could not open log: {e.Message}", this);
            }
        }

        /// <summary>Append an event line. <paramref name="detail"/> is optional.</summary>
        public void Log(string type, string detail = null)
        {
            if (m_Writer == null)
                return;

            double t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            m_Writer.WriteLine(BuildLine(t, type, detail));
        }

        /// <summary>Build one JSON-lines record: <c>{"t":..,"type":..,"detail":..}</c>. Static + pure, so
        /// the serialization (including escaping) is unit-tested without touching the filesystem.</summary>
        public static string BuildLine(double t, string type, string detail)
        {
            var sb = new StringBuilder(64);
            sb.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"type\":").Append(JsonString(type));
            if (!string.IsNullOrEmpty(detail))
                sb.Append(",\"detail\":").Append(JsonString(detail));
            sb.Append('}');
            return sb.ToString();
        }

        static string JsonString(string s)
        {
            if (s == null)
                return "null";

            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        void OnDestroy()
        {
            m_Writer?.Flush();
            m_Writer?.Dispose();
            m_Writer = null;
        }
    }
}
