using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.Remake
{
    // Static, authorized map modules adapted to our own runtime; no original scripts are executed.
    public static class RemakeRepoMaps
    {
        public static readonly string[] Names = { "CAMPUS UTEZ", "MANSIÓN · R.E.P.O.", "MUSEO · R.E.P.O.", "ÁRTICO · R.E.P.O." };
        private static readonly string[] Keys = { "", "Manor", "Museum", "Arctic" };
        private static readonly string[][] Layouts = { Array.Empty<string>(), new[] { "ManorKitchen", "Manor", "ManorRooms" },
            new[] { "MuseumColumn", "Museum", "MuseumRoundabout" }, new[] { "ArcticWarehouse", "Arctic", "ArcticLounge" } };
        [Serializable] private sealed class Module { public string source; public Node[] nodes; }
        [Serializable] private sealed class Node
        {
            public string name, mesh; public int parent;
            public Vector3 position, scale; public Quaternion rotation;
            public Surface[] materials; public Collision[] colliders;
        }
        [Serializable] private sealed class Surface { public string name, texture; public Color color; }
        [Serializable] private sealed class Collision { public string kind, mesh; public Vector3 center, size; public float radius, height; public int direction; }
        [Serializable] private sealed class Geometry { public Vector3[] vertices; public Vector2[] uv; public Submesh[] submeshes; }
        [Serializable] private sealed class Submesh { public int[] triangles; }
        [Serializable] private sealed class PropData { public string mesh, source; }
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static bool Available(int map)
        {
            if (map == 0) return true;
            if (map < 0 || map >= Keys.Length) return false;
            foreach (string key in Layouts[map]) if (Resources.Load<TextAsset>("Repo/" + key) == null) return false;
            return true;
        }

        private static Mesh GeometryMesh(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Meshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            var asset = Resources.Load<TextAsset>("Repo/" + key);
            if (asset == null) throw new InvalidOperationException("Missing REPO geometry " + key);
            var data = JsonUtility.FromJson<Geometry>(asset.text);
            mesh = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = data.vertices;
            if (data.uv != null && data.uv.Length == data.vertices.Length) mesh.uv = data.uv;
            mesh.subMeshCount = data.submeshes.Length;
            for (int i = 0; i < data.submeshes.Length; i++) mesh.SetTriangles(data.submeshes[i].triangles, i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); Meshes[key] = mesh;
            return mesh;
        }

        private static Material SurfaceMaterial(Surface surface, Material fallback)
        {
            string key = surface.name + ":" + surface.texture + ":" + surface.color;
            if (Materials.TryGetValue(key, out Material mat) && mat != null) return mat;
            mat = new Material(fallback) { name = "REPO · " + surface.name };
            Texture2D texture = string.IsNullOrEmpty(surface.texture) ? null : Resources.Load<Texture2D>("Repo/" + surface.texture);
            mat.color = surface.color;
            if (texture != null) { texture.filterMode = FilterMode.Point; mat.mainTexture = texture; }
            Materials[key] = mat;
            return mat;
        }

        public static GameObject Build(int map, Material fallback)
        {
            var root = new GameObject("Expedición REPO · " + Keys[map]);
            for (int i = 0; i < Layouts[map].Length; i++)
            {
                var module = BuildModule(Layouts[map][i], fallback);
                module.transform.SetParent(root.transform, false);
                module.transform.localPosition = new Vector3((i - 1) * 17, .22f, -32);
            }
            return root;
        }

        private static GameObject BuildModule(string key, Material fallback)
        {
            var module = JsonUtility.FromJson<Module>(Resources.Load<TextAsset>("Repo/" + key).text);
            var root = new GameObject("Sector · " + key);
            var transforms = new Transform[module.nodes.Length];
            for (int i = 0; i < module.nodes.Length; i++)
            {
                Node node = module.nodes[i]; var go = new GameObject(node.name);
                transforms[i] = go.transform;
                go.transform.SetParent(node.parent < 0 ? root.transform : transforms[node.parent], false);
                go.transform.localPosition = node.position; go.transform.localRotation = node.rotation; go.transform.localScale = node.scale;
                Mesh mesh = GeometryMesh(node.mesh);
                if (mesh != null)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    var mats = new Material[mesh.subMeshCount];
                    for (int j = 0; j < mats.Length; j++) mats[j] = node.materials.Length > 0 ? SurfaceMaterial(node.materials[Mathf.Min(j, node.materials.Length - 1)], fallback) : fallback;
                    renderer.sharedMaterials = mats;
                }
                foreach (Collision c in node.colliders)
                {
                    if (c.kind == "BoxCollider") { var box = go.AddComponent<BoxCollider>(); box.center = c.center; box.size = c.size; }
                    else if (c.kind == "MeshCollider" && !string.IsNullOrEmpty(c.mesh)) go.AddComponent<MeshCollider>().sharedMesh = GeometryMesh(c.mesh);
                    else if (c.kind == "SphereCollider") { var sphere = go.AddComponent<SphereCollider>(); sphere.center = c.center; sphere.radius = c.radius; }
                    else if (c.kind == "CapsuleCollider") { var capsule = go.AddComponent<CapsuleCollider>(); capsule.center = c.center; capsule.radius = c.radius; capsule.height = c.height; capsule.direction = c.direction; }
                }
            }
            var lamp = new GameObject("Luz de emergencia del sector"); lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0, 3, 0); var light = lamp.AddComponent<Light>();
            light.range = 22; light.intensity = 1.4f; light.color = new Color(.6f, .7f, .65f);
            Debug.Log("[Remake] REPO module loaded " + module.source + " · " + module.nodes.Length + " nodes");
            return root;
        }

        public static void CampusProp(string key, Vector3 position, float height, Material mat, Transform parent)
        {
            var asset = Resources.Load<TextAsset>("Repo/prop_" + key);
            if (asset == null) return;
            var data = JsonUtility.FromJson<PropData>(asset.text); var mesh = GeometryMesh(data.mesh);
            var go = new GameObject("REPO · " + data.source); go.transform.SetParent(parent, false); go.transform.position = position;
            go.transform.localScale = Vector3.one * (height / Mathf.Max(.01f, mesh.bounds.size.y));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var mats = new Material[mesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            renderer.sharedMaterials = mats;
            var box = go.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size;
            go.transform.position -= Vector3.up * mesh.bounds.min.y * go.transform.localScale.y;
        }
    }
}
