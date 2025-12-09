#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FindMissingScripts
{
    [MenuItem("Tools/Utility/Find Missing Scripts In Scene")]
    public static void FindInScene()
    {
        int goCount = 0, missingCount = 0;
        var gos = Object.FindObjectsOfType<GameObject>();
        foreach (var g in gos)
        {
            goCount++;
            var components = g.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missingCount++;
                    Debug.Log($"Missing script in GameObject: {GetFullPath(g)} (index {i})", g);
                }
            }
        }

        Debug.Log($"Checked {goCount} GameObjects, found {missingCount} missing components");
    }

    private static string GetFullPath(GameObject g)
    {
        string path = g.name;
        Transform t = g.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
#endif
