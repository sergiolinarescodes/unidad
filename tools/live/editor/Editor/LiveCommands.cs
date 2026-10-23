using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Unidad.Live.Editor
{
    /// <summary>The live.* commands of <c>tools/live.ps1</c> (an overlay package, never committed to a game). Every
    /// answer is one JSON object. The settings arrive in UNIDAD_LIVE_* environment variables at launch.</summary>
    public static class LiveCommands
    {
        internal const string LaunchEnv = "UNIDAD_LAUNCH";
        const string PendingKey = "unidad.live.pending", PlaysKey = "unidad.live.plays", EnteredKey = "unidad.live.entered";

        [CliCommand("live.status", "Play state, compile state, play count and the game view's render size")]
        public static string Status()
        {
            var (w, h) = Capture.TargetSize();
            return Json(("playing", EditorApplication.isPlaying), ("compiling", EditorApplication.isCompiling),
                ("updating", EditorApplication.isUpdating), ("compileFailed", EditorUtility.scriptCompilationFailed),
                ("pending", SessionState.GetString(PendingKey, "") != ""), ("plays", SessionState.GetInt(PlaysKey, 0)),
                ("playSeconds", EditorApplication.isPlaying ? EditorApplication.timeSinceStartup - SessionState.GetFloat(EnteredKey, 0f) : 0),
                ("frame", Time.frameCount), ("width", w), ("height", h), ("pid", Process.GetCurrentProcess().Id));
        }

        [CliCommand("live.play", "Leave Play if playing, then enter it again through the testbed door with this launch line")]
        public static string Play([CliArg("launch64", "The launch line (UNIDAD_LAUNCH), base64 UTF-8: a value starting with -- would read as a CLI option")] string launch64 = "")
        {
            var launch = Encoding.UTF8.GetString(Convert.FromBase64String(launch64 ?? ""));
            SessionState.SetString(PendingKey, "1");
            Environment.SetEnvironmentVariable(LaunchEnv, launch ?? "");
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return Json(("requested", "stop, then play")); }
            Next(Door);
            return Json(("requested", "play"));
        }

        [CliCommand("live.stop", "Leave Play mode")]
        public static string Stop()
        {
            SessionState.SetString(PendingKey, "");
            EditorApplication.isPlaying = false;
            return Json(("requested", "stop"));
        }

        [CliCommand("live.refresh", "AssetDatabase.Refresh: imports what changed on disk (a script change compiles)")]
        public static string Refresh()
        {
            var sw = Stopwatch.StartNew();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return Json(("ms", sw.ElapsedMilliseconds), ("compiling", EditorApplication.isCompiling));
        }

        [CliCommand("live.shot", "Write the game view (overlay UI included) as a PNG at an absolute path")]
        public static string Shot([CliArg("path", "Absolute PNG path")] string path)
        {
            var size = Capture.Wanted();
            var sw = Stopwatch.StartNew();
            Capture.EnsureView(size.w, size.h);
            var (tw, th) = Capture.TargetSize();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Capture.Png(size.w, size.h));
            return Json(("path", path.Replace('\\', '/')), ("ms", sw.ElapsedMilliseconds), ("t", Time.time), ("unityFrame", Time.frameCount),
                ("width", size.w), ("height", size.h), ("targetWidth", tw), ("targetHeight", th), ("playing", EditorApplication.isPlaying));
        }

        [CliCommand("live.query", "The visible UI Toolkit elements that carry text or a name, with their panel rects")]
        public static string Query()
        {
            var sb = new StringBuilder("{\"elements\":[");
            var first = true;
            foreach (var document in Object.FindObjectsByType<UIDocument>())
            {
                if (document.rootVisualElement == null) continue;
                foreach (var e in document.rootVisualElement.Query<VisualElement>().ToList())
                {
                    var text = (e as TextElement)?.text;
                    if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(e.name)) continue;
                    if (!Shown(e)) continue;
                    var r = e.worldBound;
                    sb.Append(first ? "" : ",").Append(Json(("document", document.name), ("name", e.name ?? ""), ("type", e.GetType().Name),
                        ("classes", string.Join(" ", e.GetClasses())), ("text", text ?? ""), ("x", r.x), ("y", r.y), ("w", r.width), ("h", r.height)));
                    first = false;
                }
            }
            return sb.Append("]}").ToString();
        }

        [CliCommand("live.quit", "Exit the editor")]
        public static string Quit()
        {
            Next(() => EditorApplication.Exit(0));
            return Json(("requested", "quit"));
        }

        // Runs once on the next editor update (delayCall never fired for a command in a background editor).
        internal static void Next(Action action)
        {
            EditorApplication.CallbackFunction once = null;
            once = () => { EditorApplication.update -= once; action(); };
            EditorApplication.update += once;
        }

        // The testbed door named in UNIDAD_LIVE_DOOR ("Namespace.Type.Method"), found by reflection: the overlay names no game.
        internal static void Door()
        {
            var door = Environment.GetEnvironmentVariable("UNIDAD_LIVE_DOOR") ?? "";
            var dot = door.LastIndexOf('.');
#pragma warning disable UAC0005 // a by-name lookup of the door's type, once per Play entry
            var type = dot > 0 ? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(door.Substring(0, dot))).FirstOrDefault(t => t != null) : null;
