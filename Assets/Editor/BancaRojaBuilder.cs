using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BancaRojaBuilder
{
    private const string PrefabPath = "Assets/Prefabs/BancaRoja.prefab";
    private const string MaterialPath = "Assets/Prefabs/BancaRoja_RojoMetalico.mat";
    private const string MeshFolder = "Assets/Prefabs/Generated";
    private const float SeatHeight = 0.47f;
    private const int TubeRadialSegments = 10;
    private static readonly string[] GeneratedFolders =
    {
        "Assets/Prefabs",
        MeshFolder
    };

    [MenuItem("HORROR-UTEZ/Props/Crear o actualizar banca roja")]
    public static void BuildInOpenScene()
    {
        EnsureFolders();
        Material material = GetOrCreateMaterial();
        RemoveExistingSceneRoots();

        GameObject root = CreateBench(material);
        Undo.RegisterCreatedObjectUndo(root, "Crear banca roja");
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
        if (!success)
            throw new InvalidOperationException("Unity no pudo guardar el prefab en " + PrefabPath);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(root.transform.position + Vector3.up * 0.48f,
                Quaternion.Euler(16f, 28f, 0f), 2.7f);
        Debug.Log("[BancaRojaBuilder] Banca generada en la escena activa y prefab actualizado: " + PrefabPath);
    }

    public static void BuildPrefabForBatch()
    {
        EnsureFolders();
        Material material = GetOrCreateMaterial();
        GameObject root = CreateBench(material);
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
        UnityEngine.Object.DestroyImmediate(root);
        if (!success)
            throw new InvalidOperationException("Unity no pudo guardar el prefab en " + PrefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AuditPrefab();
        Debug.Log("[BancaRojaBuilder] Prefab generado: " + PrefabPath);
    }

    public static void BuildPrefabForBatchAndExit()
    {
        try
        {
            BuildPrefabForBatch();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void AuditPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            BoxCollider collider = root.GetComponent<BoxCollider>();
            int seatSlats = 0;
            int backSlats = 0;
            foreach (Transform child in root.transform)
            {
                if (child.name.StartsWith("Asiento - tablilla", StringComparison.Ordinal))
                    seatSlats++;
                if (child.name.StartsWith("Respaldo - tablilla", StringComparison.Ordinal))
                    backSlats++;
            }

            if (seatSlats != 9 || backSlats != 4)
                throw new InvalidOperationException("La banca debe tener 9 tablillas de asiento y 4 de respaldo.");
            if (filters.Length != 18 || renderers.Length != 18 || collider == null)
                throw new InvalidOperationException("Faltan mallas, renderers o collider en el prefab.");
            foreach (MeshFilter filter in filters)
                if (filter.sharedMesh == null)
                    throw new InvalidOperationException("Malla faltante: " + filter.name);
            foreach (MeshRenderer renderer in renderers)
                if (renderer.sharedMaterial == null)
                    throw new InvalidOperationException("Material faltante: " + renderer.name);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.x < 1.7f || bounds.size.x > 1.9f ||
                bounds.size.y < 0.8f || bounds.size.y > 1.05f ||
                bounds.size.z < 0.58f || bounds.size.z > 0.75f)
                throw new InvalidOperationException("Dimensiones fuera de rango: " + bounds.size);

            Debug.Log("[BancaRojaBuilder] PASS: 9 tablillas de asiento, 4 de respaldo, " +
                filters.Length + " mallas, materiales y collider. Bounds " + bounds.size);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolders()
    {
        for (int i = 0; i < GeneratedFolders.Length; i++)
        {
            string folder = GeneratedFolders[i];
            if (AssetDatabase.IsValidFolder(folder))
                continue;

            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("No se encontró el shader Universal Render Pipeline/Lit.");

            material = new Material(shader);
            material.name = "BancaRoja_RojoMetalico";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetColor("_BaseColor", new Color(0.66f, 0.035f, 0.045f, 1f));
        material.SetFloat("_Metallic", 0.62f);
        material.SetFloat("_Smoothness", 0.38f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveExistingSceneRoots()
    {
        List<GameObject> matches = new List<GameObject>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == "BancaRoja")
                matches.Add(roots[i]);

        for (int i = 0; i < matches.Count; i++)
            Undo.DestroyObjectImmediate(matches[i]);
    }

    private static GameObject CreateBench(Material material)
    {
        GameObject root = new GameObject("BancaRoja");
        root.AddComponent<BoxCollider>().size = new Vector3(1.8f, 1.02f, 0.68f);
        root.GetComponent<BoxCollider>().center = new Vector3(0f, 0.51f, 0f);

        Mesh seatSlat = SaveMesh("BancaRoja_TablillaAsiento", CreateRoundedSlat(1.76f, 0.043f, 0.025f, 0.004f, 0.004f));
        Mesh backSlat = SaveMesh("BancaRoja_TablillaRespaldo", CreateRoundedSlat(1.76f, 0.057f, 0.026f, 0.004f, 0.002f));
        Mesh leftFrame = SaveMesh("BancaRoja_EstructuraLateralIzquierda", CreateTube(
            CreateSideFramePath(-1f), 0.019f, 0.014f));
        Mesh rightFrame = SaveMesh("BancaRoja_EstructuraLateralDerecha", CreateTube(
            CreateSideFramePath(1f), 0.019f, 0.014f));
        Mesh crossbar = SaveMesh("BancaRoja_Travesano", CreateTube(
            new[] { new Vector3(-0.79f, 0.43f, 0f), new Vector3(0.79f, 0.43f, 0f) },
            0.014f, 0.014f));

        for (int i = 0; i < 9; i++)
        {
            float z = -0.19f + i * 0.052f;
            float cup = 0.003f * (1f - Mathf.Abs(z) / 0.23f);
            GameObject slat = AddPart(root.transform, "Asiento - tablilla " + (i + 1).ToString("00"),
                seatSlat, material);
            slat.transform.localPosition = new Vector3(0f, SeatHeight + cup, z);
        }

        for (int i = 0; i < 4; i++)
        {
            float y = 0.665f + i * 0.095f;
            float z = -0.235f - i * 0.017f;
            GameObject slat = AddPart(root.transform, "Respaldo - tablilla " + (i + 1).ToString("00"),
                backSlat, material);
            slat.transform.localPosition = new Vector3(0f, y, z);
            slat.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
        }

        AddPart(root.transform, "Estructura lateral izquierda - tubo curvo", leftFrame, material);
        AddPart(root.transform, "Estructura lateral derecha - tubo curvo", rightFrame, material);

        for (int i = 0; i < 3; i++)
        {
            GameObject bar = AddPart(root.transform, "Soporte transversal " + (i + 1).ToString("00"),
                crossbar, material);
            bar.transform.localPosition = new Vector3(0f, 0f, -0.16f + i * 0.18f);
        }

        return root;
    }

    private static GameObject AddPart(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return part;
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.name = name;
        string path = MeshFolder + "/" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        EditorUtility.CopySerialized(mesh, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        return existing;
    }

    private static Vector3[] CreateSideFramePath(float side)
    {
        float x = side * 0.82f;
        return new[]
        {
            new Vector3(x, 0.04f, -0.27f),
            new Vector3(x, 0.13f, -0.28f),
            new Vector3(x, 0.31f, -0.255f),
            new Vector3(x, 0.46f, -0.21f),
            new Vector3(x, 0.48f, -0.10f),
            new Vector3(x, 0.48f, 0.12f),
            new Vector3(x, 0.38f, 0.245f),
            new Vector3(x, 0.20f, 0.31f),
            new Vector3(x, 0.06f, 0.32f),
            new Vector3(x, 0.035f, 0.32f),
            new Vector3(x, 0.10f, 0.28f),
            new Vector3(x, 0.20f, 0.26f),
            new Vector3(x, 0.31f, 0.24f),
            new Vector3(x, 0.43f, 0.20f),
            new Vector3(x, 0.53f, 0.16f),
            new Vector3(x, 0.58f, 0.05f),
            new Vector3(x, 0.64f, -0.08f),
            new Vector3(x, 0.72f, -0.20f),
            new Vector3(x, 0.89f, -0.28f),
            new Vector3(x, 0.96f, -0.31f)
        };
    }

    private static Mesh CreateRoundedSlat(float length, float width, float thickness, float bevel, float crown)
    {
        const int lengthSegments = 16;
        Vector2[] section = CreateBeveledRectangle(width, thickness, bevel);
        Vector3[] vertices = new Vector3[(lengthSegments + 1) * section.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[lengthSegments * section.Length * 6 + (section.Length - 2) * 6];

        for (int xIndex = 0; xIndex <= lengthSegments; xIndex++)
        {
            float t = (float)xIndex / lengthSegments;
            float x = (t - 0.5f) * length;
            float crownOffset = crown * (1f - 4f * (t - 0.5f) * (t - 0.5f));
            for (int ring = 0; ring < section.Length; ring++)
            {
                int index = xIndex * section.Length + ring;
                vertices[index] = new Vector3(x, section[ring].y + crownOffset, section[ring].x);
                uv[index] = new Vector2(t, (section[ring].x / width) + 0.5f);
            }
        }

        int triangle = 0;
        for (int xIndex = 0; xIndex < lengthSegments; xIndex++)
        {
            for (int ring = 0; ring < section.Length; ring++)
            {
                int next = (ring + 1) % section.Length;
                int a = xIndex * section.Length + ring;
                int b = (xIndex + 1) * section.Length + ring;
                int c = (xIndex + 1) * section.Length + next;
                int d = xIndex * section.Length + next;
                triangles[triangle++] = a;
                triangles[triangle++] = c;
                triangles[triangle++] = b;
                triangles[triangle++] = a;
                triangles[triangle++] = d;
                triangles[triangle++] = c;
            }
        }

        AddEndCap(triangles, ref triangle, 0, section.Length, false);
        AddEndCap(triangles, ref triangle, lengthSegments * section.Length, section.Length, true);
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2[] CreateBeveledRectangle(float width, float thickness, float bevel)
    {
        float halfWidth = width * 0.5f;
        float halfThickness = thickness * 0.5f;
        bevel = Mathf.Min(bevel, halfWidth * 0.5f, halfThickness * 0.5f);
        return new[]
        {
            new Vector2(-halfWidth + bevel, -halfThickness),
            new Vector2(halfWidth - bevel, -halfThickness),
            new Vector2(halfWidth, -halfThickness + bevel),
            new Vector2(halfWidth, halfThickness - bevel),
            new Vector2(halfWidth - bevel, halfThickness),
            new Vector2(-halfWidth + bevel, halfThickness),
            new Vector2(-halfWidth, halfThickness - bevel),
            new Vector2(-halfWidth, -halfThickness + bevel)
        };
    }

    private static void AddEndCap(int[] triangles, ref int offset, int start, int count, bool reverse)
    {
        for (int i = 1; i < count - 1; i++)
        {
            triangles[offset++] = start;
            triangles[offset++] = start + (reverse ? i + 1 : i);
            triangles[offset++] = start + (reverse ? i : i + 1);
        }
    }

    private static Mesh CreateTube(IList<Vector3> controlPoints, float halfWidth, float halfDepth)
    {
        const int curveSegmentsPerSpan = 5;
        List<Vector3> path = SampleCatmullRom(controlPoints, curveSegmentsPerSpan);
        int ringCount = path.Count;
        int vertexCount = ringCount * TubeRadialSegments;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[(ringCount - 1) * TubeRadialSegments * 6 + (TubeRadialSegments - 2) * 6];
        Vector3 startTangent = (path[1] - path[0]).normalized;
        Vector3 endTangent = (path[path.Count - 1] - path[path.Count - 2]).normalized;

        for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
        {
            int pathIndex = ringIndex;
            Vector3 center = path[pathIndex];
            Vector3 tangent = pathIndex == 0 ? startTangent :
                pathIndex == path.Count - 1 ? endTangent :
                (path[pathIndex + 1] - path[pathIndex - 1]).normalized;
            Vector3 axis = Mathf.Abs(Vector3.Dot(tangent, Vector3.right)) > 0.9f ? Vector3.up : Vector3.right;
            Vector3 side = Vector3.Cross(tangent, axis).normalized;
            Vector3 across = Vector3.Cross(side, tangent).normalized;
            for (int radial = 0; radial < TubeRadialSegments; radial++)
            {
                float angle = radial * (Mathf.PI * 2f / TubeRadialSegments);
                int index = ringIndex * TubeRadialSegments + radial;
                vertices[index] = center + side * (Mathf.Cos(angle) * halfWidth) +
                                  across * (Mathf.Sin(angle) * halfDepth);
                uv[index] = new Vector2(radial / (float)TubeRadialSegments, ringIndex / (float)(ringCount - 1));
            }
        }

        int triangle = 0;
        for (int ringIndex = 0; ringIndex < ringCount - 1; ringIndex++)
        {
            for (int radial = 0; radial < TubeRadialSegments; radial++)
            {
                int next = (radial + 1) % TubeRadialSegments;
                int a = ringIndex * TubeRadialSegments + radial;
                int b = ringIndex * TubeRadialSegments + next;
                int c = (ringIndex + 1) * TubeRadialSegments + next;
                int d = (ringIndex + 1) * TubeRadialSegments + radial;
                triangles[triangle++] = a;
                triangles[triangle++] = b;
                triangles[triangle++] = c;
                triangles[triangle++] = a;
                triangles[triangle++] = c;
                triangles[triangle++] = d;
            }
        }

        AddTubeCap(triangles, ref triangle, 0, false);
        AddTubeCap(triangles, ref triangle, (ringCount - 1) * TubeRadialSegments, true);
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTubeCap(int[] triangles, ref int offset, int start, bool reverse)
    {
        for (int i = 1; i < TubeRadialSegments - 1; i++)
        {
            triangles[offset++] = start;
            triangles[offset++] = start + (reverse ? i + 1 : i);
            triangles[offset++] = start + (reverse ? i : i + 1);
        }
    }

    private static List<Vector3> SampleCatmullRom(IList<Vector3> points, int segmentsPerSpan)
    {
        List<Vector3> samples = new List<Vector3>();
        for (int span = 0; span < points.Count - 1; span++)
        {
            Vector3 p0 = points[Mathf.Max(0, span - 1)];
            Vector3 p1 = points[span];
            Vector3 p2 = points[span + 1];
            Vector3 p3 = points[Mathf.Min(points.Count - 1, span + 2)];
            for (int segment = 0; segment < segmentsPerSpan; segment++)
            {
                float t = (float)segment / segmentsPerSpan;
                float t2 = t * t;
                float t3 = t2 * t;
                samples.Add(0.5f * ((2f * p1) + (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        samples.Add(points[points.Count - 1]);
        return samples;
    }

}
