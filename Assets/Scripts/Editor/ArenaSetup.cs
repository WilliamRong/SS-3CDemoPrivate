using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ArenaSetup
{
    [MenuItem("Tools/Arena Setup")]
    public static void CreateArena()
    {
        var arena = new GameObject("Arena");
        Undo.RegisterCreatedObjectUndo(arena, "Create Arena");

        // 地面 20x20
        CreateBlock("Ground", arena.transform,
            new Vector3(0f, -0.05f, 0f),
            new Vector3(20f, 0.1f, 20f),
            new Color(0.4f, 0.4f, 0.4f));

        // 四面墙
        CreateBlock("Wall_North", arena.transform,
            new Vector3(0f, 1f, 10.25f),
            new Vector3(20.5f, 2f, 0.5f),
            new Color(0.55f, 0.55f, 0.55f));

        CreateBlock("Wall_South", arena.transform,
            new Vector3(0f, 1f, -10.25f),
            new Vector3(20.5f, 2f, 0.5f),
            new Color(0.55f, 0.55f, 0.55f));

        CreateBlock("Wall_East", arena.transform,
            new Vector3(10.25f, 1f, 0f),
            new Vector3(0.5f, 2f, 20.5f),
            new Color(0.55f, 0.55f, 0.55f));

        CreateBlock("Wall_West", arena.transform,
            new Vector3(-10.25f, 1f, 0f),
            new Vector3(0.5f, 2f, 20.5f),
            new Color(0.55f, 0.55f, 0.55f));

        // 障碍物
        CreateBlock("Obstacle_1", arena.transform,
            new Vector3(4f, 0.75f, 3f),
            new Vector3(2f, 1.5f, 2f),
            new Color(0.6f, 0.5f, 0.4f));

        CreateBlock("Obstacle_2", arena.transform,
            new Vector3(-3f, 0.5f, -4f),
            new Vector3(1.5f, 1f, 3f),
            new Color(0.6f, 0.5f, 0.4f));

        Selection.activeGameObject = arena;
        EditorSceneManager.MarkSceneDirty(arena.scene);
        Debug.Log("Arena created! 20x20 ground + 4 walls + 2 obstacles.");
    }

    static void CreateBlock(string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        go.GetComponent<Renderer>().material = mat;
    }
}
