using System;
using UnityEngine;

namespace Unidad.Core.ModelCatalog
{
    /// <summary>
    /// A model archetype. Instances of the same kind share folder, view class,
    /// effect profile and scale conventions. Mirrors Assets/Resources/ModelCatalog/kinds.json,
    /// which is also read by the picoCAD pipeline (skill + editor builder).
    /// </summary>
    [Serializable]
    public sealed class ModelKindDefinition
    {
        public string id;
        public string displayName;
        public string folder;
        public float unitScale = 1f;
        public string viewClass;
        public string effectProfile;
        public string[] effects;
        public string designNotes;
    }

    /// <summary>One imported model. Mirrors Assets/Resources/ModelCatalog/models.json.</summary>
    [Serializable]
    public sealed class ModelEntry
    {
        public string id;
        public string kindId;
        public string prefabPath; // Resources-relative, e.g. "Models/Misc/pig"
        public string[] clips;
    }

    /// <summary>
    /// In-memory catalog database. Load from Resources in production,
    /// construct directly in tests.
    /// </summary>
    public sealed class ModelCatalogDatabase
    {
        public ModelKindDefinition[] Kinds { get; }
        public ModelEntry[] Models { get; }

        public ModelCatalogDatabase(ModelKindDefinition[] kinds, ModelEntry[] models)
        {
            Kinds = kinds ?? Array.Empty<ModelKindDefinition>();
            Models = models ?? Array.Empty<ModelEntry>();
        }

        public static ModelCatalogDatabase LoadFromResources()
        {
            var kindsAsset = Resources.Load<TextAsset>("ModelCatalog/kinds");
            var modelsAsset = Resources.Load<TextAsset>("ModelCatalog/models");
            var kinds = kindsAsset != null ? JsonUtility.FromJson<KindRegistryFile>(kindsAsset.text) : null;
            var models = modelsAsset != null ? JsonUtility.FromJson<ModelRegistryFile>(modelsAsset.text) : null;
            return new ModelCatalogDatabase(kinds?.kinds, models?.models);
        }

        [Serializable]
        sealed class KindRegistryFile
        {
            public int version;
            public ModelKindDefinition[] kinds;
        }

        [Serializable]
        sealed class ModelRegistryFile
        {
            public int version;
            public ModelEntry[] models;
        }
    }
}
