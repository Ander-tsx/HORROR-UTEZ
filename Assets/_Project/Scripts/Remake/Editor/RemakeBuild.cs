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
    // Import conventions for pieces dropped into Assets/_Project/Art/Remake (see Documentation/Remake/BLENDER.md).
    public sealed class RemakeModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/_Project/Art/Remake/",StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;model.globalScale=1;model.useFileScale=true;
            model.bakeAxisConversion=true;model.importAnimation=false;model.importCameras=false;model.importLights=false;
            model.addCollider=false;model.meshCompression=ModelImporterMeshCompression.Off;
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
        // A material slot named like a material in Art/Remake/Materials uses that material automatically.
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

    // Builds. The scenes are authored by hand in the editor and are never regenerated here.
    public static class RemakeBuild
    {
        public const string Scene="Assets/_Project/Scenes/remake.unity";
        public const string ShopScene="Assets/_Project/Scenes/remake_shop.unity";
        private const string Art="Assets/_Project/Art/Remake/";

        [MenuItem("HORROR-UTEZ/Abrir escena jugable",priority=0)]
        public static void OpenPlayable()
        {
            EditorSceneManager.OpenScene(Scene);
            Debug.Log("[RemakeBuild] Opened remake.unity. Press Play for the menu and game.");
        }

        // Project-level settings the game depends on (layers, URP, shaders kept in builds, player settings, scene list).
        // Safe to run any time: it does not open or modify scenes, prefabs or materials.
        [MenuItem("HORROR-UTEZ/Reparar configuración del proyecto")]
        public static void EnsureProjectSettings()
        {
            var tagAsset=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tagAsset.FindProperty("layers");layers.GetArrayElementAtIndex(9).stringValue="RemakeStudent";
            layers.GetArrayElementAtIndex(10).stringValue="RemakeLoot";tagAsset.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new Exception("URP is required.");
            pipeline.renderScale=1;pipeline.shadowDistance=32;pipeline.mainLightShadowmapResolution=1024;
            EditorUtility.SetDirty(pipeline);
            // Fog/screen shaders must be referenced by the renderer features or the player build strips them.
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
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true),new EditorBuildSettingsScene(ShopScene,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("[RemakeBuild] Project settings OK.");
        }

        // Creates a PSX/Lit material for every texture in Art/Remake/Textures that has none yet. Existing materials
        // are left untouched, so hand-tuned materials survive. Name a model's material slot like the texture to use it.
        [MenuItem("HORROR-UTEZ/Crear materiales PSX para texturas nuevas")]
        public static void CreateMissingMaterials()
        {
            Directory.CreateDirectory(Art+"Materials");
            int created=0;
            foreach(string png in Directory.GetFiles(Art+"Textures","*.png"))
            {
                string name=Path.GetFileNameWithoutExtension(png);
                string path=Art+"Materials/"+name+".mat";
                if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)continue;
                var mat=new Material(PsxMaterialDefaults.LitShader);
                mat.SetTexture(PsxShaderProperties.MainTex,AssetDatabase.LoadAssetAtPath<Texture2D>(png.Replace(Path.DirectorySeparatorChar,'/')));
                PsxMaterialDefaults.Apply(mat,SurfaceKind(name),false);
                AssetDatabase.CreateAsset(mat,path);created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[RemakeBuild] Created "+created+" materials.");
        }

        // The player prefab is an unpacked copy of PlayerCharacter.fbx with its own SkinnedMeshRenderers. When the
        // body FBX is re-exported its submesh and bone order can change, so copy materials, bones (by name) and bounds.
        [MenuItem("HORROR-UTEZ/Sincronizar prefab del estudiante con su FBX")]
        public static void SyncPlayerPrefab()
        {
            const string prefabPath="Assets/_Project/Prefabs/Player/PlayerCharacter.prefab";
            const string fbxPath="Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbxPath);
            if(!importer.importBlendShapes){importer.importBlendShapes=true;importer.SaveAndReimport();}
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

        [MenuItem("HORROR-UTEZ/Build/Windows")]
        public static void Windows()
        {
            EnsureProjectSettings();
            Directory.CreateDirectory("Builds/Remake/Windows");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,ShopScene},
                locationPathName="Builds/Remake/Windows/HORROR-UTEZ.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build: "+report.summary.result);
            Debug.Log("[RemakeBuild] Windows ready. "+report.summary.totalSize+" bytes.");
        }
        // macOS from Windows: Mono backend (IL2CPP cannot cross-compile to macOS), universal Intel + Apple Silicon.
        // The app is unsigned; on the Mac it must be allowed once (see Documentation/Remake/PLAY.md).
        [MenuItem("HORROR-UTEZ/Build/macOS")]
        public static void Mac()
        {
            EnsureProjectSettings();
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneOSX))
                throw new Exception("Install Mac Build Support (Mono) for this Unity editor.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"mx.utez.horror.remake");
            // UserBuildSettings lives in the Mac module's editor assembly; set it by reflection so this file compiles without it.
            Type settings=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.OSXStandalone.UserBuildSettings")).FirstOrDefault(t=>t!=null);
            var architecture=settings?.GetProperty("architecture");
            if(architecture!=null)
            {
                object universal=Enum.GetValues(architecture.PropertyType).Cast<object>().FirstOrDefault(v=>v.ToString().Contains("ARM64")&&v.ToString().Contains("64")&&v.ToString().Length>5);
                if(universal!=null)architecture.SetValue(null,universal);
                Debug.Log("[RemakeBuild] macOS architecture "+architecture.GetValue(null));
            }
            // Voice chat asks for the microphone; macOS kills apps that do so without a usage description.
            foreach(Type nested in typeof(PlayerSettings).GetNestedTypes())
            {
                var description=nested.GetProperty("microphoneUsageDescription");
                if(description!=null&&description.CanWrite)description.SetValue(null,"Chat de voz por proximidad con los demás estudiantes.");
            }
            Directory.CreateDirectory("Builds/Remake/Mac");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,ShopScene},
                locationPathName="Builds/Remake/Mac/HORROR-UTEZ.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("macOS build: "+report.summary.result);
            Debug.Log("[RemakeBuild] macOS ready. "+report.summary.totalSize+" bytes.");
        }
        [MenuItem("HORROR-UTEZ/Build/Android")]
        public static void Android()
        {
            EnsureProjectSettings();
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("Install Android Build Support (SDK, NDK, OpenJDK) for this Unity editor.");
            Directory.CreateDirectory("Builds/Remake/Android");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,ShopScene},
                locationPathName="Builds/Remake/Android/HORROR-UTEZ.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android build: "+report.summary.result);
        }
        public static void WindowsAndMac(){Windows();Mac();}
    }
}
