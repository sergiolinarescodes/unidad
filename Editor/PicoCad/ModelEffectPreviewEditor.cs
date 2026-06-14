using UnityEditor;
using UnityEngine;
using Unidad.Core.ModelCatalog;

namespace Unidad.Core.Editor.PicoCad
{
    /// <summary>
    /// Inspector for <see cref="ModelEffectPreview"/>: one play button per kind
    /// effect (PrimeTween profile) and per baked Animator clip. Buttons are live
    /// in play mode on scene instances; the lists themselves are enumerated from
    /// the current profile implementation / controller every repaint, so newly
    /// added effects appear without rebuilding the prefab.
    /// </summary>
    [CustomEditor(typeof(ModelEffectPreview))]
    sealed class ModelEffectPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var preview = (ModelEffectPreview)target;
            var effectIds = preview.GetEffectIds();
            var clipNames = preview.GetClipNames();
            var profile = preview.ResolveProfile();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (profile == null && effectIds.Length > 0)
                EditorGUILayout.HelpBox(
                    $"Effect profile '{preview.ResolveKind()?.effectProfile}' is not compiled in — " +
                    "buttons show the ids registered in kinds.json but cannot play.", MessageType.Warning);

            var isAsset = EditorUtility.IsPersistent(preview.gameObject);
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to preview effects and clips.", MessageType.Info);
            else if (isAsset)
                EditorGUILayout.HelpBox("Select a scene instance (not the prefab asset) to preview.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || isAsset))
            {
                if (effectIds.Length > 0)
                {
                    EditorGUILayout.LabelField($"Effects ({preview.ResolveKind()?.effectProfile ?? "?"})", EditorStyles.miniBoldLabel);
                    foreach (var effectId in effectIds)
                    {
                        if (GUILayout.Button($"▶ {effectId}"))
                            preview.PlayEffect(effectId);
                    }
                }

                if (clipNames.Length > 0)
                {
                    EditorGUILayout.LabelField("Baked clips", EditorStyles.miniBoldLabel);
                    foreach (var clipName in clipNames)
                    {
                        if (GUILayout.Button($"▶ clip: {clipName}"))
                            preview.PlayClip(clipName);
                    }
                }
            }

            if (effectIds.Length == 0 && clipNames.Length == 0)
                EditorGUILayout.HelpBox("No effects or clips found for this model's kind.", MessageType.Info);
        }
    }
}
