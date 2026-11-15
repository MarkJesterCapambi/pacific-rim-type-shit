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
            string cylinderPath = "Assets/Meshes/PacificRim_RocketThrusterCylinder.asset";
            UnityEngine.Mesh cylinderMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(cylinderPath);
            if (cylinderMesh == null)
            {
                cylinderMesh = GenerateThrusterCylinderMesh(0.52f, 0.68f, 5.2f, 24, 16);
                AssetDatabase.CreateAsset(cylinderMesh, cylinderPath);
                AssetDatabase.SaveAssets();
            }

            Material beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_VolumetricBeam.mat");
            Material flameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketThrusterFlame.mat");
            Material sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketSparks.mat");
            Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketSmoke.mat");
            Material exhaustBeamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketExhaustBeam.mat");

            // 6. Setup Volumetric Light Beams
            SetupBeamOrigin(headLight, coneMesh, beamMat, isHead: true);
            SetupBeamOrigin(torsoLightL, coneMesh, beamMat, isHead: false);
            SetupBeamOrigin(torsoLightR, coneMesh, beamMat, isHead: false);

            // 7. Setup Rocket Thrusters
            SetupRocketOrigin(rocketL, cylinderMesh, exhaustBeamMat, flameMat, sparkMat, smokeMat);
            SetupRocketOrigin(rocketR, cylinderMesh, exhaustBeamMat, flameMat, sparkMat, smokeMat);

            sockets.headLightOrigin = headLight;
            sockets.torsoLightOriginL = torsoLightL;
            sockets.torsoLightOriginR = torsoLightR;
            sockets.rocketOriginL = rocketL;
            sockets.rocketOriginR = rocketR;

            sockets.ResolveReferences();
            sockets.SetRocketBlastActive(false, immediate: true);

            // Setup Nuclear Vortex Turbine Spinner
            var turbineSpinner = jaeger.GetComponent<JaegerTurbineSpinner>();
            if (turbineSpinner == null) turbineSpinner = jaeger.AddComponent<JaegerTurbineSpinner>();
            turbineSpinner.ResolveReferences();

            EditorUtility.SetDirty(jaeger);
            Debug.Log("<color=#00e5ff><b>[VisualSockets]</b></color> Successfully wired Head/Chest light beams, twin cyan-blue rocket thrusters, and nuclear vortex turbine spinner!");
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

        private static void SetupRocketOrigin(Transform origin, UnityEngine.Mesh cylinderMesh, Material exhaustBeamMat, Material flameMat, Material sparkMat, Material smokeMat)
        {
            if (origin == null) return;

            // Invert exhaust direction 180 degrees so exhaust blazes downwards/backwards instead of upwards
            Quaternion exhaustRotation = Quaternion.Euler(180f, 0f, 0f);

            // 1. Volumetric Cylindrical Rocket Exhaust Beam (Solid at origin, flickering length & turbulence at tail)
            Transform meshChild = origin.Find("ExhaustBeamMesh");
            if (meshChild == null)
            {
                var mgo = new GameObject("ExhaustBeamMesh");
                mgo.transform.SetParent(origin, false);
                meshChild = mgo.transform;
            }
            meshChild.localPosition = exhaustRotation * (Vector3.forward * 0.12f);
            meshChild.localRotation = exhaustRotation;
            meshChild.localScale = Vector3.one;

            var mf = meshChild.GetComponent<MeshFilter>();
            if (mf == null) mf = meshChild.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = cylinderMesh;

            var mr = meshChild.GetComponent<MeshRenderer>();
            if (mr == null) mr = meshChild.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = exhaustBeamMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            // 2. Point Light for combustion glow & dynamic flicker
            Transform lightChild = origin.Find("ThrusterLight");
            if (lightChild == null)
            {
                var lgo = new GameObject("ThrusterLight");
                lgo.transform.SetParent(origin, false);
                lightChild = lgo.transform;
            }
            lightChild.localPosition = exhaustRotation * (Vector3.forward * 0.35f);

            var pl = lightChild.GetComponent<Light>();
            if (pl == null) pl = lightChild.gameObject.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.12f, 0.88f, 1.0f);
            pl.range = 14f;
            pl.intensity = 4800f;
            pl.shadows = LightShadows.None;
            pl.enabled = false;

            // 3. Core Supersonic Exhaust Beam - Collimated, High-Speed Continuous Plasma Stream
            Transform flameChild = origin.Find("FlameCore");
            if (flameChild == null)
            {
                var fgo = new GameObject("FlameCore");
                fgo.transform.SetParent(origin, false);
                flameChild = fgo.transform;
            }
            flameChild.localPosition = Vector3.zero;
            flameChild.localRotation = exhaustRotation;

            var ps = flameChild.GetComponent<ParticleSystem>();
            if (ps == null) ps = flameChild.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = 0.16f;
            main.startSpeed = 32f;
            main.startSize = 1.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 160;

            var emission = ps.emission;
            emission.rateOverTime = 120;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0.8f;
            shape.radius = 0.18f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.18f, 0.92f, 1.0f), 0.15f),
                    new GradientColorKey(new Color(0.05f, 0.55f, 1.0f), 0.55f),
                    new GradientColorKey(new Color(0.02f, 0.18f, 0.85f), 0.85f),
                    new GradientColorKey(new Color(0.01f, 0.06f, 0.45f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.85f);
            sizeCurve.AddKey(0.20f, 1.15f);
            sizeCurve.AddKey(0.70f, 1.00f);
            sizeCurve.AddKey(1.0f, 0.35f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var psr = flameChild.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 3.6f;
            psr.velocityScale = 0.06f;
            psr.sharedMaterial = flameMat;

            // 4. Volumetric Plasma Sheath / Outer Bloom
            Transform sheathChild = origin.Find("ExhaustOuterSheath");
            if (sheathChild == null)
            {
                var ogo = new GameObject("ExhaustOuterSheath");
                ogo.transform.SetParent(origin, false);
                sheathChild = ogo.transform;
            }
            sheathChild.localPosition = Vector3.zero;
            sheathChild.localRotation = exhaustRotation;

            var ops = sheathChild.GetComponent<ParticleSystem>();
            if (ops == null) ops = sheathChild.gameObject.AddComponent<ParticleSystem>();
            var omain = ops.main;
            omain.playOnAwake = false;
            omain.loop = true;
            omain.duration = 1.0f;
            omain.startLifetime = 0.32f;
            omain.startSpeed = 34f;
            omain.startSize = 2.8f;
            omain.simulationSpace = ParticleSystemSimulationSpace.Local;
            omain.maxParticles = 100;

            var oemission = ops.emission;
            oemission.rateOverTime = 80;

            var oshape = ops.shape;
            oshape.shapeType = ParticleSystemShapeType.Cone;
            oshape.angle = 2.5f;
            oshape.radius = 0.24f;

            var ocol = ops.colorOverLifetime;
            ocol.enabled = true;
            Gradient ograd = new Gradient();
            ograd.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.40f, 0.88f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.10f, 0.50f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.02f, 0.15f, 0.65f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.45f, 0.2f),
                    new GradientAlphaKey(0.35f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            ocol.color = ograd;

            var osize = ops.sizeOverLifetime;
            osize.enabled = true;
            AnimationCurve osizeCurve = new AnimationCurve();
            osizeCurve.AddKey(0.0f, 0.8f);
            osizeCurve.AddKey(1.0f, 1.8f);
            osize.size = new ParticleSystem.MinMaxCurve(1.0f, osizeCurve);

            var opsr = sheathChild.GetComponent<ParticleSystemRenderer>();
            opsr.renderMode = ParticleSystemRenderMode.Stretch;
            opsr.lengthScale = 3.6f;
            opsr.velocityScale = 0.06f;
            opsr.sharedMaterial = flameMat;

            // 5. Intense Manifold Flare right at nozzle rim
            Transform glowChild = origin.Find("NozzleGlow");
            if (glowChild == null)
            {
                var ggo = new GameObject("NozzleGlow");
                ggo.transform.SetParent(origin, false);
                glowChild = ggo.transform;
            }
            glowChild.localPosition = exhaustRotation * (Vector3.forward * 0.1f);
            glowChild.localRotation = exhaustRotation;

            var gps = glowChild.GetComponent<ParticleSystem>();
            if (gps == null) gps = glowChild.gameObject.AddComponent<ParticleSystem>();
            var gmain = gps.main;
            gmain.playOnAwake = false;
            gmain.loop = true;
            gmain.duration = 1.0f;
            gmain.startLifetime = 0.08f;
            gmain.startSpeed = 0.2f;
            gmain.startSize = 3.4f;
            gmain.simulationSpace = ParticleSystemSimulationSpace.Local;
            gmain.maxParticles = 30;

            var gemission = gps.emission;
            gemission.rateOverTime = 30;

            var gshape = gps.shape;
            gshape.shapeType = ParticleSystemShapeType.Sphere;
            gshape.radius = 0.08f;

            var gcol = gps.colorOverLifetime;
            gcol.enabled = true;
            Gradient ggrad = new Gradient();
            ggrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.25f, 0.88f, 1.0f), 0.6f),
                    new GradientColorKey(new Color(0.05f, 0.45f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            gcol.color = ggrad;

            var gpsr = glowChild.GetComponent<ParticleSystemRenderer>();
            gpsr.renderMode = ParticleSystemRenderMode.Billboard;
            gpsr.sharedMaterial = flameMat;

            // 6. High-velocity Sparks & Embers - needle streaks
            Transform sparksChild = origin.Find("PlasmaSparks");
            if (sparksChild == null)
            {
                var sgo = new GameObject("PlasmaSparks");
                sgo.transform.SetParent(origin, false);
                sparksChild = sgo.transform;
            }
            sparksChild.localPosition = Vector3.zero;
            sparksChild.localRotation = exhaustRotation;

            var sps = sparksChild.GetComponent<ParticleSystem>();
            if (sps == null) sps = sparksChild.gameObject.AddComponent<ParticleSystem>();
            var smain = sps.main;
            smain.playOnAwake = false;
            smain.loop = true;
            smain.duration = 1.0f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(22f, 42f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            smain.gravityModifier = 0.6f;
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            smain.maxParticles = 180;

            var semission = sps.emission;
            semission.rateOverTime = 120;

            var sshape = sps.shape;
            sshape.shapeType = ParticleSystemShapeType.Cone;
            sshape.angle = 8.5f;
            sshape.radius = 0.16f;

            var scol = sps.colorOverLifetime;
            scol.enabled = true;
            Gradient sgrad = new Gradient();
            sgrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.90f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.20f, 0.88f, 1.0f), 0.55f),
                    new GradientColorKey(new Color(0.05f, 0.40f, 0.95f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            scol.color = sgrad;

            var spsr = sparksChild.GetComponent<ParticleSystemRenderer>();
            spsr.renderMode = ParticleSystemRenderMode.Stretch;
            spsr.lengthScale = 2.4f;
            spsr.velocityScale = 0.05f;
            spsr.sharedMaterial = sparkMat;

            // 7. Steam / Condensation Vapor Mist - soft billowing smoke
            Transform steamChild = origin.Find("VaporMist");
            if (steamChild == null)
            {
                var mgo = new GameObject("VaporMist");
                mgo.transform.SetParent(origin, false);
                steamChild = mgo.transform;
            }
            steamChild.localPosition = exhaustRotation * (Vector3.forward * 0.5f);
            steamChild.localRotation = exhaustRotation;

            var mps = steamChild.GetComponent<ParticleSystem>();
            if (mps == null) mps = steamChild.gameObject.AddComponent<ParticleSystem>();
            var mmain = mps.main;
            mmain.playOnAwake = false;
            mmain.loop = true;
            mmain.duration = 1.0f;
            mmain.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.75f);
            mmain.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.0f);
            mmain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            mmain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            mmain.simulationSpace = ParticleSystemSimulationSpace.World;
            mmain.maxParticles = 80;

            var memission = mps.emission;
            memission.rateOverTime = 40;

            var mshape = mps.shape;
            mshape.shapeType = ParticleSystemShapeType.Cone;
            mshape.angle = 12.0f;
            mshape.radius = 0.20f;

            var mcol = mps.colorOverLifetime;
            mcol.enabled = true;
            Gradient mgrad = new Gradient();
            mgrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.45f, 0.75f, 0.95f), 0.0f),
                    new GradientColorKey(new Color(0.60f, 0.70f, 0.85f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.35f, 0.25f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            mcol.color = mgrad;

            var msize = mps.sizeOverLifetime;
            msize.enabled = true;
            AnimationCurve msizeCurve = new AnimationCurve();
            msizeCurve.AddKey(0.0f, 0.4f);
            msizeCurve.AddKey(1.0f, 1.8f);
            msize.size = new ParticleSystem.MinMaxCurve(1.0f, msizeCurve);

            var mrot = mps.rotationOverLifetime;
            mrot.enabled = true;
            mrot.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);

            var mpsr = steamChild.GetComponent<ParticleSystemRenderer>();
            mpsr.renderMode = ParticleSystemRenderMode.Billboard;
            mpsr.sharedMaterial = smokeMat;
        }

        public static UnityEngine.Mesh GenerateThrusterCylinderMesh(float radiusStart = 0.52f, float radiusEnd = 0.68f, float length = 5.2f, int radialSegments = 24, int lengthSegments = 16)
        {
            var mesh = new UnityEngine.Mesh();
            mesh.name = "PacificRim_RocketThrusterCylinder";

            int vertCount = (radialSegments + 1) * (lengthSegments + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            for (int y = 0; y <= lengthSegments; y++)
            {
                float t = (float)y / lengthSegments;
                float z = t * length;
                float r = Mathf.Lerp(radiusStart, radiusEnd, t);

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = (float)x / radialSegments;
                    float angle = u * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    int idx = y * (radialSegments + 1) + x;
                    vertices[idx] = new Vector3(cos * r, sin * r, z);
                    normals[idx] = new Vector3(cos, sin, 0f).normalized;
                    uvs[idx] = new Vector2(u, t);
                }
            }

            int quadCount = radialSegments * lengthSegments;
            int[] triangles = new int[quadCount * 6];
            int triIdx = 0;

            for (int y = 0; y < lengthSegments; y++)
            {
                for (int x = 0; x < radialSegments; x++)
                {
                    int b = y * (radialSegments + 1) + x;
                    int t = (y + 1) * (radialSegments + 1) + x;

                    triangles[triIdx++] = b;
                    triangles[triIdx++] = t;
                    triangles[triIdx++] = b + 1;

                    triangles[triIdx++] = b + 1;
                    triangles[triIdx++] = t;
                    triangles[triIdx++] = t + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
