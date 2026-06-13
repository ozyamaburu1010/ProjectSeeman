using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.UI;
using Seeman;

public static class SeemanSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Aquarium.unity";
    const string FbxPath = "Assets/Models/HumanFish.fbx";

    [MenuItem("Seeman/Build Aquarium Scene")]
    public static void Build()
    {
        EnsureFbxLoop();
        var controller = EnsureAnimator();
        var crtMat = EnsureCrtMaterial();
        var pelletMat = EnsureSimpleMat("Assets/Materials/Pellet.mat", new Color(0.45f, 0.3f, 0.12f), 0.2f);
        EnsureRendererFeature(crtMat);
        var volumeProfile = EnsureVolumeProfile();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- environment ----------
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.035f, 0.16f, 0.18f);
        RenderSettings.fogDensity = 0.30f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.24f, 0.26f);

        // ---------- camera ----------
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.transform.position = new Vector3(0f, 0.55f, -1.35f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.05f, 0.06f);
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camGo.AddComponent<CameraInteraction>();
        var crtCtl = camGo.AddComponent<CRTController>();
        crtCtl.CrtMaterial = crtMat;
        var narratorVoice = camGo.AddComponent<AudioSource>();
        narratorVoice.playOnAwake = false;
        narratorVoice.spatialBlend = 0f;

        // ---------- lights ----------
        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.85f;
        sun.color = new Color(0.75f, 0.91f, 1f);
        sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

        var caustic = new GameObject("CausticSpot").AddComponent<Light>();
        caustic.type = LightType.Spot;
        caustic.transform.position = new Vector3(0f, 2.0f, 0.25f);
        caustic.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        caustic.spotAngle = 75f;
        caustic.range = 4.5f;
        caustic.intensity = 3.6f;
        caustic.color = new Color(0.62f, 1f, 0.88f);
        var causticCtl = caustic.gameObject.AddComponent<CausticLight>();
        causticCtl.BaseIntensity = 3.6f;
        causticCtl.FlickerAmount = 0.7f;

        var face = new GameObject("FaceLight").AddComponent<Light>();
        face.type = LightType.Point;
        face.transform.position = new Vector3(0f, 0.75f, -0.7f);
        face.range = 2.8f;
        face.intensity = 1.6f;
        face.color = new Color(1f, 0.88f, 0.7f);

        // ---------- tank ----------
        var tank = new GameObject("Tank");
        var gravelMat = EnsureGravelMaterial();
        var rockMat = EnsureSimpleMat("Assets/Materials/Rock.mat", new Color(0.22f, 0.23f, 0.25f), 0.1f);
        var plantMat = EnsureSimpleMat("Assets/Materials/Plant.mat", new Color(0.05f, 0.3f, 0.1f), 0.3f);
        var backMat = EnsureSimpleMat("Assets/Materials/TankBack.mat", new Color(0.02f, 0.1f, 0.12f), 0.05f);

        MakeBox(tank.transform, "Floor", new Vector3(0f, 0.03f, 0.25f), new Vector3(3.2f, 0.07f, 1.9f), gravelMat);
        MakeBox(tank.transform, "BackWall", new Vector3(0f, 0.8f, 1.0f), new Vector3(3.2f, 1.7f, 0.06f), backMat);
        MakeBox(tank.transform, "LeftWall", new Vector3(-1.55f, 0.8f, 0.25f), new Vector3(0.06f, 1.7f, 1.9f), backMat);
        MakeBox(tank.transform, "RightWall", new Vector3(1.55f, 0.8f, 0.25f), new Vector3(0.06f, 1.7f, 1.9f), backMat);

        // rocks
        MakeBlob(tank.transform, "Rock1", new Vector3(-0.85f, 0.12f, 0.55f), new Vector3(0.5f, 0.22f, 0.4f), rockMat);
        MakeBlob(tank.transform, "Rock2", new Vector3(0.95f, 0.1f, 0.4f), new Vector3(0.35f, 0.18f, 0.3f), rockMat);
        MakeBlob(tank.transform, "Rock3", new Vector3(0.3f, 0.08f, 0.8f), new Vector3(0.28f, 0.14f, 0.25f), rockMat);

        // plants (stretched capsules)
        var rnd = new System.Random(7);
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-1.3f, 1.3f, (float)rnd.NextDouble());
            float z = Mathf.Lerp(0.45f, 0.9f, (float)rnd.NextDouble());
            float h = Mathf.Lerp(0.35f, 0.8f, (float)rnd.NextDouble());
            var plant = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            plant.name = "Plant" + i;
            Object.DestroyImmediate(plant.GetComponent<Collider>());
            plant.transform.SetParent(tank.transform, false);
            plant.transform.position = new Vector3(x, h * 0.5f + 0.05f, z);
            plant.transform.localScale = new Vector3(0.05f + 0.04f * (float)rnd.NextDouble(), h * 0.5f, 0.05f);
            plant.transform.rotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 16f, 0, ((float)rnd.NextDouble() - 0.5f) * 16f);
            plant.GetComponent<MeshRenderer>().sharedMaterial = plantMat;
        }

        // ---------- bubbles ----------
        BuildBubbles(tank.transform);

        // ---------- fish ----------
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var fishRoot = new GameObject("FishRoot");
        fishRoot.transform.position = new Vector3(0.4f, 0.6f, 0.4f);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model.name = "HumanFishModel";
        model.transform.SetParent(fishRoot.transform, false);
        model.transform.localScale = Vector3.one * 0.45f;
        // the FBX visually faces -X; rotate so the visual forward matches +Z
        model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        var anim = model.GetComponentInChildren<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var fishCtl = fishRoot.AddComponent<FishController>();
        fishCtl.CamDistance = 1.12f;
        var voice = fishRoot.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        var lip = fishRoot.AddComponent<FishLipSync>();
        lip.Voice = voice;

        // ---------- managers ----------
        var mgr = new GameObject("GameManager");
        mgr.AddComponent<GameManager>();
        mgr.AddComponent<PersonalitySystem>();
        mgr.AddComponent<LocalAIClient>();
        mgr.AddComponent<ConversationManager>();
        mgr.AddComponent<MicrophoneRecorder>();
        var feeding = mgr.AddComponent<FeedingSystem>();
        feeding.PelletMaterial = pelletMat;
        mgr.AddComponent<SidecarLauncher>();
        var narrator = mgr.AddComponent<NarratorSystem>();
        narrator.Voice = narratorVoice;

        // ---------- UI ----------
        var canvasGo = new GameObject("GameCanvas");
        canvasGo.AddComponent<GameUI>();

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();

        // ---------- volume ----------
        var volGo = new GameObject("GlobalVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = volumeProfile;

        // ---------- save ----------
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[Seeman] Aquarium scene built and saved: " + ScenePath);
    }

    static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void MakeBlob(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void BuildBubbles(Transform parent)
    {
        var go = new GameObject("Bubbles");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 0.1f, 0.45f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.022f);
        main.startSpeed = 0f;
        main.startLifetime = 7f;
        main.startColor = new Color(0.8f, 0.95f, 1f, 0.55f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 6f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.6f, 0.02f, 1.0f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // all three axes must use the same curve mode, or Unity logs
        // "Particle Velocity curves must all be in the same mode" every frame
        vel.x = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
        vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.025f;
        noise.frequency = 0.6f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = EnsureBubbleMaterial();
    }

    // ---------- assets ----------

    static void EnsureFbxLoop()
    {
        var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (imp == null) { Debug.LogError("[Seeman] FBX not found at " + FbxPath); return; }
        var clips = imp.clipAnimations != null && imp.clipAnimations.Length > 0
            ? imp.clipAnimations : imp.defaultClipAnimations;
        bool changed = false;
        foreach (var c in clips)
        {
            if (!c.loopTime) { c.loopTime = true; changed = true; }
        }
        if (changed)
        {
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    static RuntimeAnimatorController EnsureAnimator()
    {
        const string path = "Assets/Animations/Fish.controller";
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null) return existing;

        var swim = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var state = controller.layers[0].stateMachine.AddState("Swim");
        state.motion = swim;
        return controller;
    }

    static Material EnsureCrtMaterial()
    {
        const string path = "Assets/Materials/CRT.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Seeman/CRT");
            if (shader == null) { Debug.LogError("[Seeman] CRT shader not found"); return null; }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static Material EnsureSimpleMat(string path, Color color, float smooth)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static Material EnsureGravelMaterial()
    {
        const string texPath = "Assets/Materials/GravelTex.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var rnd = new System.Random(42);
            var colors = new Color[256 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float v = 0.13f + (float)rnd.NextDouble() * 0.14f;
                    float warm = (float)rnd.NextDouble() * 0.05f;
                    colors[y * 256 + x] = new Color(v + warm, v * 0.95f, v * 0.8f);
                }
            // sprinkle lighter pebbles
            for (int i = 0; i < 350; i++)
            {
                int cx = rnd.Next(2, 253), cy = rnd.Next(2, 253), r = rnd.Next(1, 4);
                float bright = 0.3f + (float)rnd.NextDouble() * 0.25f;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r)
                            colors[(cy + dy) * 256 + cx + dx] = new Color(bright, bright * 0.93f, bright * 0.8f);
            }
            tex.SetPixels(colors);
            tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
        }
        var mat2 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gravel.mat");
        if (mat2 == null)
        {
            mat2 = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat2.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat2.SetFloat("_Smoothness", 0.15f);
            mat2.SetTextureScale("_BaseMap", new Vector2(4f, 3f));
            AssetDatabase.CreateAsset(mat2, "Assets/Materials/Gravel.mat");
        }
        return mat2;
    }

    static Material EnsureBubbleMaterial()
    {
        const string texPath = "Assets/Materials/BubbleSprite.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var colors = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) * 5f) * 0.9f;
                    float fill = Mathf.Clamp01(1f - d) * 0.25f;
                    float a = Mathf.Clamp01(ring + fill);
                    colors[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(colors);
            tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bubble.mat");
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            mat = new Material(shader);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Bubble.mat");
        }
        return mat;
    }

    static VolumeProfile EnsureVolumeProfile()
    {
        const string path = "Assets/Settings/SeemanVolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.7f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.8f;

        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.overrideState = true;
        ca.saturation.value = 10f;
        ca.postExposure.overrideState = true;
        ca.postExposure.value = 0.15f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static void EnsureRendererFeature(Material crtMat)
    {
        foreach (var rendererPath in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (data == null) continue;
            if (data.rendererFeatures.Any(f => f != null && f.name == "SeemanCRTFeature"))
            {
                // keep material up to date
                var existing = data.rendererFeatures.First(f => f != null && f.name == "SeemanCRTFeature") as FullScreenPassRendererFeature;
                if (existing != null && existing.passMaterial != crtMat)
                {
                    existing.passMaterial = crtMat;
                    EditorUtility.SetDirty(existing);
                }
                continue;
            }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "SeemanCRTFeature";
            feature.passMaterial = crtMat;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Color;
            // fetchColorBuffer exists on newer URP versions
            var fcb = typeof(FullScreenPassRendererFeature).GetField("fetchColorBuffer");
            if (fcb != null) fcb.SetValue(feature, true);

            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(data);
            var featProp = serialized.FindProperty("m_RendererFeatures");
            int idx = featProp.arraySize;
            featProp.arraySize++;
            featProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;
            var mapProp = serialized.FindProperty("m_RendererFeatureMap");
            if (mapProp != null)
            {
                mapProp.arraySize++;
                string guid;
                long localId;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);
                mapProp.GetArrayElementAtIndex(idx).longValue = localId;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }
}