#pragma warning restore UAC0005
            var method = type?.GetMethod(door.Substring(dot + 1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) { Debug.LogError("[live] no testbed door " + door); SessionState.SetString(PendingKey, ""); return; }
            var size = Capture.Wanted();
            Capture.EnsureView(size.w, size.h);
            method.Invoke(null, null);
        }

        internal static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(PendingKey, "");
                SessionState.SetInt(PlaysKey, SessionState.GetInt(PlaysKey, 0) + 1);
                SessionState.SetFloat(EnteredKey, (float)EditorApplication.timeSinceStartup);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetString(PendingKey, "") != "")
                Next(Door);
        }

        static bool Shown(VisualElement element)
        {
            if (element.panel == null || element.worldBound.width <= 0f) return false;
            for (var e = element; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        internal static string Json(params (string Key, object Value)[] fields)
        {
            var sb = new StringBuilder("{");
            for (var i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(fields[i].Key).Append("\":");
                switch (fields[i].Value)
                {
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case string s when s.StartsWith("{", StringComparison.Ordinal): sb.Append(s); break;
                    case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")).Append('"'); break;
                    case IFormattable f: sb.Append(f.ToString(null, CultureInfo.InvariantCulture)); break;
                    default: sb.Append("null"); break;
                }
            }
            return sb.Append('}').ToString();
        }
    }

    /// <summary>The live frame: cameras plus screen-space UI Toolkit panels composed off-screen (see Png), sized like
    /// the game look capture; written to an absolute path with no AssetDatabase import.</summary>
    internal static class Capture
    {
        static readonly Type PlayModeView = Type.GetType("UnityEditor.PlayModeView,UnityEditor");
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static (int w, int h) Wanted()
        {
            var parts = (Environment.GetEnvironmentVariable("UNIDAD_LIVE_SIZE") ?? "1600x900").Split('x');
            return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        internal static (int w, int h) TargetSize()
        {
            try
            {
                var size = (Vector2)PlayModeView.GetMethod("GetMainPlayModeViewTargetSize", Any).Invoke(null, null);
                return ((int)size.x, (int)size.y);
            }
            catch { return (Screen.width, Screen.height); }
        }

        // A Game view at a fixed w x h (internal API, best effort): Screen.width/height, and so every panel's scale, follow
        // it. A batchmode editor has none, so one is made as a floating window parked off-screen, never focused.
        internal static void EnsureView(int w, int h)
        {
            try
            {
                var view = PlayModeView.GetMethod("GetMainPlayModeView", Any).Invoke(null, null) as EditorWindow;
                if (view == null)
                {
                    view = (EditorWindow)ScriptableObject.CreateInstance(Type.GetType("UnityEditor.GameView,UnityEditor"));
                    view.titleContent = new GUIContent(Environment.GetEnvironmentVariable("UNIDAD_LIVE_TITLE") ?? "live");
                    view.position = new Rect(-6000f, 0f, w, h + 40);
                    view.Show();
                }
                if (Application.isBatchMode && view.position.x > -3000f) view.position = new Rect(-6000f, 0f, w, h + 40);
                if (TargetSize() == (w, h)) return;
                var set = view.GetType().GetMethod("SetCustomResolution", Any, null, new[] { typeof(Vector2), typeof(string) }, null);
                set?.Invoke(view, new object[] { new Vector2(w, h), "live" });
                view.Repaint();
            }
            catch (Exception e) { Debug.LogWarning("[live] could not size the game view: " + e.GetBaseException().Message); }
        }

        // The frame as the player composes it: every screen camera by depth into one w x h target, then every shown
        // screen-space UI Toolkit panel by sorting order, alpha-blended on top. The Game view's own target holds no
        // runtime panels on 7000.0.0a10 (it read black over the Movement sample's HUD), and this needs no Game view.
        internal static byte[] Png(int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black);
                foreach (var cam in Camera.allCameras.Where(c => c.targetTexture == null).OrderBy(c => c.depth))
                {
                    cam.targetTexture = rt;
                    try { cam.Render(); } finally { cam.targetTexture = null; }
                }
                RenderTexture.active = rt;
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, w, h, 0);
                foreach (var document in Object.FindObjectsByType<UIDocument>()
                             .Where(d => d.panelSettings != null && d.panelSettings.targetTexture == null && d.rootVisualElement?.panel != null)
                             .OrderBy(d => d.panelSettings.sortingOrder))
                {
                    var root = document.rootVisualElement;
                    if (root.resolvedStyle.display == DisplayStyle.None || root.worldBound.width <= 0f) continue;
                    var ui = root.CaptureToRenderTexture();
                    try
                    {
                        // shortcut: on 7000.0.0a10 the capture scales the content by pixelsPerPoint twice; drawn at
                        // 1/ppp it lands where the player draws it. Recheck after an editor upgrade.
                        var ppp = root.panel.scaledPixelsPerPoint;
                        RenderTexture.active = rt;
                        Graphics.DrawTexture(new Rect(0, 0, ui.width / ppp, ui.height / ppp), ui);
                    }
                    finally { ui.Release(); Object.DestroyImmediate(ui); }
                }
                GL.PopMatrix();
#pragma warning disable UD012 // the readback is the job: one frame grab per live.shot call
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
#pragma warning restore UD012
                return tex.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
        }
    }

    /// <summary>Closes the editor itself after UNIDAD_LIVE_IDLE_SECONDS without a live command (the touch file's age) or
    /// UNIDAD_LIVE_CAP_SECONDS after the process started. Inactive without UNIDAD_LIVE_TOUCH, so no other editor is affected.</summary>
    [InitializeOnLoad]
    internal static class LiveWatchdog
    {
        static readonly string Touch = Environment.GetEnvironmentVariable("UNIDAD_LIVE_TOUCH");
        static readonly double Idle = Seconds("UNIDAD_LIVE_IDLE_SECONDS", 600), Cap = Seconds("UNIDAD_LIVE_CAP_SECONDS", 3600);
        static readonly DateTime Started = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        static readonly Stopwatch Throttle = Stopwatch.StartNew();

        static LiveWatchdog()
        {
            if (string.IsNullOrEmpty(Touch)) return;
            EditorApplication.playModeStateChanged += LiveCommands.OnPlayMode;
            EditorApplication.update += Check;
            var title = Environment.GetEnvironmentVariable("UNIDAD_LIVE_TITLE");
            if (!string.IsNullOrEmpty(title))
            {
                EditorApplication.updateMainWindowTitle += d => d.title = title + " " + d.title;
                EditorApplication.UpdateMainWindowTitle();
            }
        }

        static double Seconds(string name, double fallback) =>
            double.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : fallback;

        static void Check()
        {
            if (Throttle.ElapsedMilliseconds < 1000) return;
            Throttle.Restart();
            var utc = DateTime.UtcNow;
            var idle = File.Exists(Touch) ? (utc - File.GetLastWriteTimeUtc(Touch)).TotalSeconds : (utc - Started).TotalSeconds;
            var age = (utc - Started).TotalSeconds;
            if (idle < Idle && age < Cap) return;
            Debug.Log($"[live] closing: {(age >= Cap ? $"{Cap:0} s cap" : $"{Idle:0} s without a live command")}");
            EditorApplication.update -= Check;
            EditorApplication.Exit(0);
        }
    }
}
