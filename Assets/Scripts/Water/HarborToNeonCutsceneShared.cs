using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PacificRimAtmosphere;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Shared IK, letterbox/fade UI, and procedural VFX particle utilities for the two-part
    /// Harbor -> Hong Kong Neon Phase 3 transition cutscene.
    /// </summary>
    internal static class HarborToNeonCutsceneShared
    {
        public class LetterboxRig
        {
            public Canvas Canvas;
            public CanvasGroup Group;
            public RectTransform TopBar;
            public RectTransform BottomBar;
            public Image FadeOverlay;
            public Image FlashOverlay;
            public Text SkipText;
        }

        private static Texture2D softCircleTex;
        private static Texture2D sparkStreakTex;

        public static LetterboxRig CreateLetterboxAndFadeUI(string objName, float barHeightPercent = 0.115f, float initialBlackAlpha = 1f)
        {
            var rig = new LetterboxRig();
            GameObject go = new GameObject(objName);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 550;
            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 1f;

            // Black fade image below letterbox bars so letterbox bars stay crisp
            GameObject fadeGo = new GameObject("FadeOverlay");
            fadeGo.transform.SetParent(go.transform, false);
            Image fade = fadeGo.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, initialBlackAlpha);
            fade.raycastTarget = false;
            RectTransform fr = fade.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;

            // Additive-feel white/orange impact flash overlay
            GameObject flashGo = new GameObject("ImpactFlashOverlay");
            flashGo.transform.SetParent(go.transform, false);
            Image flash = flashGo.AddComponent<Image>();
            flash.color = new Color(1f, 0.78f, 0.55f, 0f);
            flash.raycastTarget = false;
            RectTransform flr = flash.rectTransform;
            flr.anchorMin = Vector2.zero;
            flr.anchorMax = Vector2.one;
            flr.offsetMin = Vector2.zero;
            flr.offsetMax = Vector2.zero;

            // Top letterbox bar
            GameObject topGo = new GameObject("Letterbox_Top");
            topGo.transform.SetParent(go.transform, false);
            Image topImg = topGo.AddComponent<Image>();
            topImg.color = Color.black;
            topImg.raycastTarget = false;
            RectTransform tr = topImg.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f - barHeightPercent);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            // Bottom letterbox bar
            GameObject botGo = new GameObject("Letterbox_Bottom");
            botGo.transform.SetParent(go.transform, false);
            Image botImg = botGo.AddComponent<Image>();
            botImg.color = Color.black;
            botImg.raycastTarget = false;
            RectTransform br = botImg.rectTransform;
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(1f, barHeightPercent);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;

            // Subtle skip hint inside bottom bar
            GameObject skipGo = new GameObject("SkipHint");
            skipGo.transform.SetParent(go.transform, false);
            Text skip = skipGo.AddComponent<Text>();
            skip.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skip.fontSize = 18;
            skip.alignment = TextAnchor.LowerRight;
            skip.color = new Color(0.75f, 0.88f, 0.98f, 0f);
            skip.text = "[SPACE / ESC] SKIP CUTSCENE";
            skip.raycastTarget = false;
            RectTransform sr = skip.rectTransform;
            sr.anchorMin = new Vector2(0.55f, 0f);
            sr.anchorMax = new Vector2(0.98f, barHeightPercent * 0.9f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;

            rig.Canvas = c;
            rig.Group = cg;
            rig.TopBar = tr;
            rig.BottomBar = br;
            rig.FadeOverlay = fade;
            rig.FlashOverlay = flash;
            rig.SkipText = skip;
            return rig;
        }

        public static bool IsSkipPressed()
        {
            var kb = Keyboard.current;
            return kb != null &&
                   (kb.spaceKey.wasPressedThisFrame ||
                    kb.enterKey.wasPressedThisFrame ||
                    kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.escapeKey.wasPressedThisFrame);
        }

        public static void SolveTwoBoneIKWorld(
            Transform upper,
            Transform lower,
            Transform tip,
            Vector3 targetTipWorld,
            Vector3 poleWorld,
            float weight = 1f)
        {
            if (upper == null || lower == null || tip == null || weight <= 0.001f) return;

            Quaternion origUpper = upper.rotation;
            Quaternion origLower = lower.rotation;

            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = tip.position;

            float len1 = Vector3.Distance(a, b);
            float len2 = Vector3.Distance(b, c);
            if (len1 < 0.001f || len2 < 0.001f) return;

            Vector3 toTarget = targetTipWorld - a;
            float targetDist = toTarget.magnitude;
            if (targetDist < 0.001f) return;

            float maxReach = (len1 + len2) * 0.996f;
            float minReach = Mathf.Abs(len1 - len2) + 0.01f;
            float d = Mathf.Clamp(targetDist, minReach, maxReach);
            Vector3 dir = toTarget / targetDist;

            // Compute desired bend plane orthogonal to dir facing pole
            Vector3 toPole = poleWorld - a;
            Vector3 planeNormal = Vector3.Cross(dir, toPole);
            if (planeNormal.sqrMagnitude < 1e-6f)
            {
                Vector3 currentMid = b - a;
                planeNormal = Vector3.Cross(dir, currentMid);
                if (planeNormal.sqrMagnitude < 1e-6f)
                    planeNormal = Vector3.Cross(dir, Vector3.up);
                if (planeNormal.sqrMagnitude < 1e-6f)
                    planeNormal = Vector3.Cross(dir, Vector3.right);
            }
            planeNormal.Normalize();
            Vector3 bendDir = Vector3.Cross(planeNormal, dir).normalized;

            float cosAngleA = Mathf.Clamp((len1 * len1 + d * d - len2 * len2) / (2f * len1 * d), -1f, 1f);
            float sinAngleA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAngleA * cosAngleA));

            Vector3 desiredB = a + (dir * cosAngleA + bendDir * sinAngleA) * len1;
            Vector3 desiredC = a + dir * d;

            Vector3 curUpperVec = b - a;
            Vector3 desUpperVec = desiredB - a;
            if (curUpperVec.sqrMagnitude > 1e-6f && desUpperVec.sqrMagnitude > 1e-6f)
            {
                Quaternion rotU = Quaternion.FromToRotation(curUpperVec.normalized, desUpperVec.normalized);
                upper.rotation = rotU * upper.rotation;
            }

            Vector3 curLowerVec = tip.position - lower.position;
            Vector3 desLowerVec = desiredC - lower.position;
            if (curLowerVec.sqrMagnitude > 1e-6f && desLowerVec.sqrMagnitude > 1e-6f)
            {
                Quaternion rotL = Quaternion.FromToRotation(curLowerVec.normalized, desLowerVec.normalized);
                lower.rotation = rotL * lower.rotation;
            }

            if (weight < 0.999f)
            {
                upper.rotation = Quaternion.Slerp(origUpper, upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(origLower, lower.rotation, weight);
            }
        }

        public static void TwistBoneChainToward(Transform bone, Vector3 desiredForwardWorld, Vector3 upHintWorld, float weight = 1f)
        {
            if (bone == null || weight <= 0.001f || desiredForwardWorld.sqrMagnitude < 1e-5f) return;
            Quaternion targetRot = Quaternion.LookRotation(desiredForwardWorld.normalized, upHintWorld.normalized);
            bone.rotation = Quaternion.Slerp(bone.rotation, targetRot, weight);
        }

        public static void SyncCameraRain(Camera cam, bool clearSplashes = false)
        {
            if (cam == null) return;
            var rain = cam.GetComponent<CameraLensRainController>();
            if (rain != null)
            {
                if (clearSplashes) rain.ClearLensWaterSplash();
                rain.ForceSyncNow();
            }
        }

        public static Texture2D GetSoftCircleTexture()
        {
            if (softCircleTex != null) return softCircleTex;
            const int size = 64;
            softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softCircleTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softCircleTex.SetPixels(px);
            softCircleTex.Apply();
            return softCircleTex;
        }

        public static Texture2D GetSparkStreakTexture()
        {
            if (sparkStreakTex != null) return sparkStreakTex;
            const int w = 64;
            const int h = 16;
            sparkStreakTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            sparkStreakTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float ny = Mathf.Abs((y - h * 0.5f) / (h * 0.45f));
                float ay = Mathf.Clamp01(1f - ny * ny);
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x - w * 0.5f) / (w * 0.5f));
                    float ax = Mathf.Clamp01(1f - nx);
                    float a = ay * Mathf.Pow(ax, 1.3f);
                    px[y * w + x] = new Color(1f, 0.9f, 0.72f, a);
                }
            }
            sparkStreakTex.SetPixels(px);
            sparkStreakTex.Apply();
            return sparkStreakTex;
        }

        public static Material CreateParticleMaterial(Texture2D tex, bool additive)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                       ?? Shader.Find("Particles/Standard Unlit")
                       ?? Shader.Find("Sprites/Default");
            Material mat = new Material(s);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            mat.color = Color.white;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (additive)
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ADD");
            }
            else
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.renderQueue = 3100;
            return mat;
        }

        public static ParticleSystem CreateOrangeSparkEmitter(string name, Transform parent, Transform bouncePlaneTransform, float ratePerSec = 140f, float speedMin = 12f, float speedMax = 30f)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
            main.gravityModifier = 2.6f;
            main.maxParticles = 900;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.58f, 0.08f, 1f),
                new Color(1f, 0.92f, 0.45f, 1f));

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = ratePerSec;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 2.2f;

            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            if (bouncePlaneTransform != null)
            {
                col.SetPlane(0, bouncePlaneTransform);
            }
            col.bounce = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            col.dampen = 0.25f;

            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = 0.14f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.1f)));
            var trailGrad = new Gradient();
            trailGrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.72f, 0.16f), 0f), new GradientColorKey(new Color(1f, 0.28f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(trailGrad);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.renderMode = ParticleSystemRenderMode.Stretch;
            ren.velocityScale = 0.07f;
            ren.lengthScale = 3.2f;
            Material sparkMat = CreateParticleMaterial(GetSparkStreakTexture(), true);
            ren.sharedMaterial = sparkMat;
            ren.trailMaterial = sparkMat;

            return ps;
        }

        public static ParticleSystem CreateShoulderSteamVentEmitter(string name, Transform parent, Vector3 localPos, Vector3 localDir)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(localDir.normalized, Vector3.up);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 27f);
            main.startSize = new ParticleSystem.MinMaxCurve(3.4f, 6.8f);
            main.gravityModifier = -0.45f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.90f, 0.96f, 1f, 0.72f),
                new Color(0.75f, 0.88f, 0.98f, 0.52f));

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.65f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.45f, 1.45f),
                new Keyframe(1f, 2.5f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.9f, 0.96f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.84f, 0.94f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.68f, 0.14f), new GradientAlphaKey(0.32f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = CreateParticleMaterial(GetSoftCircleTexture(), false);
            return ps;
        }

        public static ParticleSystem CreateWaterSprayWallEmitter(string name, Transform parent, float speedMin = 15f, float speedMax = 34f)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 5.4f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 800;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.88f, 0.98f, 0.58f),
                new Color(0.88f, 0.96f, 1f, 0.42f));

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 3.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.75f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.9f, 0.97f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.62f, 0.18f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = CreateParticleMaterial(GetSoftCircleTexture(), false);
            return ps;
        }
    }
}
