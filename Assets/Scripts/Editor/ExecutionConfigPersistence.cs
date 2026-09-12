#if UNITY_EDITOR

using Character.Config;
using UnityEditor;
using UnityEngine;

namespace SS3C.Editor
{
    public static class ExecutionConfigPersistence
    {
        private const string PresentationPath =
            "Assets/Data/Character/DefaultPresentation.asset";

        [MenuItem("Tools/SS3C/Execution/Persist Default Presentation")]
        public static void PersistDefaultPresentation()
        {
            CharacterPresentationConfig asset =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationConfig>(
                    PresentationPath);

            if (asset == null)
            {
                Debug.LogError(
                    $"Missing presentation config: {PresentationPath}");
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();

            bool valid = true;
            valid &= SetFloat(
                serialized,
                "executingCrossFadeDuration",
                0.08f);
            valid &= SetFloat(
                serialized,
                "executedCrossFadeDuration",
                0.08f);
            valid &= SetFloat(
                serialized,
                "executedDeathCrossFadeDuration",
                0.08f);
            valid &= SetFloat(
                serialized,
                "executeClipDuration",
                2.7f);
            valid &= SetFloat(
                serialized,
                "executedClipDuration",
                3.516667f);
            valid &= SetFloat(
                serialized,
                "executedDeathClipDuration",
                2.5333335f);

            if (!valid)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Selection.activeObject = asset;

            Debug.Log(
                $"Persisted execution presentation fields: " +
                $"{PresentationPath}");
        }

        private static bool SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serialized.FindProperty(propertyName);

            if (property == null ||
                property.propertyType != SerializedPropertyType.Float)
            {
                Debug.LogError(
                    $"Missing float property: {propertyName}");
                return false;
            }

            property.floatValue = value;
            return true;
        }
    }
}

#endif