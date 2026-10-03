using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HorrorUtez.Rendering;
using HorrorUtez.Rendering.Editor;

namespace HorrorUtez.Remake.Editor
{
    public sealed class RemakeModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/_Project/Art/Remake/",StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;model.globalScale=1;model.useFileScale=true;
            model.bakeAxisConversion=true;model.importAnimation=false;model.importCameras=false;model.importLights=false;
            model.addCollider=false;model.meshCompression=ModelImporterMeshCompression.Off;
            // Lab dressing is static-batched at runtime, which needs CPU-readable meshes.
            model.isReadable=assetPath.Contains("/Resources/Lab/");
        }
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/_Project/Art/Remake/Textures/",StringComparison.Ordinal))return;
            var texture=(TextureImporter)assetImporter;texture.filterMode=FilterMode.Point;texture.mipmapEnabled=true;
            texture.textureCompression=TextureImporterCompression.Uncompressed;texture.wrapMode=TextureWrapMode.Repeat;texture.sRGBTexture=true;
        }
        private void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/_Project/Art/Remake/Resources/Audio/",StringComparison.Ordinal))return;
            var audio=(AudioImporter)assetImporter;var settings=audio.defaultSampleSettings;
            bool music=Path.GetFileName(assetPath).StartsWith("music_")||Path.GetFileName(assetPath).StartsWith("amb_");
            settings.loadType=music?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=music?.6f:.8f;
            audio.defaultSampleSettings=settings;audio.forceToMono=true;
        }
        private void OnPostprocessModel(GameObject root)
        {
            if(!assetPath.StartsWith("Assets/_Project/Art/Remake/",StringComparison.Ordinal))return;
            foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Material[] materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null)continue;
                    Material replacement=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Remake/Materials/"+materials[i].name+".mat");
                    if(replacement!=null)materials[i]=replacement;
                }
                renderer.sharedMaterials=materials;
            }
        }
    }
    public static class RemakeBuild
    {
        public const string Scene="Assets/_Project/Scenes/remake.unity";
        private const string Art="Assets/_Project/Art/Remake/";
        [MenuItem("HORROR-UTEZ/Remake/Open playable scene")]
        public static void OpenPlayable()
        {
            EditorSceneManager.OpenScene(Scene);
            Debug.Log("[RemakeBuild] Opened remake.unity. Press Play for the new menu and game.");
        }
        [MenuItem("HORROR-UTEZ/Remake/Prepare playable scene")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Art+"Materials");AssetDatabase.Refresh();
            string[] names={"Chalk","Ink","Steel","Oxblood","Amber","Screen","Glass","Skin","Eye","Wood"};
            Color[] colors={new Color(.73f,.72f,.65f),new Color(.035f,.048f,.052f),new Color(.22f,.27f,.28f),
                new Color(.38f,.045f,.035f),new Color(.92f,.51f,.08f),new Color(.08f,.7f,.56f),
                new Color(.08f,.19f,.22f),new Color(.57f,.34f,.24f),new Color(1,.08f,.02f),new Color(.39f,.23f,.11f)};
            for(int i=0;i<names.Length;i++)
            {
                string path=Art+"Materials/"+names[i]+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetColor("_BaseColor",colors[i]);mat.SetFloat("_Smoothness",i==2?.5f:.2f);
                if(names[i]=="Screen"||names[i]=="Eye"||names[i]=="Amber")
                {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",colors[i]*1.5f);}
                EditorUtility.SetDirty(mat);
            }
            // One PSX/Lit material per generated texture (Tools/blender/gen_remake_textures.py); names match the
            // material slots in every remake FBX, which RemakeModelImporter remaps to these assets.
            foreach(string png in Directory.GetFiles(Art+"Textures","*.png"))
            {
                string name=Path.GetFileNameWithoutExtension(png);
                string texturePath=png.Replace(Path.DirectorySeparatorChar,'/');
                AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceUpdate);
                string path=Art+"Materials/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(PsxMaterialDefaults.LitShader);AssetDatabase.CreateAsset(mat,path);}
                if(mat.shader!=PsxMaterialDefaults.LitShader)mat.shader=PsxMaterialDefaults.LitShader;
                mat.SetTexture(PsxShaderProperties.MainTex,AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                PsxMaterialDefaults.Apply(mat,SurfaceKind(name),false);
            }
            AssetDatabase.SaveAssets();
            foreach(string path in Directory.GetFiles(Art,"*.fbx",SearchOption.AllDirectories))AssetDatabase.ImportAsset(path.Replace(Path.DirectorySeparatorChar,'/'),ImportAssetOptions.ForceUpdate);
            // Failed .blend imports produce empty assets. Repair them once Blender is available.
            foreach(string path in Directory.GetFiles("Assets/_Project/Art/Environment","*.blend",SearchOption.AllDirectories))
            {
                string assetPath=path.Replace('\\','/');
                if(!AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>().Any())AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceUpdate);
                if(!AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>().Any())throw new Exception("Blender import failed: "+assetPath);
            }
            SyncPlayerPrefab();
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/utez.unity");
            EditorSceneManager.SaveScene(scene,Scene,true);
            scene=EditorSceneManager.OpenScene(Scene);
            GameObject old=GameObject.Find("Player");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            foreach(GameObject sceneRoot in scene.GetRootGameObjects())
                foreach(Transform node in sceneRoot.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            foreach(Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.enabled=false;
            foreach(AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))listener.enabled=false;
            foreach(MonoBehaviour script in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(script.GetType().Name=="PauseMenu"||script.GetType().Name=="PauseController")script.enabled=false;
            var root=new GameObject("REMAKE · Turno nocturno");var game=root.AddComponent<RemakeGame>();
            game.StudentModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCharacter.prefab");
            game.TruckModel=Model("Truck");game.EnemyModel=Model("Caretaker");game.GiantModel=Model("Giant");
            game.LootModels=new[]{Model("Laptop"),Model("Projector"),Model("Microscope"),Model("Workstation"),Model("UPS"),Model("Printer"),Model("Cart"),Model("Oscilloscope"),Model("Router")};
            game.ScreenMaterials=new[]{"Screen_Dead","Screen_Cracked","Screen_BSOD","Screen_Terminal","Screen_Static"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+n+".mat")).ToArray();
            Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+n+".mat");
            game.PropMaterial=Mat("Steel");game.SkinMaterial=Mat("Skin");game.SleeveMaterial=Mat("Oxblood");game.SignalMaterial=Mat("Amber");
            var tagAsset=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tagAsset.FindProperty("layers");layers.GetArrayElementAtIndex(9).stringValue="RemakeStudent";
            layers.GetArrayElementAtIndex(10).stringValue="RemakeLoot";tagAsset.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new Exception("URP is required.");
            pipeline.renderScale=1;pipeline.shadowDistance=32;pipeline.mainLightShadowmapResolution=1024;
            EditorUtility.SetDirty(pipeline);
            var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Project/Settings/UTEZ_URP_Renderer.asset");
            foreach(ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                string shaderName=feature.GetType().Name=="PsxFogRenderFeature"?"HorrorUtez/PSX/Fog":
                    feature.GetType().Name=="PsxScreenRenderFeature"?"HorrorUtez/PSX/Screen":null;
                if(shaderName==null)continue;
                var serialized=new SerializedObject(feature);serialized.FindProperty("shader").objectReferenceValue=Shader.Find(shaderName);
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(rendererData);
            PlayerSettings.companyName="UTEZ";PlayerSettings.productName="HORROR-UTEZ · Turno nocturno";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"mx.utez.horror.remake");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("[RemakeBuild] Scene ready: "+Scene+". URP + Windows + Android controls.");
            foreach(string name in new[]{"Truck","Laptop","Caretaker","Giant"})
            {
                var probe=UnityEngine.Object.Instantiate(Model(name));
                var bounds=new Bounds(probe.transform.position,Vector3.zero);
                foreach(Renderer renderer in probe.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
                Debug.Log("[RemakeBuild] Model "+name+" bounds "+bounds+" root rotation "+probe.transform.eulerAngles);
                foreach(Transform child in probe.GetComponentsInChildren<Transform>())
                    if(child.name=="Cab"||child.name=="CargoFloor"||child.name=="Windshield")Debug.Log("[RemakeBuild] "+child.name+" world "+child.position);
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }
        // The player prefab is an unpacked copy of PlayerCharacter.fbx with its own SkinnedMeshRenderers. When the
        // body mesh is regenerated (Tools/blender/gen_player_body_v3.py) its submesh and bone order can change, so
        // copy materials, bones (matched by name) and bounds from the freshly imported FBX.
        [MenuItem("HORROR-UTEZ/Remake/Sync player prefab with FBX")]
        public static void SyncPlayerPrefab()
        {
            const string prefabPath="Assets/_Project/Prefabs/Player/PlayerCharacter.prefab";
            const string fbxPath="Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx";
            AssetDatabase.ImportAsset(fbxPath,ImportAssetOptions.ForceUpdate);
            var fbx=AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if(fbx==null||!File.Exists(prefabPath)){Debug.LogWarning("[RemakeBuild] Player prefab or FBX missing.");return;}
            GameObject root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform Find(Transform t,string name){if(t.name==name)return t;foreach(Transform c in t){Transform f=Find(c,name);if(f!=null)return f;}return null;}
                foreach(SkinnedMeshRenderer target in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    SkinnedMeshRenderer source=fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>s.name==target.name||s.sharedMesh==target.sharedMesh);
                    if(source==null)continue;
                    target.sharedMesh=source.sharedMesh;target.sharedMaterials=source.sharedMaterials;
                    target.bones=source.bones.Select(b=>b==null?null:Find(root.transform,b.name)).ToArray();
                    if(source.rootBone!=null)target.rootBone=Find(root.transform,source.rootBone.name);
                    target.localBounds=source.localBounds;
                    Debug.Log("[RemakeBuild] Synced "+target.name+": "+target.sharedMaterials.Length+" materials, "+target.bones.Length+" bones");
                }
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static PsxSurfaceKind SurfaceKind(string name)
        {
            if(name.StartsWith("LED_")||name=="Eye_Glow"||name=="Screen_BSOD"||name=="Screen_Terminal"||name=="Screen_Static")return PsxSurfaceKind.Emissive;
            if(name.StartsWith("Screen_"))return PsxSurfaceKind.Gloss;
            if(name=="Chrome"||name=="Metal_Brushed")return PsxSurfaceKind.Metal;
            if(name.StartsWith("Glass"))return PsxSurfaceKind.Glass;
            if(name=="Whiteboard"||name.StartsWith("Poster")||name.StartsWith("Sign_")||name=="Paper"||name=="Keyboard"||name=="Caution_Tape")return PsxSurfaceKind.Text;
            if(name.StartsWith("Plastic")||name.StartsWith("Laminate")||name.StartsWith("Truck")||name=="Metal_Painted"||name=="Sneaker")return PsxSurfaceKind.Satin;
            return PsxSurfaceKind.Matte;
        }
        private static GameObject Model(string name)
        {
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".fbx");
            if(model==null)throw new Exception("Missing model "+name);return model;
        }
        [MenuItem("HORROR-UTEZ/Remake/Build Windows")]
        public static void Windows()
        {
            if(!File.Exists(Scene))Prepare();
            Directory.CreateDirectory("Builds/Remake/Windows");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},
                locationPathName="Builds/Remake/Windows/HORROR-UTEZ.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build: "+report.summary.result);
            Debug.Log("[RemakeBuild] Windows ready. "+report.summary.totalSize+" bytes.");
        }
        [MenuItem("HORROR-UTEZ/Remake/Build Android")]
        public static void Android()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("Install Android Build Support (SDK, NDK, OpenJDK) for this Unity editor.");
            Directory.CreateDirectory("Builds/Remake/Android");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},
                locationPathName="Builds/Remake/Android/HORROR-UTEZ.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android build: "+report.summary.result);
        }
        public static void PrepareAndBuild(){Prepare();Windows();}
    }
}
