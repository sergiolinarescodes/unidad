namespace Unidad.Core.ModelCatalog
{
    public readonly record struct ModelSpawnedEvent(string InstanceId, string ModelId, string KindId);

    public readonly record struct ModelDespawnedEvent(string InstanceId, string ModelId);

    public readonly record struct ModelEffectPlayedEvent(string InstanceId, string EffectId);
}
