namespace Unidad.Flow
{
    /// <summary>A scene the game registers by name (its build-profile scene name).</summary>
    public readonly record struct SceneId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>What one scene hands the next across a switch.</summary>
    public interface ISceneTicket
    {
    }

    public interface ISceneSwitchService
    {
        /// <summary>The scene a switch was accepted for; never cleared (the next scene builds a new container).</summary>
        SceneId? Pending { get; }

        bool SwitchTo(SceneId scene) => SwitchTo(scene, null);

        /// <summary>Accepted only in Play or Paused with nothing pending; publishes <see cref="SceneSwitchRequestedEvent"/>.</summary>
        bool SwitchTo(SceneId scene, ISceneTicket ticket);
    }
}
