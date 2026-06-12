using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unidad.Core.ModelCatalog;

namespace Unidad.Core.Editor.PicoCad
{
    /// <summary>
    /// Editor-side read/write access to the ModelCatalog registries
    /// (Assets/Resources/ModelCatalog/kinds.json + models.json). Same JSON shape
    /// the runtime ModelCatalogDatabase loads. Used by the prefab builder, the
    /// MCP tools, and the /picocad-model pipeline.
    /// </summary>
    public static class ModelCatalogRegistry
    {
        public const string KindsPath = "Assets/Resources/ModelCatalog/kinds.json";
        public const string ModelsPath = "Assets/Resources/ModelCatalog/models.json";

        [Serializable]
        sealed class KindRegistryFile
        {
            public int version = 1;
            public ModelKindDefinition[] kinds = Array.Empty<ModelKindDefinition>();
        }

        [Serializable]
        sealed class ModelRegistryFile
        {
            public int version = 1;
            public ModelEntry[] models = Array.Empty<ModelEntry>();
        }

        public static ModelKindDefinition[] LoadKinds()
        {
            if (!File.Exists(KindsPath)) return Array.Empty<ModelKindDefinition>();
            var file = JsonUtility.FromJson<KindRegistryFile>(File.ReadAllText(KindsPath));
            return file?.kinds ?? Array.Empty<ModelKindDefinition>();
        }

        public static ModelEntry[] LoadModels()
        {
            if (!File.Exists(ModelsPath)) return Array.Empty<ModelEntry>();
            var file = JsonUtility.FromJson<ModelRegistryFile>(File.ReadAllText(ModelsPath));
            return file?.models ?? Array.Empty<ModelEntry>();
        }

        public static ModelKindDefinition FindKind(string kindId) =>
            LoadKinds().FirstOrDefault(k => k.id == kindId);

        /// <summary>Insert or replace a kind by id, then reimport the registry asset.</summary>
        public static void UpsertKind(ModelKindDefinition kind)
        {
            if (kind == null || string.IsNullOrEmpty(kind.id))
                throw new ArgumentException("kind.id is required");
            var kinds = LoadKinds().Where(k => k.id != kind.id).Append(kind).ToArray();
            Write(KindsPath, JsonUtility.ToJson(new KindRegistryFile { kinds = kinds }, true));
        }

        /// <summary>Insert or replace a model entry by id, then reimport the registry asset.</summary>
        public static void UpsertModel(ModelEntry model)
        {
            if (model == null || string.IsNullOrEmpty(model.id))
                throw new ArgumentException("model.id is required");
            var models = LoadModels().Where(m => m.id != model.id).Append(model).ToArray();
            Write(ModelsPath, JsonUtility.ToJson(new ModelRegistryFile { models = models }, true));
        }

        static void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json + "\n");
            AssetDatabase.ImportAsset(path);
        }
    }
}
