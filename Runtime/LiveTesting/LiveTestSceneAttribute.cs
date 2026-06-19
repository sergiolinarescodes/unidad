using System;

namespace Unidad.Core.LiveTesting
{
    /// <summary>
    /// Marks an <see cref="ILiveTestScene"/> implementation with static, instantiation-free
    /// metadata so the runner (Unidad.LiveTest.ListAll) can enumerate every live test —
    /// id, name, and scene path — WITHOUT constructing the (service-bound) scene object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class LiveTestSceneAttribute : Attribute
    {
        public string Id { get; }
        public string Name { get; }
        public string ScenePath { get; }

        public LiveTestSceneAttribute(string id, string name, string scenePath)
        {
            Id = id;
            Name = name;
            ScenePath = scenePath;
        }
    }
}
