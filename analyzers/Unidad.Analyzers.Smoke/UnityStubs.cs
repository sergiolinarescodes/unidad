namespace Unidad.Analyzers.Smoke
{
    /// <summary>The few Unity shapes the smoke tests touch, compiled in memory into a reference named UnityEngine.</summary>
    internal static class UnityStubs
    {
        public const string Source = """
            namespace UnityEngine
            {
                public class Object
                {
                    public static T Instantiate<T>(T original) where T : Object => original;
                    public static void Destroy(Object o) { }
                }
                public class GameObject : Object
                {
                    public static GameObject Find(string name) => null;
                    public T AddComponent<T>() where T : Component => null;
                }
                public class Component : Object { public T GetComponent<T>() => default; }
                public class Transform : Component { public Transform parent { get; set; } }
                public class Behaviour : Component { }
                public class MonoBehaviour : Behaviour { }
                public struct Vector2 { public float x, y; }
                public struct Vector3 { public float x, y, z; }
                public static class Time { public static float deltaTime => 0; }
                public static class Input { public static Vector3 mousePosition => default; }
                public static class Random { public static float value => 0; }
                public static class Mathf { public static float Sin(float f) => f; }
                public static class Resources { public static Object Load(string path) => null; }
                public static class Cursor { public static bool visible { get => true; set { } } }
                public class Texture2D : Object { public void ReadPixels() { } }
            }
            namespace UnityEngine.InputSystem
            {
                public class Mouse
                {
                    public static Mouse current => null;
                    public void WarpCursorPosition(UnityEngine.Vector2 position) { }
                }
            }
            namespace UnityEngine.SceneManagement
            {
                public static class SceneManager { public static void LoadScene(string name) { } }
            }
            namespace UnityEditor
            {
                public static class AssetDatabase { public static void Refresh() { } }
            }
            namespace Unity.Mathematics
            {
                public static class math { public static float sin(float x) => x; }
            }
            """;
    }
}
