using UnityEditor;
using UnityEngine;

namespace MazeGrid.Editor
{
    /// <summary>
    /// Migration tool for updating MazeGridConfig data from old cell state values.
    /// Converts GridWall (3) → Invalid (0) in all MazeGridConfig fields found in ScriptableObjects.
    ///
    /// Note: Empty (0) → Invalid (0) and Full (1) → Valid (1) share the same integer values,
    /// so they migrate automatically. Only GridWall (3) needs explicit conversion.
    /// </summary>
    public static class MazeGridMigrationTool
    {
        [MenuItem("Tools/MazeGrid/Migrate Levels (GridWall → Invalid)")]
        public static void MigrateLevels()
        {
            // Find all ScriptableObjects in the project
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            int migratedAssets = 0;
            int migratedCells = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                // Use SerializedObject to find any MazeGridConfig fields
                var serializedObject = new SerializedObject(asset);
                bool assetChanged = false;

                // Iterate all properties to find gridConfig or any MazeGridConfig field
                var iterator = serializedObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    // Look for cells arrays that contain state fields
                    if (iterator.name == "cells" && iterator.isArray)
                    {
                        for (int i = 0; i < iterator.arraySize; i++)
                        {
                            var element = iterator.GetArrayElementAtIndex(i);
                            var stateProperty = element.FindPropertyRelative("state");

                            if (stateProperty != null && stateProperty.propertyType == SerializedPropertyType.Enum)
                            {
                                // GridWall was enum value 3, convert to Invalid (0)
                                if (stateProperty.enumValueIndex == 3)
                                {
                                    stateProperty.enumValueIndex = 0; // Invalid
                                    assetChanged = true;
                                    migratedCells++;
                                }
                            }

                            // Also migrate spawnerQueue cells
                            var spawnerQueue = element.FindPropertyRelative("spawnerQueue");
                            if (spawnerQueue != null && spawnerQueue.isArray)
                            {
                                for (int j = 0; j < spawnerQueue.arraySize; j++)
                                {
                                    var queueElement = spawnerQueue.GetArrayElementAtIndex(j);
                                    var queueState = queueElement.FindPropertyRelative("state");
                                    if (queueState != null && queueState.enumValueIndex == 3)
                                    {
                                        queueState.enumValueIndex = 0;
                                        assetChanged = true;
                                        migratedCells++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (assetChanged)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    migratedAssets++;
                    Debug.Log($"MazeGrid Migration: Updated {path}");
                }
            }

            AssetDatabase.SaveAssets();

            if (migratedAssets > 0)
            {
                Debug.Log($"MazeGrid Migration complete: {migratedCells} cells in {migratedAssets} assets converted from GridWall to Invalid.");
                EditorUtility.DisplayDialog("Migration Complete",
                    $"Migrated {migratedCells} cells in {migratedAssets} assets.\n\nGridWall → Invalid",
                    "OK");
            }
            else
            {
                Debug.Log("MazeGrid Migration: No GridWall cells found. Nothing to migrate.");
                EditorUtility.DisplayDialog("Migration Complete",
                    "No GridWall cells found. All levels are already up to date.",
                    "OK");
            }
        }
    }
}
