using HorrorUtez.World;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Deterministic scenery shared by host and clients; authored campus transforms are preserved.
    public static class RemakeCampusExpansion
    {
        public static void Build(RemakeGame game)
        {
            Furnish(game, "CDS", UtezDimensions.Cds, false);
            Furnish(game, "Auditorium", UtezDimensions.Auditorium, true);
            var props = new GameObject("REPO · equipo autorizado del campus").transform;
            RemakeRepoMaps.CampusProp("Trash_Bin_1", new Vector3(17, .1f, 8), 1, game.PropMaterial, props);
            RemakeRepoMaps.CampusProp("Computer_Horizontal", new Vector3(-7, .1f, 10), .5f, game.PropMaterial, props);
            RemakeRepoMaps.CampusProp("Valuable_Arctic_Server_Rack", new Vector3(-10, .1f, 27), 1.8f, game.PropMaterial, props);
            foreach (Vector3 p in new[] { new Vector3(-27, .1f, -10), new Vector3(30, .1f, 25), new Vector3(-28, .1f, 31), new Vector3(28, .1f, -42) })
            {
                Prop(props, "Lab_Bench", p);
                Prop(props, "Lab_Trash", p + Vector3.right * 2.5f);
                Prop(props, "Lab_Papers", p + Vector3.forward * 1.5f, 35);
            }
            Forest(game);
            foreach (var weather in Object.FindObjectsByType<WeatherSystem>(FindObjectsSortMode.None)) weather.NightLightScale = .5f;
        }

        private static GameObject Prop(Transform root, string model, Vector3 position, float yaw = 0)
        {
            var prefab = Resources.Load<GameObject>("Lab/" + model);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, root);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (Transform child in go.GetComponentsInChildren<Transform>()) child.gameObject.layer = 11;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * .02f);
            }
            return go;
        }

        private static void Furnish(RemakeGame game, string name, UtezDimensions.Footprint fp, bool auditorium)
        {
            var structure = GameObject.Find("UTEZ_Buildings/" + name + "_Structure");
            if (structure == null) { Debug.LogWarning("[Remake] Missing expansion building " + name); return; }
            var root = new GameObject(name + " · interior nocturno").transform;
            root.SetParent(structure.transform, false);
            float w = fp.ScaledSize.x * .5f, depth = fp.ScaledSize.y * .5f;
            if (!auditorium) OpenCdsEntrance(game, structure.transform, w, depth);
            if (auditorium)
            {
                // Central aisle and two side aisles remain open from the north entrance to the stage.
                for (float z = -depth + 5; z < depth - 3; z += 1.6f)
                    foreach (float x in new[] { -2.8f, -1.5f, 1.5f, 2.8f })
                        Prop(root, "Lab_Chair", new Vector3(x, .2f, z), 180);
                Solid(root, "Escenario", new Vector3(0, .3f, -depth + 2), new Vector3(w * 1.6f, .35f, 3), game.PropMaterial);
                Prop(root, "Lab_TeacherDesk", new Vector3(-2, .5f, -depth + 2), 180);
                Prop(root, "Lab_Rack", new Vector3(w - 1, .2f, -depth + 1));
                Prop(root, "Lab_Whiteboard", new Vector3(0, 1.1f, -depth + .5f), 180);
            }
            else
            {
                // Office partitions have generous door gaps onto the central corridor.
                foreach (float side in new[] { -1f, 1f })
                    for (float z = -depth + 4; z < depth - 4; z += 6)
                    {
                        Solid(root, "División de oficina", new Vector3(side * 2.1f, 1.6f, z + 1), new Vector3(.12f, 2.8f, 2), game.PropMaterial);
                        Solid(root, "División de oficina", new Vector3(side * 2.1f, 1.6f, z + 4.6f), new Vector3(.12f, 2.8f, 1.2f), game.PropMaterial);
                        Solid(root, "Muro de despacho", new Vector3(side * (w + 2.1f) * .5f, 1.6f, z), new Vector3(w - 2.1f, 2.8f, .12f), game.PropMaterial);
                    }
                // Open offices with transverse passages and a broad central corridor.
                Rect stairwell = UtezDimensions.StairwellLocal(fp);
                stairwell.xMin -= 1; stairwell.xMax += 1; stairwell.yMin -= 1; stairwell.yMax += 1;
                for (float z = -depth + 3; z < depth - 2; z += 3.5f)
                    foreach (float x in new[] { -w + 2, -w + 5, w - 5, w - 2 })
                    {
                        if (stairwell.Contains(new Vector2(x, z))) continue;
                        Prop(root, "Lab_Desk", new Vector3(x, .2f, z));
                        Prop(root, "Lab_Chair", new Vector3(x, .2f, z + .9f), (int)(x * z) % 40);
                        Prop(root, "Lab_CRT", new Vector3(x, .98f, z), 180);
                        Prop(root, "Lab_TowerOpen", new Vector3(x + .65f, .2f, z));
                    }
                for (float x = -w + 2; x < w - 1; x += 2)
                    if (!stairwell.Contains(new Vector2(x, depth - .7f))) Prop(root, "Lab_Shelf", new Vector3(x, .2f, depth - .7f));
                Prop(root, "Lab_Rack", new Vector3(-w + 1, .2f, depth - 2));
            }
            for (int i = 0; i < 12; i++)
                Prop(root, "Lab_Papers", new Vector3(Mathf.Sin(i * 7) * (w - 1), .22f, Mathf.Cos(i * 3) * (depth - 2)), i * 31);
            // Low intensity guidance; flashlight remains useful.
            var lamp = new GameObject("Luz de emergencia"); lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0, 2.6f, 0);
            var light = lamp.AddComponent<Light>(); light.range = 13; light.intensity = .8f;
            light.color = new Color(.5f, .65f, .58f); light.shadows = LightShadows.None;
            Debug.Log("[Remake] Furnished " + name + ": " + root.GetComponentsInChildren<Renderer>().Length + " renderers");
            game.Dressing.RoomCentres[name + " recepción"] = root.TransformPoint(new Vector3(0, .2f, 0));
            if (!auditorium)
            {
                var upper = Object.Instantiate(root.gameObject, structure.transform);
                upper.name = "CDS · oficinas de planta alta";
                upper.transform.localPosition = Vector3.up * UtezDimensions.FloorHeight;
            }
        }

        private static void Solid(Transform parent, string name, Vector3 p, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void OpenCdsEntrance(RemakeGame game, Transform structure, float halfWidth, float depth)
        {
            // The authored storefront's door is offset from the old generic shell opening.
            // Align the invisible shell behind it without altering the landmark or the saved scene.
            Vector3 hinges = Vector3.zero; int count = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "Door_CDSWest" || t.name == "Door_CDSEast") { hinges += t.position; count++; }
            if (count != 2) { Debug.LogWarning("[Remake] CDS storefront hinges missing"); return; }
            float x = structure.InverseTransformPoint(hinges / count).x;
            Material wall = game.PropMaterial;
            foreach (Transform t in structure)
                if (t.name.StartsWith("Wall_South_Jamb") || t.name.StartsWith("Wall_South_Lintel"))
                {
                    if (t.TryGetComponent(out Renderer renderer)) wall = renderer.sharedMaterial;
                    t.gameObject.SetActive(false);
                }
            const float halfDoor = 1.15f;
            float z = -depth + UtezDimensions.WallThickness * .5f;
            Solid(structure, "CDS · muro oeste de entrada", new Vector3((-halfWidth + x - halfDoor) * .5f, 2, z), new Vector3(x - halfDoor + halfWidth, 4, UtezDimensions.WallThickness), wall);
            Solid(structure, "CDS · muro este de entrada", new Vector3((halfWidth + x + halfDoor) * .5f, 2, z), new Vector3(halfWidth - x - halfDoor, 4, UtezDimensions.WallThickness), wall);
            Solid(structure, "CDS · dintel de entrada", new Vector3(x, 3.3f, z), new Vector3(halfDoor * 2, 1.4f, UtezDimensions.WallThickness), wall);
            Solid(structure, "CDS · paso sobre jardinera", new Vector3(x, .37f, -depth - .7f), new Vector3(2.25f, .08f, 2.3f), game.PropMaterial);
            Solid(structure, "CDS · descanso interior", new Vector3(x, .15f, -depth + .45f), new Vector3(2.25f, .30f, .9f), game.PropMaterial);
            // Keep the short threshold and the raised planted approach traversable in the grid.
            game.Dressing.ExtraDoorways.Add(new Bounds(hinges / count + Vector3.up, new Vector3(2.4f, 3, 5)));
            Debug.Log("[Remake] CDS shell entrance aligned at local x " + x);
        }

        private static void Forest(RemakeGame game)
        {
            var existing = GameObject.Find("UTEZ_Forest");
            if (existing == null) return;
            var sources = existing.GetComponentsInChildren<MeshFilter>();
            var root = new GameObject("Bosque · sotobosque denso").transform;
            var random = new System.Random(20261005);
            Vector2 half = UtezDimensions.DirtSize * .5f, slab = UtezDimensions.SlabSize * .5f, centre = UtezDimensions.SlabCenter;
            int count = 0;
            for (int i = 0; i < 650 && sources.Length > 0; i++)
            {
                float x = ((float)random.NextDouble() * 2 - 1) * half.x;
                float z = ((float)random.NextDouble() * 2 - 1) * half.y;
                if (Mathf.Abs(x) < slab.x + 2 && Mathf.Abs(z) < slab.y + 2) continue;
                var source = sources[random.Next(sources.Length)];
                if (!source.TryGetComponent(out MeshRenderer renderer)) continue;
                // Reuse the project's existing PSX forest meshes and materials, without duplicating assets.
                var go = new GameObject("Vegetación " + count++); go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x + centre.x, source.transform.position.y, z + centre.y);
                go.transform.rotation = Quaternion.Euler(0, random.Next(360), 0);
                go.transform.localScale = source.transform.lossyScale * Mathf.Lerp(.55f, 1.25f, (float)random.NextDouble());
                go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.intensity *= .45f;
            RenderSettings.ambientIntensity *= .55f;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.018f, .028f, .025f);
            if (!Application.isMobilePlatform) StaticBatchingUtility.Combine(root.gameObject);
            Debug.Log("[Remake] Added forest scenery: " + count);
        }
    }
}
