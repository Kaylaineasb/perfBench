using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PerfBench.EditorTools
{
    /// <summary>
    /// Menu PerfBench → monta tudo: materiais, perfis LEVE/MÉDIO/PESADO, config,
    /// objeto PerfBench na cena aberta, ajustes do URP (Forward+, sombras) e Player Settings.
    /// Pode ser executado de novo sem duplicar nada (perfis existentes não são sobrescritos).
    /// </summary>
    public static class PerfBenchSetup
    {
        const string Root = "Assets/PerfBench/Generated";

        [MenuItem("PerfBench/1. Configurar projeto e cena aberta")]
        public static void SetupAll()
        {
            EnsureFolder(Root);
            var stress = MakeMaterial("PerfBench/StressLit", "PB_StressLit", true);
            var overdraw = MakeMaterial("PerfBench/Overdraw", "PB_Overdraw", false);
            var particle = MakeMaterial("PerfBench/Particle", "PB_Particle", false);

            var light = GetOrCreateProfile("light", "LEVE", FillLight);
            var medium = GetOrCreateProfile("medium", "MÉDIO", FillMedium);
            var heavy = GetOrCreateProfile("heavy", "PESADO", FillHeavy);

            var cfgPath = Root + "/BenchmarkConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<BenchmarkConfig>(cfgPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<BenchmarkConfig>();
                cfg.profiles = new List<ScenarioProfile> { light, medium, heavy };
                AssetDatabase.CreateAsset(cfg, cfgPath);
            }

            SetupScene(cfg, stress, overdraw, particle);
            SetupUrp();
            SetupPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[PerfBench] Configuração concluída. Salve a cena (Ctrl+S). Veja o Console para avisos.");
        }

        [MenuItem("PerfBench/2. Recriar perfis com valores padrão")]
        public static void ResetProfiles()
        {
            if (!EditorUtility.DisplayDialog("PerfBench", "Sobrescrever LEVE/MÉDIO/PESADO com os valores padrão?", "Sim", "Cancelar")) return;
            Fill(GetOrCreateProfile("light", "LEVE", FillLight), FillLight);
            Fill(GetOrCreateProfile("medium", "MÉDIO", FillMedium), FillMedium);
            Fill(GetOrCreateProfile("heavy", "PESADO", FillHeavy), FillHeavy);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Perfis padrão (ponto de partida para calibração) ----------------

        static void FillLight(ScenarioProfile p)
        {
            p.worldSpeed = 15; p.obstacleSpacing = 30;
            p.renderMegapixels = 0.9f; p.msaa = 1; p.hdr = false;
            p.shaderAluIterations = 4;
            p.instancedCount = 300; p.instancedMeshSegments = 12; p.instancedCastShadows = true;
            p.dynamicObjectCount = 20; p.dynamicObjectsBlink = true;
            p.overdrawLayers = 0; p.overdrawAluIterations = 4; p.particleMax = 0; p.particleRate = 0;
            p.pointLights = 0; p.shadowedSpotLights = 0;
            p.mainShadowResolution = 1024; p.shadowCascades = 1; p.softShadows = false; p.shadowDistance = 40; p.additionalShadowAtlasResolution = 1024;
            p.bloom = false; p.bloomHighQuality = false; p.depthOfField = false; p.motionBlur = false; p.colorGrading = false;
            p.virtualCameraResolution = 0; p.virtualCameraInterval = 1;
            p.cpuJobElements = 0; p.cpuJobIterations = 64; p.physicsBodies = 0; p.gcPressureMBps = 0;
        }

        static void FillMedium(ScenarioProfile p)
        {
            p.worldSpeed = 25; p.obstacleSpacing = 15;
            p.renderMegapixels = 2.1f; p.msaa = 2; p.hdr = true;
            p.shaderAluIterations = 32;
            p.instancedCount = 2000; p.instancedMeshSegments = 16; p.instancedCastShadows = true;
            p.dynamicObjectCount = 200; p.dynamicObjectsBlink = true;
            p.overdrawLayers = 4; p.overdrawAluIterations = 8; p.particleMax = 2000; p.particleRate = 800;
            p.pointLights = 8; p.shadowedSpotLights = 0;
            p.mainShadowResolution = 2048; p.shadowCascades = 2; p.softShadows = true; p.shadowDistance = 60; p.additionalShadowAtlasResolution = 2048;
            p.bloom = true; p.bloomHighQuality = false; p.depthOfField = false; p.motionBlur = false; p.colorGrading = true;
            p.virtualCameraResolution = 1024; p.virtualCameraInterval = 4;
            p.cpuJobElements = 65536; p.cpuJobIterations = 64; p.physicsBodies = 150; p.gcPressureMBps = 2;
        }

        static void FillHeavy(ScenarioProfile p)
        {
            p.worldSpeed = 35; p.obstacleSpacing = 10;
            p.renderMegapixels = 3.7f; p.msaa = 4; p.hdr = true;
            p.shaderAluIterations = 128;
            p.instancedCount = 10000; p.instancedMeshSegments = 16; p.instancedCastShadows = true;
            p.dynamicObjectCount = 800; p.dynamicObjectsBlink = true;
            p.overdrawLayers = 12; p.overdrawAluIterations = 16; p.particleMax = 10000; p.particleRate = 4000;
            p.pointLights = 28; p.shadowedSpotLights = 4;
            p.mainShadowResolution = 4096; p.shadowCascades = 4; p.softShadows = true; p.shadowDistance = 80; p.additionalShadowAtlasResolution = 4096;
            p.bloom = true; p.bloomHighQuality = true; p.depthOfField = true; p.motionBlur = true; p.colorGrading = true;
            p.virtualCameraResolution = 2048; p.virtualCameraInterval = 1;
            p.cpuJobElements = 262144; p.cpuJobIterations = 64; p.physicsBodies = 600; p.gcPressureMBps = 8;
        }

        static void Fill(ScenarioProfile p, System.Action<ScenarioProfile> fill)
        {
            fill(p);
            EditorUtility.SetDirty(p);
        }

        static ScenarioProfile GetOrCreateProfile(string id, string display, System.Action<ScenarioProfile> fill)
        {
            string path = $"{Root}/Scenario_{id}.asset";
            var p = AssetDatabase.LoadAssetAtPath<ScenarioProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<ScenarioProfile>();
            p.id = id;
            p.displayName = display;
            fill(p);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        // ---------------- Materiais ----------------

        static Material MakeMaterial(string shaderName, string assetName, bool instancing)
        {
            string path = $"{Root}/{assetName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[PerfBench] Shader '{shaderName}' não encontrado (erro de compilação?)."); return null; }
            m = new Material(shader) { enableInstancing = instancing };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------- Cena ----------------

        static void SetupScene(BenchmarkConfig cfg, Material stress, Material overdraw, Material particle)
        {
            var scene = SceneManager.GetActiveScene();
            GameObject playerGo = FindRoot(scene, "player");
            GameObject spawner = FindRoot(scene, "ObstacleSpawner");
            GameObject pista = FindRoot(scene, "pista");
            GameObject oldVolume = FindRoot(scene, "Global Volume");

            // Remove componentes de scripts antigos apagados (Missing Script)
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            if (spawner != null) spawner.SetActive(false);
            if (pista != null) pista.SetActive(false);
            if (oldVolume != null) oldVolume.SetActive(false);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(new Vector3(0, 6, -9), Quaternion.Euler(20, 0, 0));
                cam.fieldOfView = 70;
                cam.farClipPlane = 260;
                cam.allowHDR = true;
                cam.allowMSAA = true;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);
                sun.shadows = LightShadows.Soft;
            }

            var runnerGo = FindRoot(scene, "PerfBench");
            if (runnerGo == null)
            {
                runnerGo = new GameObject("PerfBench");
                Undo.RegisterCreatedObjectUndo(runnerGo, "PerfBench");
            }
            runnerGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var runner = GetOrAdd<BenchmarkRunner>(runnerGo);
            GetOrAdd<WorldSimulation>(runnerGo);
            GetOrAdd<RenderQualityModule>(runnerGo);
            GetOrAdd<InstancedFieldModule>(runnerGo);
            GetOrAdd<DynamicObjectsModule>(runnerGo);
            GetOrAdd<OverdrawModule>(runnerGo);
            GetOrAdd<ParticleModule>(runnerGo);
            GetOrAdd<LightsModule>(runnerGo);
            GetOrAdd<VirtualCameraModule>(runnerGo);
            GetOrAdd<CpuJobModule>(runnerGo);
            GetOrAdd<PhysicsModule>(runnerGo);
            GetOrAdd<GcPressureModule>(runnerGo);
            var metrics = GetOrAdd<MetricsRecorder>(runnerGo);

            var so = new SerializedObject(runner);
            SetRef(so, "config", cfg);
            SetRef(so, "mainCamera", cam);
            SetRef(so, "mainLight", sun);
            SetRef(so, "player", playerGo != null ? playerGo.transform : null);
            SetRef(so, "stressLitMaterial", stress);
            SetRef(so, "overdrawMaterial", overdraw);
            SetRef(so, "particleMaterial", particle);
            so.ApplyModifiedProperties();

            var mso = new SerializedObject(metrics);
            SetRef(mso, "runner", runner);
            mso.ApplyModifiedProperties();

            if (playerGo == null) Debug.LogWarning("[PerfBench] Objeto 'player' não encontrado: o personagem não será exibido.");
            if (sun == null) Debug.LogWarning("[PerfBench] Nenhuma Directional Light encontrada.");
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[PerfBench] Campo '{prop}' não encontrado.");
        }

        // ---------------- URP ----------------

        static void SetupUrp()
        {
            var assets = new HashSet<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null) assets.Add(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var a = QualitySettings.GetRenderPipelineAssetAt(i);
                if (a != null) assets.Add(a);
            }

            foreach (var asset in assets)
            {
                if (!(asset is UniversalRenderPipelineAsset)) continue;
                var so = new SerializedObject(asset);
                SetInt(so, "m_AdditionalLightsRenderingMode", 1);   // Per Pixel
                SetBool(so, "m_MainLightShadowsSupported", true);
                SetBool(so, "m_AdditionalLightShadowsSupported", true);
                SetBool(so, "m_SoftShadowsSupported", true);
                SetBool(so, "m_SupportsHDR", true);
                SetInt(so, "m_GPUResidentDrawerMode", 0);          // desligado: carga não pode se adaptar sozinha
                so.ApplyModifiedProperties();

                var list = so.FindProperty("m_RendererDataList");
                if (list == null || !list.isArray) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var rd = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (rd == null) continue;
                    var rso = new SerializedObject(rd);
                    SetInt(rso, "m_RenderingMode", 2);             // Forward+
                    rso.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rd);
                }
                EditorUtility.SetDirty(asset);
                Debug.Log($"[PerfBench] URP ajustado: {asset.name}");
            }
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = v; else Debug.LogWarning($"[PerfBench] URP: '{name}' não encontrado em {so.targetObject.name} (ajuste manual).");
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v; else Debug.LogWarning($"[PerfBench] URP: '{name}' não encontrado em {so.targetObject.name} (ajuste manual).");
        }

        // ---------------- Player Settings (release) ----------------

        static void SetupPlayer()
        {
            PlayerSettings.enableFrameTimingStats = true;                 // necessário p/ FrameTimingManager (CPU/GPU ms)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = false;          // Swappy não deve limitar o FPS
            EditorUserBuildSettings.development = false;

            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android).Contains("template"))
                Debug.LogWarning("[PerfBench] O Package Name ainda é o do template. Defina um id próprio em Player Settings → Identification.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
