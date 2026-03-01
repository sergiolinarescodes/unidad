using UnityEngine;
using UnityEngine.UIElements;

namespace Unidad.Core.UI.WorldSpace
{
    public sealed class WorldUIHandle
    {
        public int Id { get; }
        public GameObject GameObject { get; }
        public UIDocument Document { get; }
        public VisualElement Root => Document?.rootVisualElement;

        public WorldUIHandle(int id, GameObject gameObject, UIDocument document)
        {
            Id = id;
            GameObject = gameObject;
            Document = document;
        }
    }
}
