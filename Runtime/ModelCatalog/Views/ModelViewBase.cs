using UnityEngine;

namespace Unidad.Core.ModelCatalog.Views
{
    /// <summary>
    /// Plain C# wrapper around a spawned model GameObject (NOT a MonoBehaviour —
    /// per Unidad rules the prefab carries only mesh/Animator components).
    /// Per-kind subclasses add behavior hooks; the service drives Tick.
    /// </summary>
    public class ModelViewBase
    {
        public GameObject Root { get; }
        public Animator Animator { get; }

        public ModelViewBase(GameObject root)
        {
            Root = root;
            Animator = root != null ? root.GetComponent<Animator>() : null;
        }

        /// <summary>Play a baked clip state on the Animator (no-op when none).</summary>
        public void PlayClip(string stateName)
        {
            if (Animator != null && Animator.runtimeAnimatorController != null)
                Animator.Play(stateName);
        }

        /// <summary>Per-frame hook driven by the service's Tick.</summary>
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>Default view for kinds without a custom view class.</summary>
    public sealed class DefaultModelView : ModelViewBase
    {
        public DefaultModelView(GameObject root) : base(root) { }
    }
}
