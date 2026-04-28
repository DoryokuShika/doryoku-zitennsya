using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts (Open Scenes)")]
    public static void RemoveMissingScriptsInOpenScenes()
    {
        if (!EditorUtility.DisplayDialog(
                "Remove Missing Scripts",
                "現在開いているシーン内の Missing Script を削除します。実行しますか？",
                "実行",
                "キャンセル"))
            return;

        int removedCount = 0;
        int objectCount = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                removedCount += RemoveMissingScriptsRecursively(roots[i], ref objectCount);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MissingScriptCleaner] Open scenes: removed {removedCount} missing scripts from {objectCount} objects.");
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts (All Prefabs)")]
    public static void RemoveMissingScriptsInAllPrefabs()
    {
        if (!EditorUtility.DisplayDialog(
                "Remove Missing Scripts",
                "Assets配下の全Prefab内の Missing Script を削除します。実行しますか？",
                "実行",
                "キャンセル"))
            return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int removedCount = 0;
        int prefabCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                int before = removedCount;
                int dummyCount = 0;
                removedCount += RemoveMissingScriptsRecursively(root, ref dummyCount);

                if (removedCount > before)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[MissingScriptCleaner] Prefabs: removed {removedCount} missing scripts in {prefabCount} prefabs.");
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts (Selection)")]
    public static void RemoveMissingScriptsInSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.Log("[MissingScriptCleaner] Selection is empty.");
            return;
        }

        int removedCount = 0;
        int objectCount = 0;

        for (int i = 0; i < selected.Length; i++)
            removedCount += RemoveMissingScriptsRecursively(selected[i], ref objectCount);

        AssetDatabase.SaveAssets();
        Debug.Log($"[MissingScriptCleaner] Selection: removed {removedCount} missing scripts from {objectCount} objects.");
    }

    static int RemoveMissingScriptsRecursively(GameObject root, ref int objectCount)
    {
        int removed = 0;
        if (root == null)
            return removed;

        var stack = new Stack<Transform>();
        stack.Push(root.transform);

        while (stack.Count > 0)
        {
            Transform t = stack.Pop();
            objectCount++;
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }

        return removed;
    }
}
