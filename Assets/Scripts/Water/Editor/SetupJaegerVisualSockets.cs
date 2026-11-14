using UnityEngine;
using UnityEditor;

namespace PacificRimWater.Editor
{
    public static class SetupJaegerVisualSockets
    {
        [MenuItem("Pacific Rim/Setup Gipsy Visual Sockets & VFX")]
        public static void Setup()
        {
            var jaeger = GameObject.Find("Jaeger_Mech");
            if (jaeger == null)
            {
                Debug.LogError("[VisualSockets] Jaeger_Mech not found in scene!");
                return;
            }

            var sockets = jaeger.GetComponent<JaegerVisualSockets>();
            if (sockets == null)
            {
                sockets = jaeger.AddComponent<JaegerVisualSockets>();
            }

            // 1. Locate bones
            Transform headBone = null;
            Transform chestBone = null;
            foreach (var t in jaeger.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head" && t.parent != null && t.parent.name == "Chest") headBone = t;
                else if (t.name == "Chest" && t.parent != null && t.parent.name == "Spine1") chestBone = t;
            }

            if (headBone == null || chestBone == null)
            {
                Debug.LogError("[VisualSockets] Could not find Head or Chest bones on Jaeger_Mech!");
                return;
            }

            sockets.headBone = headBone;
            sockets.chestBone = chestBone;

            // 2. Locate origin nodes
            Transform headLight = null;
            Transform torsoLightL = null;
            Transform torsoLightR = null;
            Transform rocketL = null;
            Transform rocketR = null;

            foreach (var t in jaeger.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LightBeamOriginHead")
                {
                    headLight = t;
                }
                else if (t.name.StartsWith("LightBeamOriginUpperTorso"))
                {
                    if (t.position.x < jaeger.transform.position.x) torsoLightL = t;
                    else torsoLightR = t;
                }
                else if (t.name.StartsWith("RocketEngineOrigin"))
                {
                    if (t.position.x < jaeger.transform.position.x) rocketL = t;
                    else rocketR = t;
                }
            }

            // Rename for clarity
            if (torsoLightL != null) torsoLightL.name = "LightBeamOriginUpperTorso.L";
            if (torsoLightR != null) torsoLightR.name = "LightBeamOriginUpperTorso.R";
            if (rocketL != null) rocketL.name = "RocketEngineOrigin.L";
            if (rocketR != null) rocketR.name = "RocketEngineOrigin.R";

            // 3. Reparent preserving world transform
            if (headLight != null && headLight.parent != headBone)
            {
                Undo.SetTransformParent(headLight, headBone, "Reparent Head Light Node");
            }
            if (torsoLightL != null && torsoLightL.parent != chestBone)
            {
                Undo.SetTransformParent(torsoLightL, chestBone, "Reparent Torso Light Node L");
            }
            if (torsoLightR != null && torsoLightR.parent != chestBone)
            {
                Undo.SetTransformParent(torsoLightR, chestBone, "Reparent Torso Light Node R");
            }
            if (rocketL != null && rocketL.parent != chestBone)
            {
                Undo.SetTransformParent(rocketL, chestBone, "Reparent Rocket Node L");
            }
            if (rocketR != null && rocketR.parent != chestBone)
            {
                Undo.SetTransformParent(rocketR, chestBone, "Reparent Rocket Node R");
            }

            // 4. Disable placeholder cube renderers on origins so they act purely as sockets
            var allOrigins = new[] { headLight, torsoLightL, torsoLightR, rocketL, rocketR };
            foreach (var o in allOrigins)
            {
                if (o == null) continue;
                var mr = o.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                var col = o.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            // 5. Load assets
            UnityEngine.Mesh coneMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");
            Material beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_VolumetricBeam.mat");
            Material flameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketThrusterFlame.mat");

            // 6. Setup Volumetric Light Beams
            SetupBeamOrigin(headLight, coneMesh, beamMat, isHead: true);
            SetupBeamOrigin(torsoLightL, coneMesh, beamMat, isHead: false);
            SetupBeamOrigin(torsoLightR, coneMesh, beamMat, isHead: false);

            // 7. Setup Rocket Thrusters
            SetupRocketOrigin(rocketL, flameMat);
            SetupRocketOrigin(rocketR, flameMat);

            sockets.headLightOrigin = headLight;
            sockets.torsoLightOriginL = torsoLightL;
            sockets.torsoLightOriginR = torsoLightR;
            sockets.rocketOriginL = rocketL;
            sockets.rocketOriginR = rocketR;

            sockets.ResolveReferences();
            sockets.SetRocketBlastActive(false, immediate: true);

            EditorUtility.SetDirty(jaeger);
            Debug.Log("<color=#00e5ff><b>[VisualSockets]</b></color> Successfully wired Head/Chest light beams and twin cyan-blue rocket thrusters!");
        }

        private static void SetupBeamOrigin(Transform origin, UnityEngine.Mesh coneMesh, Material beamMat, bool isHead)
        {
            if (origin == null) return;

            // Spot light - gentle surface illumination so surfaces aren't bleached out
            var spot = origin.GetComponent<Light>();
            if (spot == null) spot = origin.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = isHead ? new Color(0.96f, 0.98f, 1.0f) : new Color(1.0f, 0.95f, 0.88f);
            spot.range = isHead ? 85f : 75f;
            spot.spotAngle = isHead ? 34f : 44f;
            spot.innerSpotAngle = isHead ? 20f : 28f;
            spot.intensity = isHead ? 650f : 420f;
            spot.shadows = LightShadows.None;

            // Conical mesh child
            Transform beamChild = origin.Find("VolumetricBeam");
            if (beamChild == null)
            {
                var go = new GameObject("VolumetricBeam");
                go.transform.SetParent(origin, false);
                beamChild = go.transform;
            }
            beamChild.localPosition = Vector3.zero;
            beamChild.localRotation = Quaternion.identity;
            beamChild.localScale = isHead ? new Vector3(0.85f, 0.85f, 1.05f) : Vector3.one;

            var mf = beamChild.GetComponent<MeshFilter>();
            if (mf == null) mf = beamChild.gameObject.AddComponent<MeshFilter>();
            var mr = beamChild.GetComponent<MeshRenderer>();
            if (mr == null) mr = beamChild.gameObject.AddComponent<MeshRenderer>();

            mf.sharedMesh = coneMesh;
            mr.sharedMaterial = beamMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void SetupRocketOrigin(Transform origin, Material flameMat)
        {
            if (origin == null) return;

            // 1. Point Light for combustion glow - compact and grounded
            Transform lightChild = origin.Find("ThrusterLight");
            if (lightChild == null)
            {
                var lgo = new GameObject("ThrusterLight");
                lgo.transform.SetParent(origin, false);
                lightChild = lgo.transform;
            }
            lightChild.localPosition = Vector3.forward * 0.25f;

            var pl = lightChild.GetComponent<Light>();
            if (pl == null) pl = lightChild.gameObject.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.08f, 0.85f, 1.0f);
            pl.range = 6.5f;
            pl.intensity = 1400f;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            // 2. Core Supersonic Flame - tight, thin, compact plume focused right at nozzle
            Transform flameChild = origin.Find("FlameCore");
            if (flameChild == null)
            {
                var fgo = new GameObject("FlameCore");
                fgo.transform.SetParent(origin, false);
                flameChild = fgo.transform;
            }
            flameChild.localPosition = Vector3.zero;
            flameChild.localRotation = Quaternion.identity;

            var ps = flameChild.GetComponent<ParticleSystem>();
            if (ps == null) ps = flameChild.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = 0.12f;
            main.startSpeed = 16f;
            main.startSize = 0.42f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = 80;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 1.8f;
            shape.radius = 0.11f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.92f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.0f, 0.88f, 1.0f), 0.35f),
                    new GradientColorKey(new Color(0.05f, 0.35f, 0.95f), 0.75f),
                    new GradientColorKey(new Color(0.02f, 0.12f, 0.65f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.55f);
            sizeCurve.AddKey(0.35f, 1.05f);
            sizeCurve.AddKey(1.0f, 0.20f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var psr = flameChild.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 1.7f;
            psr.velocityScale = 0.04f;
            psr.sharedMaterial = flameMat;

            // 3. High-velocity Sparks & Embers - thin, short and focused
            Transform sparksChild = origin.Find("PlasmaSparks");
            if (sparksChild == null)
            {
                var sgo = new GameObject("PlasmaSparks");
                sgo.transform.SetParent(origin, false);
                sparksChild = sgo.transform;
            }
            sparksChild.localPosition = Vector3.zero;
            sparksChild.localRotation = Quaternion.identity;

            var sps = sparksChild.GetComponent<ParticleSystem>();
            if (sps == null) sps = sparksChild.gameObject.AddComponent<ParticleSystem>();
            var smain = sps.main;
            smain.playOnAwake = false;
            smain.loop = true;
            smain.duration = 1.0f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(10f, 18f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            smain.gravityModifier = 0.8f;
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            smain.maxParticles = 140;

            var semission = sps.emission;
            semission.rateOverTime = 80;

            var sshape = sps.shape;
            sshape.shapeType = ParticleSystemShapeType.Cone;
            sshape.angle = 5.0f;
            sshape.radius = 0.12f;

            var scol = sps.colorOverLifetime;
            scol.enabled = true;
            Gradient sgrad = new Gradient();
            sgrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.85f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.15f, 0.85f, 1.0f), 0.6f),
                    new GradientColorKey(new Color(0.05f, 0.35f, 0.95f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.8f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            scol.color = sgrad;

            var spsr = sparksChild.GetComponent<ParticleSystemRenderer>();
            spsr.renderMode = ParticleSystemRenderMode.Stretch;
            spsr.lengthScale = 1.4f;
            spsr.velocityScale = 0.03f;
            spsr.sharedMaterial = flameMat;

            // 4. Steam / Condensation Vapor Mist - compact trailing vapor
            Transform steamChild = origin.Find("VaporMist");
            if (steamChild == null)
            {
                var mgo = new GameObject("VaporMist");
                mgo.transform.SetParent(origin, false);
                steamChild = mgo.transform;
            }
            steamChild.localPosition = Vector3.forward * 0.4f;
            steamChild.localRotation = Quaternion.identity;

            var mps = steamChild.GetComponent<ParticleSystem>();
            if (mps == null) mps = steamChild.gameObject.AddComponent<ParticleSystem>();
            var mmain = mps.main;
            mmain.playOnAwake = false;
            mmain.loop = true;
            mmain.duration = 1.0f;
            mmain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            mmain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            mmain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            mmain.simulationSpace = ParticleSystemSimulationSpace.World;
            mmain.maxParticles = 40;

            var memission = mps.emission;
            memission.rateOverTime = 25;

            var mshape = mps.shape;
            mshape.shapeType = ParticleSystemShapeType.Cone;
            mshape.angle = 7.0f;
            mshape.radius = 0.14f;

            var mcol = mps.colorOverLifetime;
            mcol.enabled = true;
            Gradient mgrad = new Gradient();
            mgrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.35f, 0.75f, 0.95f), 0.0f),
                    new GradientColorKey(new Color(0.55f, 0.70f, 0.85f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.25f, 0.25f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            mcol.color = mgrad;

            var msize = mps.sizeOverLifetime;
            msize.enabled = true;
            AnimationCurve msizeCurve = new AnimationCurve();
            msizeCurve.AddKey(0.0f, 0.4f);
            msizeCurve.AddKey(1.0f, 1.4f);
            msize.size = new ParticleSystem.MinMaxCurve(1.0f, msizeCurve);

            var mpsr = steamChild.GetComponent<ParticleSystemRenderer>();
            mpsr.renderMode = ParticleSystemRenderMode.Billboard;
            mpsr.sharedMaterial = flameMat;
        }
    }
}
