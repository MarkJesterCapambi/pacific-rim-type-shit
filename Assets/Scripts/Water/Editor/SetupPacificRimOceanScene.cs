#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PacificRimAtmosphere;
using PacificRimEnvironment;

namespace PacificRimWater.Editor
{
    public static class SetupPacificRimOceanScene
    {
        
        [MenuItem("Pacific Rim/Setup Complete Jaeger Character", false, 5)]
        public static void SetupCompleteJaegerCharacter()
        {
            string fbxPath = "Assets/Models/GipsyDangerV2.fbx";
            string animFbxPath = "Assets/Models/GipsyDanger.fbx";

            // 0. Align Seabed / Test Platform & BoxCollider flush with visual floor (top surface at Y = 0.0)
            GameObject platform = GameObject.Find("Test_Platform");
            if (platform == null)
            {
                platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "Test_Platform";
                Undo.RegisterCreatedObjectUndo(platform, "Create Seabed Platform");
            }
            if (platform != null)
            {
                Undo.RecordObject(platform.transform, "Align Test Platform");
                platform.transform.position = new Vector3(0f, -0.5f, 0f);
                platform.transform.rotation = Quaternion.identity;
                platform.transform.localScale = new Vector3(400f, 1f, 400f);
                platform.tag = "Ground";

                var platCol = platform.GetComponent<BoxCollider>();
                if (platCol == null) platCol = platform.AddComponent<BoxCollider>();
                Undo.RecordObject(platCol, "Align Test Platform Collider");
                platCol.center = Vector3.zero;
                platCol.size = Vector3.one;

                var platRend = platform.GetComponent<Renderer>();
                if (platRend != null)
                {
                    platRend.sharedMaterial = EnsureSeabedMaterial();
                }
            }

            // 1. Repair FBX animation clip import ranges & looping before loading sub-assets (source clips live in GipsyDanger.fbx)
            if (!RepairFbxAnimationImport(animFbxPath))
            {
                return;
            }

            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogError("GipsyDangerV2.fbx prefab not found at " + fbxPath);
                return;
            }

            // 2. Locate Avatar from V2 model and AnimationClips from repaired animation source FBX
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var animAssets = AssetDatabase.LoadAllAssetsAtPath(animFbxPath);
            Avatar avatar = null;
            foreach (var a in assets)
            {
                if (a is Avatar av)
                {
                    avatar = av;
                    break;
                }
            }

            if (avatar == null)
            {
                Debug.LogError("Could not find Avatar in " + fbxPath);
                return;
            }

            AnimationClip idleClip = FindNonPreviewClip(animAssets, "Idle");
            AnimationClip walkClip = FindNonPreviewClip(animAssets, "Walk");
            AnimationClip punch1Clip = FindNonPreviewClip(animAssets, "Combat Punch1");
            AnimationClip heavyClip = FindNonPreviewClip(animAssets, "Combat Heavy Attack");

            // 3. Always rebuild Animator Controller with valid non-zero clips & guarded transitions
            string animControllerPath = "Assets/Animations/GipsyDanger_Locomotion.controller";
            RuntimeAnimatorController animCtrl = BuildLocomotionAnimator(fbxPath, animFbxPath, animControllerPath);

            // 4. Cleanly recreate Jaeger_Mech from FBX so hierarchy/bindings match repaired Avatar & clips
            GameObject oldJaeger = GameObject.Find("Jaeger_Mech");
            Transform oldParent = oldJaeger != null ? oldJaeger.transform.parent : null;
            if (oldJaeger != null)
            {
                Undo.DestroyObjectImmediate(oldJaeger);
            }

            GameObject jaeger = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            jaeger.name = "Jaeger_Mech";
            if (oldParent != null) jaeger.transform.SetParent(oldParent, false);
            jaeger.transform.position = Vector3.zero;
            jaeger.transform.rotation = Quaternion.identity;
            jaeger.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(jaeger, "Configure Complete Jaeger Character");

            // Apply Jaeger armor material and ensure SkinnedMeshRenderers render regardless of root bounds
            Material jaegerMat = EnsureJaegerMaterial();
            foreach (var rend in jaeger.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = jaegerMat;
                rend.sharedMaterials = mats;

                if (rend is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }

            Animator anim = jaeger.GetComponent<Animator>();
            if (anim == null) anim = jaeger.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = animCtrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();

            Debug.Log($"<color=#00ffaa><b>[Jaeger]</b> Assigned Avatar: '{avatar.name}', Controller: '{animCtrl.name}'</color>");

            // 5. Measure Idle-pose SkinnedMeshRenderer bounds & align soles cleanly to Y = 0.0
            if (idleClip != null)
            {
                idleClip.SampleAnimation(jaeger, 0f);
            }

            float initialY = jaeger.transform.position.y;
            Bounds totalBounds;
            bool hasBounds = ComputeCombinedSkinnedBounds(jaeger, out totalBounds);
            float colHeight = hasBounds ? totalBounds.size.y : 25.89f;
            float localCenterY = hasBounds ? (totalBounds.center.y - initialY) : 10.02f;
            float targetY = hasBounds ? (initialY - totalBounds.min.y) : 2.92f;

            jaeger.transform.position = new Vector3(0f, targetY, 0f);

            // 6. Calibrated Primary Locomotion CapsuleCollider
            CapsuleCollider mainCol = jaeger.GetComponent<CapsuleCollider>();
            if (mainCol == null) mainCol = jaeger.AddComponent<CapsuleCollider>();
            mainCol.direction = 1; // Y-axis
            mainCol.height = colHeight;
            mainCol.radius = 2.8f;
            mainCol.center = new Vector3(0f, localCenterY, 0f);

            // Frictionless/stable Physics Material
            PhysicsMaterial jaegerPhysMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Jaeger_Physics.physicMaterial");
            if (jaegerPhysMat == null)
            {
                jaegerPhysMat = new PhysicsMaterial("Jaeger_Physics")
                {
                    dynamicFriction = 0.5f,
                    staticFriction = 0.5f,
                    bounciness = 0.0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                EnsureFolder("Assets/Materials");
                AssetDatabase.CreateAsset(jaegerPhysMat, "Assets/Materials/Jaeger_Physics.physicMaterial");
            }
            mainCol.sharedMaterial = jaegerPhysMat;

            // 7. Configure Rigidbody
            Rigidbody rb = jaeger.GetComponent<Rigidbody>();
            if (rb == null) rb = jaeger.AddComponent<Rigidbody>();
            rb.mass = 2000f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 8. Setup Accurate Compound Hitboxes Container (scale 1:1 with Jaeger)
            SetupAccurateHitboxes(jaeger.transform);

            // 9. Preserve JaegerWaterInteraction & configure SimpleJaegerController
            if (jaeger.GetComponent<JaegerWaterInteraction>() == null)
            {
                jaeger.AddComponent<JaegerWaterInteraction>();
            }

            SimpleJaegerController controller = jaeger.GetComponent<SimpleJaegerController>();
            if (controller == null) controller = jaeger.AddComponent<SimpleJaegerController>();
            controller.walkSpeed = 9.0f;
            controller.runSpeed = 18.0f;
            controller.turnSpeed = 60.0f;
            controller.acceleration = 3.0f;
            controller.toggleRunMode = true;
            controller.punchCooldown = 1.16f;
            controller.comboResetWindow = 2.4f;
            controller.heavyAttackCooldown = 1.5f;
            controller.attackAnimSpeed = 0.75f;
            controller.lightAttackSpeedMultiplier = 0.70f;
            controller.heavyAttackSpeedMultiplier = 0.30f;
            controller.runTransitionSpeed = 1.6f;
            controller.animator = anim;

            // 10. Verify skeletal deformation & root-local foot excursion on Walk and Attack clips before restoring Idle pose
            if (walkClip != null && walkClip.length > 0f)
            {
                Transform footL = FindChildRecursive(jaeger.transform, "Foot.L");
                Transform footR = FindChildRecursive(jaeger.transform, "Foot.R");
                Transform thighL = FindChildRecursive(jaeger.transform, "Thigh.L");

                walkClip.SampleAnimation(jaeger, 0f);
                Vector3 foot0 = footL != null ? footL.position : Vector3.zero;
                Vector3 thigh0 = thighL != null ? thighL.position : Vector3.zero;

                walkClip.SampleAnimation(jaeger, walkClip.length * 0.25f);
                float footDeltaQ = footL != null ? Vector3.Distance(foot0, footL.position) : 0f;
                float thighDeltaQ = thighL != null ? Vector3.Distance(thigh0, thighL.position) : 0f;

                walkClip.SampleAnimation(jaeger, walkClip.length * 0.5f);
                float footDeltaHalf = footL != null ? Vector3.Distance(foot0, footL.position) : 0f;
                float thighDeltaHalf = thighL != null ? Vector3.Distance(thigh0, thighL.position) : 0f;

                float minLY = float.MaxValue, maxLY = float.MinValue, minLZ = float.MaxValue, maxLZ = float.MinValue;
                float minRY = float.MaxValue, maxRY = float.MinValue, minRZ = float.MaxValue, maxRZ = float.MinValue;
                const int sampleSteps = 16;
                for (int s = 0; s <= sampleSteps; s++)
                {
                    walkClip.SampleAnimation(jaeger, (walkClip.length * s) / sampleSteps);
                    if (footL != null)
                    {
                        Vector3 lpL = jaeger.transform.InverseTransformPoint(footL.position);
                        minLY = Mathf.Min(minLY, lpL.y); maxLY = Mathf.Max(maxLY, lpL.y);
                        minLZ = Mathf.Min(minLZ, lpL.z); maxLZ = Mathf.Max(maxLZ, lpL.z);
                    }
                    if (footR != null)
                    {
                        Vector3 lpR = jaeger.transform.InverseTransformPoint(footR.position);
                        minRY = Mathf.Min(minRY, lpR.y); maxRY = Mathf.Max(maxRY, lpR.y);
                        minRZ = Mathf.Min(minRZ, lpR.z); maxRZ = Mathf.Max(maxRZ, lpR.z);
                    }
                }
                float excLY = footL != null ? (maxLY - minLY) : 0f;
                float excLZ = footL != null ? (maxLZ - minLZ) : 0f;
                float excRY = footR != null ? (maxRY - minRY) : 0f;
                float excRZ = footR != null ? (maxRZ - minRZ) : 0f;

                int curveCount = AnimationUtility.GetCurveBindings(walkClip).Length;
                Debug.Log($"<color=#00ffaa><b>[Walk Deformation Check]</b> Clip '{walkClip.name}' (len={walkClip.length:F2}s, curves={curveCount}) -> Foot.L delta(0.25={footDeltaQ:F4}m, 0.5={footDeltaHalf:F4}m), Thigh.L delta(0.25={thighDeltaQ:F4}m, 0.5={thighDeltaHalf:F4}m), LocalExcursion Foot.L(Y={excLY:F3}m, Z={excLZ:F3}m) Foot.R(Y={excRY:F3}m, Z={excRZ:F3}m)</color>");
            }

            if (punch1Clip != null && punch1Clip.length > 0f)
            {
                LogAttackDeformation(jaeger, punch1Clip);
            }

            if (heavyClip != null && heavyClip.length > 0f)
            {
                LogAttackDeformation(jaeger, heavyClip);
            }

            if (idleClip != null)
            {
                idleClip.SampleAnimation(jaeger, 0f);
            }

            // 11. Camera Follow Setup (Fortnite-style right-shoulder TPS camera + passive sway)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                GetOrAddComponent<PacificRimCameraShake>(mainCam.gameObject);
                JaegerCameraFollow camFollow = mainCam.GetComponent<JaegerCameraFollow>();
                if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<JaegerCameraFollow>();
                camFollow.target = jaeger.transform;
                camFollow.targetOffset = new Vector3(0f, 16.5f, 0f);
                camFollow.rightShoulderOffset = 6.5f;
                camFollow.distance = 24.0f;
                camFollow.height = 4.5f;
                camFollow.followDamping = 6.0f;
                camFollow.rotationDamping = 5.0f;
                camFollow.enablePassiveSway = true;
                camFollow.passiveSwayPositionAmplitude = 0.16f;
                camFollow.passiveSwayRotationAmplitude = 0.35f;
                camFollow.passiveSwayRollAmplitude = 0.20f;
                camFollow.passiveSwayFrequency = 0.55f;

                Quaternion initRot = Quaternion.Euler(15f, jaeger.transform.eulerAngles.y, 0f);
                Quaternion initYawRot = Quaternion.Euler(0f, jaeger.transform.eulerAngles.y, 0f);
                Vector3 shoulderPivot = jaeger.transform.position + camFollow.targetOffset + (initYawRot * Vector3.right * camFollow.rightShoulderOffset);
                Vector3 camPos = shoulderPivot - (initRot * Vector3.forward * camFollow.distance) + Vector3.up * camFollow.height;
                Vector3 aimPoint = shoulderPivot + (initRot * Vector3.forward * 120f);
                mainCam.transform.position = camPos;
                mainCam.transform.LookAt(aimPoint);
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.AlignViewToObject(mainCam.transform);
                }
            }

            // 12. Re-add & calibrate the height-limited ocean system at mid-thigh elevation
            float midThighY = SetupOceanSystemAtMidThigh(jaeger, platform);

            // 13. Build and wire the Thunderstorm Weather System (Rain, Lightning, Fog, Wind, LUT)
            SetupThunderstormWeatherSystem(jaeger);

            // 14. Build and wire the Pacific Rim UI (HUD & Main Menu)
            PacificRimUI.Editor.SetupPacificRimUIScene.SetupUI();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(jaeger.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(jaeger.scene);

            Debug.Log($"<color=#00e5ff><b>[Pacific Rim]</b> Jaeger Mech aligned at Y={targetY:F2} (capsule height={colHeight:F2}, centerY={localCenterY:F2}); Ocean at Y={midThighY:F2}m with Thunderstorm Weather System active!</color>");
        }

        private static void SetupAccurateHitboxes(Transform jaegerRoot)
        {
            Transform container = jaegerRoot.Find("Jaeger_Hitboxes");
            if (container != null) Undo.DestroyObjectImmediate(container.gameObject);

            GameObject hitboxObj = new GameObject("Jaeger_Hitboxes");
            hitboxObj.transform.SetParent(jaegerRoot, false);
            hitboxObj.transform.localPosition = Vector3.zero;
            hitboxObj.transform.localRotation = Quaternion.identity;
            hitboxObj.transform.localScale = Vector3.one;

            // Chest / Cockpit / Turbine
            CreateHitbox(hitboxObj.transform, "Hitbox_Chest", new Vector3(0f, 15.5f, 0.2f), new Vector3(6.5f, 5.5f, 4.0f));

            // Pelvis / Hips
            CreateHitbox(hitboxObj.transform, "Hitbox_Pelvis", new Vector3(0f, 10.5f, 0.0f), new Vector3(4.8f, 3.6f, 3.2f));

            // Head / Conn-Pod
            CreateSphereHitbox(hitboxObj.transform, "Hitbox_Head", new Vector3(0f, 19.8f, 0.8f), 1.5f);

            // Left Leg
            CreateCapsuleHitbox(hitboxObj.transform, "Hitbox_LeftLeg", new Vector3(-2.2f, 5.0f, 0.0f), 10.0f, 1.6f);

            // Right Leg
            CreateCapsuleHitbox(hitboxObj.transform, "Hitbox_RightLeg", new Vector3(2.2f, 5.0f, 0.0f), 10.0f, 1.6f);

            // Left Arm
            CreateCapsuleHitbox(hitboxObj.transform, "Hitbox_LeftArm", new Vector3(-4.8f, 15.0f, 0.0f), 8.5f, 1.4f);

            // Right Arm
            CreateCapsuleHitbox(hitboxObj.transform, "Hitbox_RightArm", new Vector3(4.8f, 15.0f, 0.0f), 8.5f, 1.4f);
        }

        private static void CreateHitbox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
            col.isTrigger = true;
        }

        private static void CreateSphereHitbox(Transform parent, string name, Vector3 center, float radius)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<SphereCollider>();
            col.center = center;
            col.radius = radius;
            col.isTrigger = true;
        }

        private static void CreateCapsuleHitbox(Transform parent, string name, Vector3 center, float height, float radius)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.center = center;
            col.height = height;
            col.radius = radius;
            col.isTrigger = true;
        }

        private static void SetupLimbColliders(Transform root)
        {
            // Find key bones
            var chest = FindChildRecursive(root, "Chest");
            if (chest != null)
            {
                var col = GetOrAddComponent<BoxCollider>(chest.gameObject);
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(5.2f, 4.4f, 3.4f);
                col.isTrigger = true;
            }

            var hips = FindChildRecursive(root, "Hips");
            if (hips != null)
            {
                var col = GetOrAddComponent<BoxCollider>(hips.gameObject);
                col.center = Vector3.zero;
                col.size = new Vector3(3.8f, 2.6f, 2.6f);
                col.isTrigger = true;
            }

            var head = FindChildRecursive(root, "Head");
            if (head != null)
            {
                var col = GetOrAddComponent<SphereCollider>(head.gameObject);
                col.center = new Vector3(0f, 0.5f, 0.2f);
                col.radius = 1.2f;
                col.isTrigger = true;
            }

            var thighL = FindChildRecursive(root, "Thigh.L");
            if (thighL != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(thighL.gameObject);
                col.direction = 1;
                col.radius = 1.0f;
                col.height = 5.0f;
                col.center = new Vector3(0f, -2.2f, 0f);
                col.isTrigger = true;
            }

            var thighR = FindChildRecursive(root, "Thigh.R");
            if (thighR != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(thighR.gameObject);
                col.direction = 1;
                col.radius = 1.0f;
                col.height = 5.0f;
                col.center = new Vector3(0f, -2.2f, 0f);
                col.isTrigger = true;
            }

            var shinL = FindChildRecursive(root, "Shin.L");
            if (shinL != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(shinL.gameObject);
                col.direction = 1;
                col.radius = 0.95f;
                col.height = 5.5f;
                col.center = new Vector3(0f, -2.5f, 0f);
                col.isTrigger = true;
            }

            var shinR = FindChildRecursive(root, "Shin.R");
            if (shinR != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(shinR.gameObject);
                col.direction = 1;
                col.radius = 0.95f;
                col.height = 5.5f;
                col.center = new Vector3(0f, -2.5f, 0f);
                col.isTrigger = true;
            }

            var footL = FindChildRecursive(root, "Foot.L");
            if (footL != null)
            {
                var col = GetOrAddComponent<BoxCollider>(footL.gameObject);
                col.center = new Vector3(0f, -0.4f, 0.4f);
                col.size = new Vector3(1.6f, 0.9f, 3.8f);
                col.isTrigger = true;
            }

            var footR = FindChildRecursive(root, "Foot.R");
            if (footR != null)
            {
                var col = GetOrAddComponent<BoxCollider>(footR.gameObject);
                col.center = new Vector3(0f, -0.4f, 0.4f);
                col.size = new Vector3(1.6f, 0.9f, 3.8f);
                col.isTrigger = true;
            }

            var armL = FindChildRecursive(root, "Upperarm.L");
            if (armL != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(armL.gameObject);
                col.direction = 0; // X-axis
                col.radius = 0.9f;
                col.height = 4.2f;
                col.isTrigger = true;
            }

            var armR = FindChildRecursive(root, "Upperarm.R");
            if (armR != null)
            {
                var col = GetOrAddComponent<CapsuleCollider>(armR.gameObject);
                col.direction = 0;
                col.radius = 0.9f;
                col.height = 4.2f;
                col.isTrigger = true;
            }
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool RepairFbxAnimationImport(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[FBX Import] Could not load ModelImporter for '{fbxPath}'");
                return false;
            }

            importer.globalScale = 0.003f;
            importer.useFileScale = false;
            importer.useFileUnits = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            TakeInfo[] takeInfos = importer.importedTakeInfos;
            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;

            Debug.Log($"<color=#00e5ff><b>[FBX Import]</b> '{fbxPath}': importedTakeInfos={takeInfos?.Length ?? 0}, defaultClipAnimations={defaultClips?.Length ?? 0}</color>");

            bool hasTakes = takeInfos != null && takeInfos.Length > 0;
            bool hasDefaults = defaultClips != null && defaultClips.Length > 0;
            if (!hasTakes && !hasDefaults)
            {
                Debug.LogError($"[FBX Import] Both importedTakeInfos and defaultClipAnimations are empty for '{fbxPath}'.");
                return false;
            }

            var takeLookup = new System.Collections.Generic.Dictionary<string, TakeInfo>(System.StringComparer.OrdinalIgnoreCase);
            if (takeInfos != null)
            {
                foreach (var t in takeInfos)
                {
                    if (!string.IsNullOrEmpty(t.name) && !takeLookup.ContainsKey(t.name)) takeLookup[t.name] = t;
                    if (!string.IsNullOrEmpty(t.defaultClipName) && !takeLookup.ContainsKey(t.defaultClipName)) takeLookup[t.defaultClipName] = t;
                }
            }

            ModelImporterClipAnimation[] repairedClips;
            if (hasDefaults)
            {
                repairedClips = defaultClips;
            }
            else
            {
                repairedClips = new ModelImporterClipAnimation[takeInfos.Length];
                for (int i = 0; i < takeInfos.Length; i++)
                {
                    TakeInfo t = takeInfos[i];
                    float sr = t.sampleRate > 0f ? t.sampleRate : 30f;
                    float startT = t.stopTime > t.startTime ? t.startTime : t.bakeStartTime;
                    float stopT = t.stopTime > t.startTime ? t.stopTime : t.bakeStopTime;
                    repairedClips[i] = new ModelImporterClipAnimation
                    {
                        name = string.IsNullOrEmpty(t.defaultClipName) ? t.name : t.defaultClipName,
                        takeName = t.name,
                        firstFrame = Mathf.Round(startT * sr),
                        lastFrame = Mathf.Round(stopT * sr)
                    };
                }
            }

            foreach (var clip in repairedClips)
            {
                if (clip.lastFrame <= clip.firstFrame)
                {
                    TakeInfo matchedTake;
                    if ((!string.IsNullOrEmpty(clip.takeName) && takeLookup.TryGetValue(clip.takeName, out matchedTake)) ||
                        (!string.IsNullOrEmpty(clip.name) && takeLookup.TryGetValue(clip.name, out matchedTake)))
                    {
                        float sr = matchedTake.sampleRate > 0f ? matchedTake.sampleRate : 30f;
                        float startT = matchedTake.stopTime > matchedTake.startTime ? matchedTake.startTime : matchedTake.bakeStartTime;
                        float stopT = matchedTake.stopTime > matchedTake.startTime ? matchedTake.stopTime : matchedTake.bakeStopTime;
                        clip.firstFrame = Mathf.Round(startT * sr);
                        clip.lastFrame = Mathf.Round(stopT * sr);
                    }
                }

                bool isLocomotionLoop = MatchesAnyClipTarget(clip.name, clip.takeName,
                    "Idle", "Walk", "Run", "Combat Idle", "Combat Block Idle");
                bool isActionClip = MatchesAnyClipTarget(clip.name, clip.takeName,
                    "Combat Punch1", "Combat Punch2", "Combat Punch3",
                    "Combat Heavy Attack", "Combat Heavy Attack Run",
                    "Combat Block Hit1", "Combat Block Hit2", "Emote1");

                clip.loopTime = isLocomotionLoop;
                clip.loop = isLocomotionLoop;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;

                if (isLocomotionLoop || isActionClip)
                {
                    Debug.Log($"<color=#00ffaa>[FBX Clip Repaired]</color> name='{clip.name}', take='{clip.takeName}', frames=[{clip.firstFrame}..{clip.lastFrame}], loopTime={clip.loopTime}");
                }
            }

            importer.clipAnimations = repairedClips;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            return true;
        }

        private static bool MatchesAnyClipTarget(string clipName, string takeName, params string[] targets)
        {
            foreach (string target in targets)
            {
                if (MatchesClipTarget(clipName, target) || MatchesClipTarget(takeName, target))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesClipTarget(string value, string target)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return string.Equals(value, target, System.StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("." + target, System.StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("|" + target, System.StringComparison.OrdinalIgnoreCase);
        }

        private static AnimationClip FindNonPreviewClip(Object[] assets, string target)
        {
            if (assets == null) return null;
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__") && MatchesClipTarget(clip.name, target))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void LogAttackDeformation(GameObject root, AnimationClip clip)
        {
            Transform upperarmL = FindChildRecursive(root.transform, "Upperarm.L");
            Transform upperarmR = FindChildRecursive(root.transform, "Upperarm.R");
            Transform handL = FindChildRecursive(root.transform, "Hand.L");
            Transform handR = FindChildRecursive(root.transform, "Hand.R");

            clip.SampleAnimation(root, 0f);
            Vector3 armL0 = upperarmL != null ? upperarmL.position : Vector3.zero;
            Vector3 armR0 = upperarmR != null ? upperarmR.position : Vector3.zero;
            Vector3 handL0 = handL != null ? handL.position : Vector3.zero;
            Vector3 handR0 = handR != null ? handR.position : Vector3.zero;

            clip.SampleAnimation(root, clip.length * 0.5f);
            float armLDelta = upperarmL != null ? Vector3.Distance(armL0, upperarmL.position) : 0f;
            float armRDelta = upperarmR != null ? Vector3.Distance(armR0, upperarmR.position) : 0f;
            float handLDelta = handL != null ? Vector3.Distance(handL0, handL.position) : 0f;
            float handRDelta = handR != null ? Vector3.Distance(handR0, handR.position) : 0f;

            int curveCount = AnimationUtility.GetCurveBindings(clip).Length;
            Debug.Log($"<color=#ffaa00><b>[Attack Deformation Check]</b></color> Clip '{clip.name}' (len={clip.length:F2}s, curves={curveCount}) -> Upperarm.L={armLDelta:F4}m, Upperarm.R={armRDelta:F4}m, Hand.L={handLDelta:F4}m, Hand.R={handRDelta:F4}m");
        }

        private static bool ComputeCombinedSkinnedBounds(GameObject root, out Bounds totalBounds)
        {
            totalBounds = new Bounds();
            bool hasBounds = false;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!hasBounds)
                {
                    totalBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(r.bounds);
                }
            }
            return hasBounds;
        }

        private static void AddLocomotionTransition(
            AnimatorState from,
            AnimatorState to,
            float duration,
            params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            if (from == null || to == null) return;
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var c in conditions)
            {
                t.AddCondition(c.mode, c.threshold, c.param);
            }
        }

        private static void AddAnyStateAttackTransition(
            AnimatorStateMachine sm,
            AnimatorState target,
            string triggerParam,
            float duration)
        {
            if (sm == null || target == null) return;
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = duration;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        }

        private static void AddAttackReturnTransitions(
            AnimatorState attackState,
            AnimatorState idleState,
            AnimatorState locomotionState,
            AnimatorState combatIdleState,
            float exitTime,
            float duration)
        {
            if (attackState == null) return;

            if (locomotionState != null)
            {
                var toLocomotion = attackState.AddTransition(locomotionState);
                toLocomotion.hasExitTime = true;
                toLocomotion.exitTime = exitTime;
                toLocomotion.duration = duration;
                toLocomotion.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }

            if (combatIdleState != null)
            {
                var toCombatIdle = attackState.AddTransition(combatIdleState);
                toCombatIdle.hasExitTime = true;
                toCombatIdle.exitTime = exitTime;
                toCombatIdle.duration = duration;
                toCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
                toCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            if (idleState != null)
            {
                var toIdle = attackState.AddTransition(idleState);
                toIdle.hasExitTime = true;
                toIdle.exitTime = exitTime;
                toIdle.duration = duration;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
                toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
        }

        [MenuItem("Pacific Rim/Inspect Jaeger Armature", false, 1)]
        public static void InspectJaegerArmature()
        {
            var jaeger = UnityEngine.GameObject.Find("Jaeger_Mech");
            if (jaeger != null)
            {
                if (ComputeCombinedSkinnedBounds(jaeger, out Bounds totalBounds))
                {
                    Debug.Log($"<color=#ffff00><b>[Jaeger Mesh Total World Bounds]</b> Center: {totalBounds.center}, Size: {totalBounds.size}, Min: {totalBounds.min}, Max: {totalBounds.max}</color>");
                }
            }

            string[] inspectedClips =
            {
                "Idle", "Walk", "Run",
                "Combat Punch1", "Combat Punch2", "Combat Punch3",
                "Combat Heavy Attack", "Combat Heavy Attack Run"
            };

            foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Models/GipsyDanger.fbx"))
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (MatchesAnyClipTarget(clip.name, clip.name, inspectedClips))
                    {
                        int curveCount = AnimationUtility.GetCurveBindings(clip).Length;
                        Debug.Log($"<color=#00e5ff>[Clip Info]</color> '{clip.name}': length = {clip.length:F2}s, frameRate = {clip.frameRate}, isLooping = {clip.isLooping}, curves = {curveCount}");
                    }
                }
            }
        }

        private static void LogBones(Transform t, int depth)
        {
            if (depth > 5) return;
            string pad = new string(' ', depth * 2);
            UnityEngine.Debug.Log($"{pad}> {t.name} (pos: {t.localPosition:F2}, localScale: {t.localScale:F3}, lossyScale: {t.lossyScale:F3})");
            for (int i = 0; i < t.childCount; i++)
            {
                LogBones(t.GetChild(i), depth + 1);
            }
        }

        private static void LogTransformRecursive(UnityEngine.Transform t, int indent)
        {
            string pad = new string(' ', indent * 2);
            UnityEngine.Debug.Log($"{pad}- {t.name} (pos: {t.localPosition}, rot: {t.localEulerAngles}, scale: {t.localScale})");
            for (int i = 0; i < t.childCount; i++)
            {
                LogTransformRecursive(t.GetChild(i), indent + 1);
            }
        }

        private static float SetupOceanSystemAtMidThigh(GameObject jaeger, GameObject platform)
        {
            Transform hips = FindChildRecursive(jaeger.transform, "Hips");
            Transform chest = FindChildRecursive(jaeger.transform, "Chest");
            Transform thighL = FindChildRecursive(jaeger.transform, "Thigh.L");
            Transform thighR = FindChildRecursive(jaeger.transform, "Thigh.R");
            Transform shinL = FindChildRecursive(jaeger.transform, "Shin.L");
            Transform shinR = FindChildRecursive(jaeger.transform, "Shin.R");
            Transform footL = FindChildRecursive(jaeger.transform, "Foot.L");
            Transform footR = FindChildRecursive(jaeger.transform, "Foot.R");

            float thighY = (thighL != null && thighR != null)
                ? (thighL.position.y + thighR.position.y) * 0.5f
                : (thighL != null ? thighL.position.y : (thighR != null ? thighR.position.y : 16.04f));
            float shinY = (shinL != null && shinR != null)
                ? (shinL.position.y + shinR.position.y) * 0.5f
                : (shinL != null ? shinL.position.y : (shinR != null ? shinR.position.y : 10.64f));
            float hipsY = hips != null ? hips.position.y : 15.52f;
            float chestY = chest != null ? chest.position.y : 19.20f;

            // Mean ocean surface sits above knee level so lowest wave troughs dip to knee height (~10.76m),
            // average swells hit pelvis (~13.5-14.4m), and rare rogue waves surge up to upper torso (~18.5-19.4m).
            float oceanBaseY = shinY + 2.24f;

            // Remove any prior ocean/splash objects to avoid duplicates
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && (go.name == "ProceduralOcean" || go.name == "WaterSplashManager" || go.name == "ProceduralOceanSplashParticles"))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            // Configure JaegerWaterInteraction with explicit leg transforms and animation-synced footsteps
            var waterInteraction = GetOrAddComponent<JaegerWaterInteraction>(jaeger);
            waterInteraction.leftFootTransform = footL;
            waterInteraction.rightFootTransform = footR;
            waterInteraction.leftShinTransform = shinL;
            waterInteraction.rightShinTransform = shinR;
            waterInteraction.leftThighTransform = thighL;
            waterInteraction.rightThighTransform = thighR;
            waterInteraction.useAnimationSyncedFootsteps = true;
            waterInteraction.minFootstepInterval = 0.30f;
            waterInteraction.crossFootMinInterval = 0.18f;
            waterInteraction.footstepWaveRadius = 18.0f;
            waterInteraction.footstepWaveStrength = 2.4f;
            waterInteraction.strideDistance = 6.0f;
            waterInteraction.footSpacing = 2.4f;
            waterInteraction.legWidth = 2.8f;
            waterInteraction.spawnSplashes = true;
            waterInteraction.splashScale = 0.55f;
            waterInteraction.footstepShakeIntensity = 0.16f;
            waterInteraction.footstepShakeDuration = 0.20f;
            waterInteraction.oceanWadingDrag = 3.5f;
            waterInteraction.deepWaterSpeedMultiplier = 0.78f;
            waterInteraction.oceanBuoyancy = 0f;
            waterInteraction.buoyancyThresholdDepth = 22.0f;

            // Ensure splash clip guard and wave body splash interaction on Jaeger
            var clipGuard = GetOrAddComponent<JaegerSplashClipGuard>(jaeger);
            clipGuard.AutoBuild();
            var waveBody = GetOrAddComponent<JaegerWaveBodyInteraction>(jaeger);
            waveBody.enableBodySplashes = true;
            waveBody.enableCrestBubbles = true;
            waveBody.AutoBuildProbes();

            // Also ensure wave splash components on Leatherback if present
            GameObject kaiju = GameObject.Find("Kaiju_Leatherback");
            if (kaiju != null)
            {
                var kGuard = GetOrAddComponent<JaegerSplashClipGuard>(kaiju);
                kGuard.AutoBuild();
                var kWaveBody = GetOrAddComponent<JaegerWaveBodyInteraction>(kaiju);
                kWaveBody.enableBodySplashes = true;
                kWaveBody.enableCrestBubbles = false;
                kWaveBody.AutoBuildProbes();
            }

            // Create ProceduralOcean positioned cleanly so troughs dip to knees, average swells hit pelvis, and rare rogue waves reach upper torso
            GameObject oceanObj = new GameObject("ProceduralOcean");
            Undo.RegisterCreatedObjectUndo(oceanObj, "Setup Procedural Ocean");
            oceanObj.transform.position = new Vector3(0f, oceanBaseY, 0f);
            oceanObj.transform.rotation = Quaternion.identity;
            oceanObj.transform.localScale = Vector3.one;

            var waterSystem = oceanObj.AddComponent<WaterSystem>();
            waterSystem.swellAmplitude1 = 1.15f;
            waterSystem.swellWavelength1 = 92f;
            waterSystem.swellSpeed1 = 1.15f;
            waterSystem.swellDirection1 = new Vector2(0.951f, 0.309f);
            waterSystem.swellSteepness1 = 0.50f;

            waterSystem.swellAmplitude2 = 0.78f;
            waterSystem.swellWavelength2 = 64f;
            waterSystem.swellSpeed2 = 1.25f;
            waterSystem.swellDirection2 = new Vector2(0.848f, -0.530f);
            waterSystem.swellSteepness2 = 0.46f;

            waterSystem.swellAmplitude3 = 0.52f;
            waterSystem.swellWavelength3 = 45f;
            waterSystem.swellSpeed3 = 1.38f;
            waterSystem.swellDirection3 = new Vector2(0.616f, 0.788f);
            waterSystem.swellSteepness3 = 0.42f;

            waterSystem.chopAmplitude = 0.08f;
            waterSystem.chopWavelength = 5.4f;
            waterSystem.chopSpeed = 2.42f;

            waterSystem.waveSetBoost = 3.10f;
            waterSystem.maxRippleHeight = 2.8f;
            waterSystem.maxTotalWaveHeight = 6.60f;
            waterSystem.maxTroughDepth = 2.25f;
            waterSystem.wakeExpansionSpeed = 7.0f;
            waterSystem.wakeLifetime = 4.2f;

            waterSystem.simResolution = 112;
            waterSystem.simAreaSize = 110f;
            waterSystem.waveSpeed = 0.26f;
            waterSystem.waveDamping = 0.982f;
            waterSystem.absorbEdges = true;
            waterSystem.followTarget = jaeger.transform;

            oceanObj.AddComponent<MeshFilter>();
            var oceanRenderer = oceanObj.AddComponent<MeshRenderer>();
            oceanRenderer.sharedMaterial = EnsureOceanMaterial();
            oceanRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            oceanRenderer.receiveShadows = true;

            var oceanMesh = oceanObj.AddComponent<OceanWaterMesh>();
            oceanMesh.meshWidth = 480f;
            oceanMesh.meshLength = 480f;
            oceanMesh.resolution = 180;
            oceanMesh.foamHeightThreshold = 0.76f;
            oceanMesh.rippleFoamThreshold = 0.20f;
            oceanMesh.followTarget = jaeger.transform;
            oceanMesh.jaegerInteraction = waterInteraction;

            waterSystem.InitializeSimulation();
            oceanMesh.InitMesh();

            // Create WaterSplashManager
            GameObject splashMgrObj = new GameObject("WaterSplashManager");
            Undo.RegisterCreatedObjectUndo(splashMgrObj, "Setup Water Splash Manager");
            splashMgrObj.transform.position = Vector3.zero;
            splashMgrObj.AddComponent<WaterSplashManager>();

            Debug.Log($"<color=#00e5ff><b>[Ocean Calibration]</b></color> ShinY={shinY:F2}m, ThighY={thighY:F2}m, HipsY={hipsY:F2}m, ChestY={chestY:F2}m -> Ocean Y={oceanBaseY:F2}m (maxTotalWaveHeight={waterSystem.maxTotalWaveHeight:F2}m)");
            return oceanBaseY;
        }

        [MenuItem("Pacific Rim/Setup Arena Invisible Barriers", false, 9)]
        public static void SetupArenaInvisibleBarriersMenu()
        {
            SetupArenaBarriers.CreateArenaBarriers();
        }

        [MenuItem("Pacific Rim/Inspect Buildings FBX", false, 11)]
        public static void InspectBuildingsFbxMenu()
        {
            SetupPacificRimCity.InspectBuildingsFbx();
        }

        [MenuItem("Pacific Rim/Setup Ocean Wading Scene", false, 10)]
        public static void CreatePacificRimOceanScene()
        {
            var existingOceanRoot = GameObject.Find("--- Pacific Rim Ocean Scene ---");
            if (existingOceanRoot != null) Undo.DestroyObjectImmediate(existingOceanRoot);

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && (go.name.Contains("Kaiju") || go.name.Contains("Seabed_Floor")))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            Light dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                dirLight.color = Color.white;
                dirLight.intensity = 1.3f;
            }

            SetupCompleteJaegerCharacter();
            Debug.Log("<color=#00ffaa><b>[Pacific Rim]</b> Ocean Wading Scene ready!</color>");
        }

        public static Material EnsureOceanMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Ocean.mat";
            Shader oceanShader = Shader.Find("PacificRim/OceanWater");
            if (oceanShader == null) oceanShader = Shader.Find("Universal Render Pipeline/Lit");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(oceanShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (oceanShader != null && mat.shader != oceanShader)
            {
                mat.shader = oceanShader;
            }

            if (mat.HasProperty("_DeepColor")) mat.SetColor("_DeepColor", new Color(0.008f, 0.032f, 0.068f, 1.0f));
            if (mat.HasProperty("_ShallowColor")) mat.SetColor("_ShallowColor", new Color(0.025f, 0.19f, 0.28f, 1.0f));
            if (mat.HasProperty("_SubsurfaceColor")) mat.SetColor("_SubsurfaceColor", new Color(0.05f, 0.34f, 0.38f, 1.0f));
            if (mat.HasProperty("_FoamColor")) mat.SetColor("_FoamColor", new Color(0.86f, 0.92f, 0.96f, 1.0f));
            if (mat.HasProperty("_FoamThreshold")) mat.SetFloat("_FoamThreshold", 0.80f);
            if (mat.HasProperty("_FoamFalloff")) mat.SetFloat("_FoamFalloff", 0.18f);
            if (mat.HasProperty("_AbsorptionScale")) mat.SetFloat("_AbsorptionScale", 0.35f);
            if (mat.HasProperty("_RefractionStrength")) mat.SetFloat("_RefractionStrength", 0.025f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
            if (mat.HasProperty("_MicroNormalStrength")) mat.SetFloat("_MicroNormalStrength", 0.18f);
            if (mat.HasProperty("_MeniscusStrength")) mat.SetFloat("_MeniscusStrength", 0.45f);
            if (mat.HasProperty("_SpecularColor")) mat.SetColor("_SpecularColor", new Color(0.95f, 0.98f, 1.0f, 1.0f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material EnsureSeabedMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Seabed.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(litShader);
                mat.color = new Color(0.04f, 0.08f, 0.12f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        public static Material EnsureJaegerMaterial()
        {
            EnsureFolder("Assets/Materials");
            const string palettePath = "Assets/Materials/UV_Color_Palette.png";
            if (AssetImporter.GetAtPath(palettePath) is TextureImporter importer)
            {
                bool changed = false;
                if (!importer.sRGBTexture) { importer.sRGBTexture = true; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }

            Shader targetShader = Shader.Find("PacificRim/JaegerWetLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            string path = "Assets/Materials/PacificRim_JaegerArmor.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(targetShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (targetShader != null && mat.shader != targetShader)
            {
                mat.shader = targetShader;
            }

            Texture2D paletteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
            if (paletteTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", paletteTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", paletteTex);
                mat.mainTexture = paletteTex;
            }

            string[] clearTexProps =
            {
                "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap",
                "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask", "_SpecGlossMap", "_ParallaxMap"
            };
            foreach (string texProp in clearTexProps)
            {
                if (mat.HasProperty(texProp)) mat.SetTexture(texProp, null);
            }

            string[] disableKeywords =
            {
                "_NORMALMAP", "_METALLICGLOSSMAP", "_OCCLUSIONMAP", "_EMISSION",
                "_DETAIL_MULX2", "_DETAIL_SCALED", "_SPECGLOSSMAP", "_PARALLAXMAP"
            };
            foreach (string kw in disableKeywords)
            {
                mat.DisableKeyword(kw);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.18f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.48f);
            if (mat.HasProperty("_WetSmoothness")) mat.SetFloat("_WetSmoothness", 0.88f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material EnsureKaijuMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Kaiju.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(litShader);
                mat.color = new Color(0.12f, 0.16f, 0.14f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static RuntimeAnimatorController BuildLocomotionAnimator(string modelFbxPath, string animFbxPath, string outputPath)
        {
            EnsureFolder("Assets/Animations");

            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(outputPath) != null)
            {
                AssetDatabase.DeleteAsset(outputPath);
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(animFbxPath);
            AnimationClip idleClip = FindNonPreviewClip(assets, "Idle");
            AnimationClip combatIdleClip = FindNonPreviewClip(assets, "Combat Idle");
            AnimationClip blockIdleClip = FindNonPreviewClip(assets, "Combat Block Idle");
            AnimationClip blockHit1Clip = FindNonPreviewClip(assets, "Combat Block Hit1");
            AnimationClip emoteClip = FindNonPreviewClip(assets, "Emote1");
            AnimationClip walkClip = FindNonPreviewClip(assets, "Walk");
            AnimationClip runClip = FindNonPreviewClip(assets, "Run");
            AnimationClip punch1Clip = FindNonPreviewClip(assets, "Combat Punch1");
            AnimationClip punch2Clip = FindNonPreviewClip(assets, "Combat Punch2");
            AnimationClip punch3Clip = FindNonPreviewClip(assets, "Combat Punch3");
            AnimationClip heavyClip = FindNonPreviewClip(assets, "Combat Heavy Attack");
            AnimationClip heavyRunClip = FindNonPreviewClip(assets, "Combat Heavy Attack Run");

            var allClips = new (string label, AnimationClip clip)[]
            {
                ("Idle", idleClip),
                ("CombatIdle", combatIdleClip),
                ("BlockIdle", blockIdleClip),
                ("BlockHit1", blockHit1Clip),
                ("Emote", emoteClip),
                ("Walk", walkClip),
                ("Run", runClip),
                ("Punch1", punch1Clip),
                ("Punch2", punch2Clip),
                ("Punch3", punch3Clip),
                ("HeavyAttack", heavyClip),
                ("HeavyAttackRun", heavyRunClip)
            };

            foreach (var c in allClips)
            {
                if (c.clip == null)
                {
                    Debug.LogError($"[Animator Build] Missing '{c.label}' AnimationClip in '{animFbxPath}'");
                }
                else
                {
                    int curves = AnimationUtility.GetCurveBindings(c.clip).Length;
                    Debug.Log($"<color=#00e5ff>[Animator Clip]</color> {c.label} -> '{c.clip.name}': len={c.clip.length:F2}s, fps={c.clip.frameRate}, loop={c.clip.isLooping}, curves={curves}");
                }
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
            controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsBlocking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("BlockHit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Emote", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "RunBlend",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.0f
            });
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "WalkDirection",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f
            });
            controller.AddParameter("Punch1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Punch2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Punch3", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttackRun", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;

            var combatIdleState = rootStateMachine.AddState("CombatIdle");
            combatIdleState.motion = combatIdleClip != null ? combatIdleClip : idleClip;

            var blockIdleState = rootStateMachine.AddState("BlockIdle");
            blockIdleState.motion = blockIdleClip != null ? blockIdleClip : combatIdleClip;

            var blockHitState = rootStateMachine.AddState("BlockHit");
            blockHitState.motion = blockHit1Clip != null ? blockHit1Clip : blockIdleClip;
            blockHitState.speed = 1.0f;

            var emoteState = rootStateMachine.AddState("Emote");
            emoteState.motion = emoteClip != null ? emoteClip : idleClip;
            emoteState.speed = 1.0f;

            // Phase-synchronized 1D BlendTree smoothly blends Walk (0) <-> Run (1) without resetting foot phase
            var locomotionState = CreateWalkRunBlendState(controller, rootStateMachine, walkClip, runClip);

            var punch1State = rootStateMachine.AddState("Punch1");
            punch1State.motion = punch1Clip;
            punch1State.speed = 0.75f;

            var punch2State = rootStateMachine.AddState("Punch2");
            punch2State.motion = punch2Clip;
            punch2State.speed = 0.75f;

            var punch3State = rootStateMachine.AddState("Punch3");
            punch3State.motion = punch3Clip;
            punch3State.speed = 0.75f;

            var heavyState = rootStateMachine.AddState("HeavyAttack");
            heavyState.motion = heavyClip;
            heavyState.speed = 0.75f;

            var heavyRunState = rootStateMachine.AddState("HeavyAttackRun");
            heavyRunState.motion = heavyRunClip;
            heavyRunState.speed = 0.75f;

            rootStateMachine.defaultState = idleState;

            // Base Layer Idle <-> CombatIdle transitions (only replaces Idle when stationary)
            var idleToCombatIdle = idleState.AddTransition(combatIdleState);
            idleToCombatIdle.hasExitTime = false;
            idleToCombatIdle.duration = 0.35f;
            idleToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            idleToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var combatIdleToIdle = combatIdleState.AddTransition(idleState);
            combatIdleToIdle.hasExitTime = false;
            combatIdleToIdle.duration = 0.45f;
            combatIdleToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            combatIdleToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Locomotion transitions with standard Idle and CombatIdle
            // Walking always plays full-body Locomotion
            var idleToLoco = idleState.AddTransition(locomotionState);
            idleToLoco.hasExitTime = false;
            idleToLoco.duration = 0.30f;
            idleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var combatIdleToLoco = combatIdleState.AddTransition(locomotionState);
            combatIdleToLoco.hasExitTime = false;
            combatIdleToLoco.duration = 0.32f;
            combatIdleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Stopping transitions: seamless cross-fades into CombatIdle or standard Idle
            var locoToCombatIdle = locomotionState.AddTransition(combatIdleState);
            locoToCombatIdle.hasExitTime = false;
            locoToCombatIdle.duration = 0.38f;
            locoToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            locoToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var locoToIdle = locomotionState.AddTransition(idleState);
            locoToIdle.hasExitTime = false;
            locoToIdle.duration = 0.36f;
            locoToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            locoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Blocking transitions from CombatIdle / Locomotion / AnyState
            var anyToBlock = rootStateMachine.AddAnyStateTransition(blockIdleState);
            anyToBlock.hasExitTime = false;
            anyToBlock.duration = 0.15f;
            anyToBlock.canTransitionToSelf = false;
            anyToBlock.AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");

            var blockExitToCombatIdle = blockIdleState.AddTransition(combatIdleState);
            blockExitToCombatIdle.hasExitTime = false;
            blockExitToCombatIdle.duration = 0.22f;
            blockExitToCombatIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            blockExitToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            blockExitToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var blockExitToLoco = blockIdleState.AddTransition(locomotionState);
            blockExitToLoco.hasExitTime = false;
            blockExitToLoco.duration = 0.20f;
            blockExitToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            blockExitToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var blockExitToIdle = blockIdleState.AddTransition(idleState);
            blockExitToIdle.hasExitTime = false;
            blockExitToIdle.duration = 0.22f;
            blockExitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            blockExitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            blockExitToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Block hit reaction
            var blockToHit = blockIdleState.AddTransition(blockHitState);
            blockToHit.hasExitTime = false;
            blockToHit.duration = 0.08f;
            blockToHit.AddCondition(AnimatorConditionMode.If, 0f, "BlockHit");

            var hitReturnToBlock = blockHitState.AddTransition(blockIdleState);
            hitReturnToBlock.hasExitTime = true;
            hitReturnToBlock.exitTime = 0.75f;
            hitReturnToBlock.duration = 0.15f;
            hitReturnToBlock.AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");

            var hitReturnToCombatIdle = blockHitState.AddTransition(combatIdleState);
            hitReturnToCombatIdle.hasExitTime = true;
            hitReturnToCombatIdle.exitTime = 0.85f;
            hitReturnToCombatIdle.duration = 0.20f;
            hitReturnToCombatIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            hitReturnToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            hitReturnToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var hitReturnToLoco = blockHitState.AddTransition(locomotionState);
            hitReturnToLoco.hasExitTime = true;
            hitReturnToLoco.exitTime = 0.85f;
            hitReturnToLoco.duration = 0.20f;
            hitReturnToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            hitReturnToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var hitReturnToIdle = blockHitState.AddTransition(idleState);
            hitReturnToIdle.hasExitTime = true;
            hitReturnToIdle.exitTime = 0.85f;
            hitReturnToIdle.duration = 0.20f;
            hitReturnToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
            hitReturnToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            hitReturnToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Emote transitions (AnyState -> Emote, then return to Idle/CombatIdle/Locomotion)
            AddAnyStateAttackTransition(rootStateMachine, emoteState, "Emote", 0.20f);

            var emoteToIdle = emoteState.AddTransition(idleState);
            emoteToIdle.hasExitTime = true;
            emoteToIdle.exitTime = 0.90f;
            emoteToIdle.duration = 0.25f;
            emoteToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "InCombat");
            emoteToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var emoteToCombatIdle = emoteState.AddTransition(combatIdleState);
            emoteToCombatIdle.hasExitTime = true;
            emoteToCombatIdle.exitTime = 0.90f;
            emoteToCombatIdle.duration = 0.25f;
            emoteToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "InCombat");
            emoteToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var emoteToLoco = emoteState.AddTransition(locomotionState);
            emoteToLoco.hasExitTime = true;
            emoteToLoco.exitTime = 0.90f;
            emoteToLoco.duration = 0.25f;
            emoteToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // AnyState -> Attack triggers
            AddAnyStateAttackTransition(rootStateMachine, punch1State, "Punch1", 0.12f);
            AddAnyStateAttackTransition(rootStateMachine, punch2State, "Punch2", 0.12f);
            AddAnyStateAttackTransition(rootStateMachine, punch3State, "Punch3", 0.12f);
            AddAnyStateAttackTransition(rootStateMachine, heavyState, "HeavyAttack", 0.15f);
            AddAnyStateAttackTransition(rootStateMachine, heavyRunState, "HeavyAttackRun", 0.15f);

            // Direct transitions between all distinct punch states for seamless combo chaining without dropping hands
            AddLocomotionTransition(punch1State, punch2State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch2"));
            AddLocomotionTransition(punch1State, punch3State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch3"));
            AddLocomotionTransition(punch2State, punch1State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch1"));
            AddLocomotionTransition(punch2State, punch3State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch3"));
            AddLocomotionTransition(punch3State, punch1State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch1"));
            AddLocomotionTransition(punch3State, punch2State, 0.14f, (AnimatorConditionMode.If, 0f, "Punch2"));

            // Direct combo transitions into heavy attack finishers
            AddLocomotionTransition(punch1State, heavyState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttack"));
            AddLocomotionTransition(punch2State, heavyState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttack"));
            AddLocomotionTransition(punch3State, heavyState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttack"));
            AddLocomotionTransition(punch1State, heavyRunState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttackRun"));
            AddLocomotionTransition(punch2State, heavyRunState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttackRun"));
            AddLocomotionTransition(punch3State, heavyRunState, 0.15f, (AnimatorConditionMode.If, 0f, "HeavyAttackRun"));

            // Attack exit transitions back to Locomotion / CombatIdle / Idle
            AnimatorState[] attackStates = { punch1State, punch2State, punch3State, heavyState, heavyRunState };
            foreach (var attackState in attackStates)
            {
                AddAttackReturnTransitions(attackState, idleState, locomotionState, combatIdleState, 0.88f, 0.28f);
            }

            // Layer 1 ("LowerBody"): prioritizes legs/hips walk/run when moving while attacking
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelFbxPath);
            AvatarMask lowerBodyMask = fbxPrefab != null
                ? BuildLowerBodyAvatarMask(fbxPrefab, "Assets/Animations/GipsyDanger_LowerBody.mask")
                : null;

            controller.AddLayer("LowerBody");
            var layers = controller.layers;
            layers[1].avatarMask = lowerBodyMask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].defaultWeight = 0f;
            controller.layers = layers;

            var lowerSm = controller.layers[1].stateMachine;
            var lowerIdle = lowerSm.AddState("Idle");
            lowerIdle.motion = idleClip;
            var lowerLocomotion = CreateWalkRunBlendState(controller, lowerSm, walkClip, runClip);
            lowerSm.defaultState = lowerIdle;

            AddLocomotionLayerTransitions(lowerIdle, lowerLocomotion);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState CreateWalkRunBlendState(
            AnimatorController controller,
            AnimatorStateMachine sm,
            AnimationClip walkClip,
            AnimationClip runClip)
        {
            BlendTree tree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "RunBlend",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };

            if (AssetDatabase.Contains(controller))
            {
                AssetDatabase.AddObjectToAsset(tree, controller);
            }

            if (walkClip != null) tree.AddChild(walkClip, 0f);
            if (runClip != null) tree.AddChild(runClip, 1f);

            AnimatorState state = sm.AddState("Locomotion");
            state.motion = tree;
            state.speed = 1.0f;
            state.speedParameterActive = true;
            state.speedParameter = "WalkDirection";
            return state;
        }

        private static void AddLocomotionLayerTransitions(
            AnimatorState idleState,
            AnimatorState locomotionState)
        {
            AddLocomotionTransition(idleState, locomotionState, 0.25f,
                (AnimatorConditionMode.Greater, 0.1f, "Speed"));

            AddLocomotionTransition(locomotionState, idleState, 0.25f,
                (AnimatorConditionMode.Less, 0.1f, "Speed"));
        }

        private static AvatarMask BuildLowerBodyAvatarMask(GameObject fbxPrefab, string maskAssetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(maskAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(maskAssetPath);
            }

            AvatarMask mask = new AvatarMask();
            mask.AddTransformPath(fbxPrefab.transform, true);

            int activeCount = 0;
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                bool hasChest = path.Contains("/Chest");
                bool isLowerBody = !hasChest && (
                    path.EndsWith("/Hips") ||
                    path.Contains("Thigh.L") ||
                    path.Contains("Thigh.R") ||
                    path.Contains("Shin.L") ||
                    path.Contains("Shin.R") ||
                    path.Contains("Foot.L") ||
                    path.Contains("Foot.R") ||
                    path.Contains("Toe") ||
                    path.Contains("Heel"));

                mask.SetTransformActive(i, isLowerBody);
                if (isLowerBody) activeCount++;
            }

            AssetDatabase.CreateAsset(mask, maskAssetPath);
            Debug.Log($"<color=#00ffaa><b>[AvatarMask]</b></color> Built '{maskAssetPath}': {activeCount}/{mask.transformCount} lower-body transforms active");
            return mask;
        }

        [MenuItem("Pacific Rim/Setup Thunderstorm Weather System", false, 6)]
        public static void SetupThunderstormWeatherSystemMenu()
        {
            GameObject jaeger = GameObject.Find("Jaeger_Mech");
            SetupThunderstormWeatherSystem(jaeger);
            if (jaeger != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(jaeger.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(jaeger.scene);
            }
        }

        public static void SetupPacificRimUIMenu()
        {
            PacificRimUI.Editor.SetupPacificRimUIScene.SetupUI();
            GameObject jaeger = GameObject.Find("Jaeger_Mech");
            if (jaeger != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(jaeger.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(jaeger.scene);
            }
        }

        public static void SetupThunderstormWeatherSystem(GameObject jaeger)
        {
            EnsureWeatherTextures(out Texture2D rainNormalTex, out Texture2D voronoiTex, out Texture2D mistNoiseTex, out Texture2D stormLutTex);

            Material stormSkyboxMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_StormSkybox.mat", "PacificRim/StormSkybox", m =>
            {
                if (m.HasProperty("_HorizonColor")) m.SetColor("_HorizonColor", new Color(0.07f, 0.09f, 0.13f, 1.0f));
                if (m.HasProperty("_ZenithColor")) m.SetColor("_ZenithColor", new Color(0.015f, 0.025f, 0.045f, 1.0f));
                if (m.HasProperty("_CloudDarkColor")) m.SetColor("_CloudDarkColor", new Color(0.045f, 0.065f, 0.095f, 1.0f));
                if (m.HasProperty("_CloudMidColor")) m.SetColor("_CloudMidColor", new Color(0.12f, 0.16f, 0.22f, 1.0f));
                if (m.HasProperty("_CloudLightColor")) m.SetColor("_CloudLightColor", new Color(0.24f, 0.32f, 0.42f, 1.0f));
                if (m.HasProperty("_ScudCloudColor")) m.SetColor("_ScudCloudColor", new Color(0.035f, 0.050f, 0.075f, 1.0f));
                if (m.HasProperty("_CloudCover")) m.SetFloat("_CloudCover", 0.62f);
                if (m.HasProperty("_CloudSoftness")) m.SetFloat("_CloudSoftness", 0.28f);
                if (m.HasProperty("_CloudDetailScale")) m.SetFloat("_CloudDetailScale", 1.35f);
                if (m.HasProperty("_ScudSpeedMultiplier")) m.SetFloat("_ScudSpeedMultiplier", 2.4f);
                if (m.HasProperty("_CloudNoiseTex")) m.SetTexture("_CloudNoiseTex", mistNoiseTex);
            });

            Material rainStreakMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_RainStreak.mat", "PacificRim/RainStreak", m =>
            {
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.72f, 0.82f, 0.94f, 0.18f));
                if (m.HasProperty("_InvFade")) m.SetFloat("_InvFade", 0.65f);
            });

            Material rainLensMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_RainLens.mat", "PacificRim/RainLens", m =>
            {
                if (m.HasProperty("_RainNormalMap")) m.SetTexture("_RainNormalMap", rainNormalTex);
                if (m.HasProperty("_DistortionStrength")) m.SetFloat("_DistortionStrength", 0.0045f);
                if (m.HasProperty("_WetAlpha")) m.SetFloat("_WetAlpha", 0.10f);
                if (m.HasProperty("_ScrollSpeed")) m.SetFloat("_ScrollSpeed", 0.65f);
            });

            Material boltMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_LightningBolt.mat", "PacificRim/LightningBolt", m =>
            {
                if (m.HasProperty("_CoreColor")) m.SetColor("_CoreColor", new Color(2.2f, 2.8f, 4.0f, 1.0f));
                if (m.HasProperty("_EdgeColor")) m.SetColor("_EdgeColor", new Color(0.35f, 0.55f, 1.5f, 1.0f));
                if (m.HasProperty("_DisplacementStrength")) m.SetFloat("_DisplacementStrength", 1.35f);
                if (m.HasProperty("_JitterSpeed")) m.SetFloat("_JitterSpeed", 38.0f);
                if (m.HasProperty("_FlashAlpha")) m.SetFloat("_FlashAlpha", 1.0f);
            });

            Material sheetMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_SheetLightning.mat", "PacificRim/SheetLightning", m =>
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", voronoiTex);
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.45f, 0.65f, 1.0f, 1.0f));
                if (m.HasProperty("_FlickerIntensity")) m.SetFloat("_FlickerIntensity", 0f);
            });

            Material fogWispMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_FogWisp.mat", "PacificRim/FogWisp", m =>
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", mistNoiseTex);
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.14f, 0.18f, 0.24f, 0.22f));
                if (m.HasProperty("_InvFade")) m.SetFloat("_InvFade", 0.25f);
                if (m.HasProperty("_ScrollSpeed")) m.SetFloat("_ScrollSpeed", 0.18f);
            });

            Material windMistMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_WindMist.mat", "PacificRim/FogWisp", m =>
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", mistNoiseTex);
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.22f, 0.27f, 0.34f, 0.26f));
                if (m.HasProperty("_InvFade")) m.SetFloat("_InvFade", 0.45f);
                if (m.HasProperty("_ScrollSpeed")) m.SetFloat("_ScrollSpeed", 0.42f);
            });

            // Remove any prior StormWeatherSystem to keep setup cleanly idempotent
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && go.name == "StormWeatherSystem")
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            // Dark Storm Overcoat & Light Extinction
            Color stormSlateFog = new Color(0.08f, 0.10f, 0.14f, 1.0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = stormSlateFog;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 155f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.14f, 0.19f, 1.0f);
            RenderSettings.ambientEquatorColor = stormSlateFog;
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.08f, 1.0f);
            RenderSettings.skybox = stormSkyboxMat;
            DynamicGI.UpdateEnvironment();

            foreach (var lightComp in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (lightComp.type == LightType.Directional && lightComp.gameObject.name != "LightningFlashLight")
                {
                    lightComp.color = new Color(0.58f, 0.68f, 0.80f, 1.0f);
                    lightComp.intensity = 0.85f;
                    lightComp.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
                    break;
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = stormSlateFog;
                mainCam.farClipPlane = 450f;
                var camData = mainCam.GetComponent<UniversalAdditionalCameraData>() ?? mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
            }

            // Create StormWeatherSystem root
            GameObject weatherRoot = new GameObject("StormWeatherSystem");
            Undo.RegisterCreatedObjectUndo(weatherRoot, "Setup Thunderstorm Weather System");
            weatherRoot.transform.position = Vector3.zero;

            var weatherMgr = weatherRoot.AddComponent<StormWeatherManager>();
            weatherMgr.windDirection = new Vector2(0.85f, 0.52f);
            weatherMgr.baseWindSpeed = 38.0f;
            weatherMgr.gustStrength = 18.0f;
            weatherMgr.gustFrequency = 0.45f;
            weatherMgr.rainWetness = 1.0f;
            weatherMgr.followTarget = jaeger != null ? jaeger.transform : null;

            // LUT Color Grading Volume
            Volume postVolume = weatherRoot.AddComponent<Volume>();
            postVolume.isGlobal = true;
            postVolume.priority = 10f;
            postVolume.sharedProfile = EnsureStormVolumeProfile(stormLutTex);

            // 1. GPU-Instanced Stretch-Billboard Rain Streaks with Noise Module wind wobble (Pure Typhoon Torrential Downpour)
            GameObject rainGo = new GameObject("RainStreakParticles");
            rainGo.transform.SetParent(weatherRoot.transform, false);
            rainGo.transform.localPosition = new Vector3(0f, 68f, 0f);
            rainGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rainPs = rainGo.AddComponent<ParticleSystem>();
            var rainMain = rainPs.main;
            rainMain.loop = true;
            rainMain.playOnAwake = true;
            rainMain.maxParticles = 7500;
            rainMain.startLifetime = new ParticleSystem.MinMaxCurve(0.68f, 1.05f);
            rainMain.startSpeed = new ParticleSystem.MinMaxCurve(75f, 110f);
            rainMain.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.105f);
            rainMain.startColor = new Color(0.85f, 0.92f, 1.0f, 0.65f);
            rainMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var rainEmission = rainPs.emission;
            rainEmission.enabled = true;
            rainEmission.rateOverTime = 5500f;

            var rainShape = rainPs.shape;
            rainShape.enabled = true;
            rainShape.shapeType = ParticleSystemShapeType.Box;
            rainShape.scale = new Vector3(145f, 145f, 14f);

            var rainVel = rainPs.velocityOverLifetime;
            rainVel.enabled = true;
            rainVel.space = ParticleSystemSimulationSpace.World;
            rainVel.x = 28.0f;
            rainVel.y = -10.0f;
            rainVel.z = 18.0f;

            var rainNoise = rainPs.noise;
            rainNoise.enabled = true;
            rainNoise.strength = 4.2f;
            rainNoise.frequency = 0.85f;
            rainNoise.scrollSpeed = 2.4f;
            rainNoise.damping = true;
            rainNoise.quality = ParticleSystemNoiseQuality.Medium;

            var rainRend = rainGo.GetComponent<ParticleSystemRenderer>();
            rainRend.renderMode = ParticleSystemRenderMode.Stretch;
            rainRend.velocityScale = 0.085f;
            rainRend.lengthScale = 6.8f;
            rainRend.enableGPUInstancing = true;
            rainRend.shadowCastingMode = ShadowCastingMode.Off;
            rainRend.receiveShadows = false;
            rainRend.sharedMaterial = rainStreakMat;
            weatherMgr.rainParticles = rainPs;

            // 2. Screen-Space Lens Distortion Quad (active only during Play Mode)
            GameObject lensQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lensQuad.name = "ScreenSpaceRainLensQuad";
            lensQuad.transform.SetParent(weatherRoot.transform, false);
            var lensCol = lensQuad.GetComponent<Collider>();
            if (lensCol != null) Object.DestroyImmediate(lensCol);
            var lensRend = lensQuad.GetComponent<MeshRenderer>();
            lensRend.sharedMaterial = rainLensMat;
            lensRend.shadowCastingMode = ShadowCastingMode.Off;
            lensRend.receiveShadows = false;
            lensQuad.SetActive(false);
            weatherMgr.rainLensQuad = lensQuad.transform;

            // 3. Lightning System (GI Flash Light, 4 Pooled 3D Bolt Meshes, 4 Sheet Lightning Billboards)
            var lightningSys = weatherRoot.AddComponent<LightningSystem>();
            lightningSys.followCenter = jaeger != null ? jaeger.transform : null;
            lightningSys.flashPeakIntensity = 4.8f;
            lightningSys.minStrikeInterval = 4.0f;
            lightningSys.maxStrikeInterval = 8.0f;

            GameObject flashLightGo = new GameObject("LightningFlashLight");
            flashLightGo.transform.SetParent(weatherRoot.transform, false);
            flashLightGo.transform.rotation = Quaternion.Euler(42f, 145f, 0f);
            var flashLight = flashLightGo.AddComponent<Light>();
            flashLight.type = LightType.Directional;
            flashLight.color = new Color(0.68f, 0.80f, 1.0f, 1.0f);
            flashLight.intensity = 3.6f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;
            lightningSys.flashDirectionalLight = flashLight;

            EnsureFolder("Assets/Meshes/Weather");
            Renderer[] boltPool = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                string meshPath = $"Assets/Meshes/Weather/LightningBolt_{i}.asset";
                Mesh boltMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (boltMesh == null)
                {
                    boltMesh = LightningSystem.BuildLowPolyLightningBoltMesh(101 + i, 14);
                    AssetDatabase.CreateAsset(boltMesh, meshPath);
                }

                GameObject boltGo = new GameObject($"LightningBolt_{i}");
                boltGo.transform.SetParent(weatherRoot.transform, false);
                boltGo.transform.position = new Vector3(0f, 13f, 140f);
                var mf = boltGo.AddComponent<MeshFilter>();
                mf.sharedMesh = boltMesh;
                var mr = boltGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = boltMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                boltPool[i] = mr;
            }
            lightningSys.boltMeshPool = boltPool;

            Renderer[] sheetQuads = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                float angleDeg = i * 90f + 25f;
                Vector3 dir = Quaternion.Euler(0f, angleDeg, 0f) * Vector3.forward;
                GameObject sheetGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sheetGo.name = $"SheetLightning_{i}";
                sheetGo.transform.SetParent(weatherRoot.transform, false);
                var sc = sheetGo.GetComponent<Collider>();
                if (sc != null) Object.DestroyImmediate(sc);
                sheetGo.transform.position = dir * 135f + new Vector3(0f, 72f, 0f);
                sheetGo.transform.rotation = Quaternion.LookRotation(-dir + Vector3.down * 0.25f, Vector3.up);
                sheetGo.transform.localScale = new Vector3(95f, 36f, 1f);
                var sr = sheetGo.GetComponent<MeshRenderer>();
                sr.sharedMaterial = sheetMat;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;
                sheetQuads[i] = sr;
            }
            lightningSys.sheetLightningQuads = sheetQuads;

            // 4. Wind-Swept Fog Wisps (alpha-blended quads panning noise with soft-particle depth fade)
            GameObject wispGo = new GameObject("WindSweptFogWisps");
            wispGo.transform.SetParent(weatherRoot.transform, false);
            wispGo.transform.localPosition = new Vector3(0f, 19f, 0f);
            var wispPs = wispGo.AddComponent<ParticleSystem>();
            var wispMain = wispPs.main;
            wispMain.loop = true;
            wispMain.playOnAwake = true;
            wispMain.maxParticles = 75;
            wispMain.startLifetime = new ParticleSystem.MinMaxCurve(5.0f, 8.5f);
            wispMain.startSpeed = 0f;
            wispMain.startSize = new ParticleSystem.MinMaxCurve(28f, 52f);
            wispMain.startColor = Color.white;
            wispMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var wispEmission = wispPs.emission;
            wispEmission.enabled = true;
            wispEmission.rateOverTime = 9.5f;

            var wispShape = wispPs.shape;
            wispShape.enabled = true;
            wispShape.shapeType = ParticleSystemShapeType.Box;
            wispShape.scale = new Vector3(140f, 12f, 140f);

            var wispVel = wispPs.velocityOverLifetime;
            wispVel.enabled = true;
            wispVel.space = ParticleSystemSimulationSpace.World;
            wispVel.x = 22.0f;
            wispVel.y = 0.35f;
            wispVel.z = 14.0f;

            var wispCol = wispPs.colorOverLifetime;
            wispCol.enabled = true;
            Gradient wispGrad = new Gradient();
            wispGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            wispCol.color = wispGrad;

            var wispRend = wispGo.GetComponent<ParticleSystemRenderer>();
            wispRend.renderMode = ParticleSystemRenderMode.Billboard;
            wispRend.shadowCastingMode = ShadowCastingMode.Off;
            wispRend.receiveShadows = false;
            wispRend.sharedMaterial = fogWispMat;
            weatherMgr.fogWispParticles = wispPs;

            // 5. Wind Mist & Ocean Spray Debris (max 75 particles within 50-100 spec)
            GameObject mistGo = new GameObject("WindMistAndDebris");
            mistGo.transform.SetParent(weatherRoot.transform, false);
            mistGo.transform.localPosition = new Vector3(0f, 22f, 0f);
            var mistPs = mistGo.AddComponent<ParticleSystem>();
            var mistMain = mistPs.main;
            mistMain.loop = true;
            mistMain.playOnAwake = true;
            mistMain.maxParticles = 140;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            mistMain.startSpeed = 0f;
            mistMain.startSize = new ParticleSystem.MinMaxCurve(3.0f, 7.5f);
            mistMain.startColor = Color.white;
            mistMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var mistEmission = mistPs.emission;
            mistEmission.enabled = true;
            mistEmission.rateOverTime = 55f;

            var mistShape = mistPs.shape;
            mistShape.enabled = true;
            mistShape.shapeType = ParticleSystemShapeType.Box;
            mistShape.scale = new Vector3(110f, 20f, 110f);

            var mistVel = mistPs.velocityOverLifetime;
            mistVel.enabled = true;
            mistVel.space = ParticleSystemSimulationSpace.World;
            mistVel.x = 34.0f;
            mistVel.y = -1.5f;
            mistVel.z = 22.0f;

            var mistCol = mistPs.colorOverLifetime;
            mistCol.enabled = true;
            Gradient mistGrad = new Gradient();
            mistGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0f, 1f) });
            mistCol.color = mistGrad;

            var mistRend = mistGo.GetComponent<ParticleSystemRenderer>();
            mistRend.renderMode = ParticleSystemRenderMode.Billboard;
            mistRend.shadowCastingMode = ShadowCastingMode.Off;
            mistRend.receiveShadows = false;
            mistRend.sharedMaterial = windMistMat;
            weatherMgr.windMistParticles = mistPs;

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00ffaa><b>[Weather]</b></color> Configured Thunderstorm Weather System (Rain, Screen-Space Lens, WetLit, Lightning, Fog Wisps, Wind Mist, and 32x32x32 Storm LUT).");
        }

        private static Material EnsureWeatherMaterial(string assetPath, string shaderName, System.Action<Material> configure)
        {
            EnsureFolder("Assets/Materials");
            Shader shader = Shader.Find(shaderName);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader ?? Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static VolumeProfile EnsureStormVolumeProfile(Texture2D stormLutTex)
        {
            EnsureFolder("Assets/Settings");
            string profilePath = "Assets/Settings/PacificRim_StormVolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            if (!profile.TryGet<ColorLookup>(out var colorLookup))
            {
                colorLookup = profile.Add<ColorLookup>(true);
            }

            if (!AssetDatabase.Contains(colorLookup))
            {
                AssetDatabase.AddObjectToAsset(colorLookup, profile);
            }

            colorLookup.active = true;
            colorLookup.texture.Override(stormLutTex);
            colorLookup.contribution.Override(0.85f);

            EditorUtility.SetDirty(colorLookup);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureWeatherTextures(
            out Texture2D rainNormalTex,
            out Texture2D voronoiTex,
            out Texture2D mistNoiseTex,
            out Texture2D stormLutTex)
        {
            EnsureFolder("Assets/Textures/Weather");

            string rainNormalPath = "Assets/Textures/Weather/RainLensNormal.png";
            if (!System.IO.File.Exists(rainNormalPath))
            {
                GenerateRainLensNormalPng(rainNormalPath, 128);
            }
            ConfigureWeatherTextureImporter(rainNormalPath, TextureImporterType.NormalMap, false, true, TextureWrapMode.Repeat, FilterMode.Bilinear, TextureImporterCompression.Compressed, false);
            rainNormalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(rainNormalPath);

            string voronoiPath = "Assets/Textures/Weather/VoronoiNoise.png";
            if (!System.IO.File.Exists(voronoiPath))
            {
                GenerateVoronoiNoisePng(voronoiPath, 128);
            }
            ConfigureWeatherTextureImporter(voronoiPath, TextureImporterType.Default, false, true, TextureWrapMode.Repeat, FilterMode.Bilinear, TextureImporterCompression.Compressed, false);
            voronoiTex = AssetDatabase.LoadAssetAtPath<Texture2D>(voronoiPath);

            string mistPath = "Assets/Textures/Weather/StormMistNoise.png";
            if (!System.IO.File.Exists(mistPath))
            {
                GenerateStormMistNoisePng(mistPath, 128);
            }
            ConfigureWeatherTextureImporter(mistPath, TextureImporterType.Default, true, true, TextureWrapMode.Repeat, FilterMode.Bilinear, TextureImporterCompression.Compressed, false);
            mistNoiseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mistPath);

            string lutPath = "Assets/Textures/Weather/PacificRim_StormLUT.png";
            if (!System.IO.File.Exists(lutPath))
            {
                GenerateStormLutPng(lutPath, 32);
            }
            ConfigureWeatherTextureImporter(lutPath, TextureImporterType.Default, false, false, TextureWrapMode.Clamp, FilterMode.Bilinear, TextureImporterCompression.Uncompressed, true);
            stormLutTex = AssetDatabase.LoadAssetAtPath<Texture2D>(lutPath);
        }

        private static void ConfigureWeatherTextureImporter(
            string path,
            TextureImporterType importerType,
            bool sRGB,
            bool mipmaps,
            TextureWrapMode wrap,
            FilterMode filter,
            TextureImporterCompression compression,
            bool disableNpotScale)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool dirty = false;
                if (importer.textureType != importerType) { importer.textureType = importerType; dirty = true; }
                if (importerType != TextureImporterType.NormalMap && importer.sRGBTexture != sRGB) { importer.sRGBTexture = sRGB; dirty = true; }
                if (importer.mipmapEnabled != mipmaps) { importer.mipmapEnabled = mipmaps; dirty = true; }
                if (importer.wrapMode != wrap) { importer.wrapMode = wrap; dirty = true; }
                if (importer.filterMode != filter) { importer.filterMode = filter; dirty = true; }
                if (importer.textureCompression != compression) { importer.textureCompression = compression; dirty = true; }
                if (disableNpotScale && importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
        }

        private static void GenerateRainLensNormalPng(string path, int size)
        {
            float[,] height = new float[size, size];
            System.Random rng = new System.Random(2026);

            // Tileable sliding rivulet streaks
            for (int s = 0; s < 14; s++)
            {
                float x0 = (float)rng.NextDouble();
                float y0 = (float)rng.NextDouble();
                float len = 0.25f + (float)rng.NextDouble() * 0.45f;
                float radius = 0.016f + (float)rng.NextDouble() * 0.012f;
                float amp = 0.65f + (float)rng.NextDouble() * 0.35f;

                int steps = 28;
                for (int k = 0; k < steps; k++)
                {
                    float frac = (float)k / (steps - 1);
                    float cx = Mathf.Repeat(x0 + Mathf.Sin(frac * 9f + s) * 0.012f, 1f);
                    float cy = Mathf.Repeat(y0 + frac * len, 1f);
                    float r = radius * Mathf.Lerp(0.55f, 1.0f, frac);

                    int pxMin = Mathf.FloorToInt((cx - r) * size) - 1;
                    int pxMax = Mathf.CeilToInt((cx + r) * size) + 1;
                    int pyMin = Mathf.FloorToInt((cy - r) * size) - 1;
                    int pyMax = Mathf.CeilToInt((cy + r) * size) + 1;

                    for (int py = pyMin; py <= pyMax; py++)
                    {
                        for (int px = pxMin; px <= pxMax; px++)
                        {
                            float u = (px + 0.5f) / size;
                            float v = (py + 0.5f) / size;
                            float dx = Mathf.Abs(u - cx); if (dx > 0.5f) dx = 1f - dx;
                            float dy = Mathf.Abs(v - cy); if (dy > 0.5f) dy = 1f - dy;
                            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                            if (d < 1f)
                            {
                                float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)) * amp;
                                int ix = ((px % size) + size) % size;
                                int iy = ((py % size) + size) % size;
                                height[ix, iy] = Mathf.Max(height[ix, iy], dome);
                            }
                        }
                    }
                }
            }

            // Tileable stationary droplets
            for (int dIdx = 0; dIdx < 45; dIdx++)
            {
                float cx = (float)rng.NextDouble();
                float cy = (float)rng.NextDouble();
                float r = 0.014f + (float)rng.NextDouble() * 0.018f;
                int pxMin = Mathf.FloorToInt((cx - r) * size) - 1;
                int pxMax = Mathf.CeilToInt((cx + r) * size) + 1;
                int pyMin = Mathf.FloorToInt((cy - r) * size) - 1;
                int pyMax = Mathf.CeilToInt((cy + r) * size) + 1;

                for (int py = pyMin; py <= pyMax; py++)
                {
                    for (int px = pxMin; px <= pxMax; px++)
                    {
                        float u = (px + 0.5f) / size;
                        float v = (py + 0.5f) / size;
                        float dx = Mathf.Abs(u - cx); if (dx > 0.5f) dx = 1f - dx;
                        float dy = Mathf.Abs(v - cy); if (dy > 0.5f) dy = 1f - dy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        if (dist < 1f)
                        {
                            float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - dist * dist));
                            int ix = ((px % size) + size) % size;
                            int iy = ((py % size) + size) % size;
                            height[ix, iy] = Mathf.Max(height[ix, iy], dome);
                        }
                    }
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;
                    float dx = (height[xp, y] - height[xm, y]) * 2.4f;
                    float dy = (height[x, yp] - height[x, ym]) * 2.4f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateVoronoiNoisePng(string path, int size)
        {
            const int cells = 6;
            Vector2[,] seeds = new Vector2[cells, cells];
            System.Random rng = new System.Random(4096);
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    seeds[cx, cy] = new Vector2(cx + (float)rng.NextDouble(), cy + (float)rng.NextDouble());
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float gy = (float)y / size * cells;
                int iy = Mathf.FloorToInt(gy);
                for (int x = 0; x < size; x++)
                {
                    float gx = (float)x / size * cells;
                    int ix = Mathf.FloorToInt(gx);
                    float minD = 9f;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int ncx = ((ix + ox) % cells + cells) % cells;
                            int ncy = ((iy + oy) % cells + cells) % cells;
                            Vector2 pt = seeds[ncx, ncy] + new Vector2(ix + ox - ncx, iy + oy - ncy);
                            float d = Vector2.Distance(new Vector2(gx, gy), pt);
                            if (d < minD) minD = d;
                        }
                    }

                    float val = Mathf.Pow(Mathf.Clamp01(1f - minD * 1.1f), 1.6f);
                    pixels[y * size + x] = new Color(val, val, val, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateStormMistNoisePng(string path, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float val = TileableValueNoise(u, v, 4, 17) * 0.55f
                              + TileableValueNoise(u, v, 8, 53) * 0.30f
                              + TileableValueNoise(u, v, 16, 97) * 0.15f;
                    val = Mathf.Clamp01(val);
                    pixels[y * size + x] = new Color(val, val, val, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static float TileableValueNoise(float u, float v, int period, int seed)
        {
            float gx = u * period;
            float gy = v * period;
            int x0 = Mathf.FloorToInt(gx) % period;
            int y0 = Mathf.FloorToInt(gy) % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;

            float tx = gx - Mathf.Floor(gx);
            float ty = gy - Mathf.Floor(gy);
            float sx = tx * tx * (3f - 2f * tx);
            float sy = ty * ty * (3f - 2f * ty);

            float n00 = Hash2D(x0, y0, seed);
            float n10 = Hash2D(x1, y0, seed);
            float n01 = Hash2D(x0, y1, seed);
            float n11 = Hash2D(x1, y1, seed);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        private static float Hash2D(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        private static void GenerateStormLutPng(string path, int lutSize)
        {
            int width = lutSize * lutSize;
            int height = lutSize;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            Color[] pixels = new Color[width * height];
            float inv = 1f / (lutSize - 1);

            for (int g = 0; g < lutSize; g++)
            {
                float green = g * inv;
                for (int b = 0; b < lutSize; b++)
                {
                    float blue = b * inv;
                    for (int r = 0; r < lutSize; r++)
                    {
                        float red = r * inv;

                        float lum = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
                        float dr = Mathf.Lerp(red, lum, 0.28f);
                        float dg = Mathf.Lerp(green, lum, 0.28f);
                        float db = Mathf.Lerp(blue, lum, 0.28f);

                        // High-contrast S-curve
                        dr = Mathf.Lerp(dr, dr * dr * (3f - 2f * dr), 0.38f);
                        dg = Mathf.Lerp(dg, dg * dg * (3f - 2f * dg), 0.38f);
                        db = Mathf.Lerp(db, db * db * (3f - 2f * db), 0.38f);

                        // Moody dark slate/steel storm grading
                        float shadowWeight = 1f - lum;
                        dr = Mathf.Clamp01(dr * Mathf.Lerp(0.95f, 0.86f, shadowWeight));
                        dg = Mathf.Clamp01(dg * Mathf.Lerp(0.99f, 0.96f, shadowWeight));
                        db = Mathf.Clamp01(db * Mathf.Lerp(1.04f, 1.12f, shadowWeight) + 0.015f * shadowWeight);

                        pixels[g * width + b * lutSize + r] = new Color(dr, dg, db, 1f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        [MenuItem("Pacific Rim/Setup Industrial Atmosphere and Background Scale", false, 8)]
        public static void SetupAtmosphereAndScaleElements()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log("[PacificRim] Starting atmospheric environment & background scale setup...");

            // 1. Atmosphere Root Manager
            GameObject atmoRoot = GameObject.Find("Atmosphere_Systems");
            if (atmoRoot == null)
            {
                atmoRoot = new GameObject("Atmosphere_Systems");
                Undo.RegisterCreatedObjectUndo(atmoRoot, "Create Atmosphere_Systems");
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(atmoRoot);
            atmoRoot.transform.position = Vector3.zero;
            atmoRoot.transform.rotation = Quaternion.identity;

            // 2. Setup Atmosphere Volume & Ambient Lighting
            var volCtrl = atmoRoot.GetComponent<AtmosphereVolumeController>();
            if (volCtrl == null) volCtrl = atmoRoot.AddComponent<AtmosphereVolumeController>();
            Undo.RecordObject(volCtrl, "Configure AtmosphereVolumeController");
            volCtrl.ApplyAtmosphericLightingAndFog();
            volCtrl.ConfigureGlobalVolumeProfile();
            volCtrl.EnsureAnamorphicFlareQuad();

            // 3. Camera Trauma Controller
            var traumaCtrl = atmoRoot.GetComponent<HeavyImpactCameraTraumaController>();
            if (traumaCtrl == null) traumaCtrl = atmoRoot.AddComponent<HeavyImpactCameraTraumaController>();
            Undo.RecordObject(traumaCtrl, "Configure HeavyImpactCameraTraumaController");
            traumaCtrl.enablePeriodicSubBassImpact = false;

            // 4. Main Camera Screen-Space Lens Rain & Audio Low-Pass
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }

            if (mainCam != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(mainCam.gameObject);
                var lensRain = mainCam.GetComponent<CameraLensRainController>();
                if (lensRain == null) lensRain = mainCam.gameObject.AddComponent<CameraLensRainController>();
                Undo.RecordObject(lensRain, "Configure CameraLensRainController");
                lensRain.RebuildLensQuad();

                var lowPass = mainCam.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = mainCam.gameObject.AddComponent<AudioLowPassFilter>();
                Undo.RecordObject(lowPass, "Configure AudioLowPassFilter");
                lowPass.cutoffFrequency = 22000f;
                lowPass.lowpassResonanceQ = 1.0f;
            }

            // 5. Atmosphere Audio Controller
            var audioCtrl = atmoRoot.GetComponent<AtmosphereAudioController>();
            if (audioCtrl == null) audioCtrl = atmoRoot.AddComponent<AtmosphereAudioController>();
            Undo.RecordObject(audioCtrl, "Configure AtmosphereAudioController");
            audioCtrl.EnsureAudioSources();
            audioCtrl.EnsureRealClips();

            // 6. Background Scale Elements - Distant Megastructure / Sea Wall
            GameObject seaWallObj = GameObject.Find("Megastructure_SeaWall");
            if (seaWallObj == null)
            {
                seaWallObj = new GameObject("Megastructure_SeaWall");
                Undo.RegisterCreatedObjectUndo(seaWallObj, "Create Megastructure_SeaWall");
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(seaWallObj);
            seaWallObj.transform.position = new Vector3(0f, 0f, 260f);
            seaWallObj.transform.rotation = Quaternion.identity;
            var seaWall = seaWallObj.GetComponent<MegastructureSeaWall>();
            if (seaWall == null) seaWall = seaWallObj.AddComponent<MegastructureSeaWall>();
            Undo.RecordObject(seaWall, "Configure MegastructureSeaWall");
            seaWall.BuildSeaWall();

            // 7. Mid-Ground Scale Elements - Patrol Helicopter Rig (Removed per user request)
            GameObject heliObj = GameObject.Find("Patrol_Helicopter_Alpha");
            if (heliObj != null)
            {
                Undo.DestroyObjectImmediate(heliObj);
            }

            // 8. Mid-Ground Scale Elements - Floating Debris Field
            GameObject debrisGroup = GameObject.Find("Floating_Debris_Field");
            if (debrisGroup == null)
            {
                debrisGroup = new GameObject("Floating_Debris_Field");
                Undo.RegisterCreatedObjectUndo(debrisGroup, "Create Floating_Debris_Field");
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(debrisGroup);
            debrisGroup.transform.position = Vector3.zero;

            var debrisConfigs = new[]
            {
                new { name = "ShippingContainer_01_Rust", type = FloatingDebris.DebrisType.ShippingContainer, pos = new Vector3(-35f, 0f, 45f), color = new Color(0.82f, 0.32f, 0.12f) },
                new { name = "ShippingContainer_02_Teal", type = FloatingDebris.DebrisType.ShippingContainer, pos = new Vector3(42f, 0f, 62f), color = new Color(0.12f, 0.45f, 0.55f) },
                new { name = "MaritimeBuoy_01", type = FloatingDebris.DebrisType.MaritimeBuoy, pos = new Vector3(-18f, 0f, 32f), color = new Color(0.95f, 0.65f, 0.05f) },
                new { name = "MaritimeBuoy_02", type = FloatingDebris.DebrisType.MaritimeBuoy, pos = new Vector3(28f, 0f, 88f), color = new Color(0.95f, 0.65f, 0.05f) },
                new { name = "IndustrialHullPlate_01", type = FloatingDebris.DebrisType.IndustrialWreckage, pos = new Vector3(-55f, 0f, 70f), color = new Color(0.25f, 0.28f, 0.32f) }
            };

            foreach (var cfg in debrisConfigs)
            {
                Transform childT = debrisGroup.transform.Find(cfg.name);
                GameObject itemObj;
                if (childT == null)
                {
                    itemObj = new GameObject(cfg.name);
                    itemObj.transform.SetParent(debrisGroup.transform, false);
                    Undo.RegisterCreatedObjectUndo(itemObj, "Create " + cfg.name);
                }
                else
                {
                    itemObj = childT.gameObject;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(itemObj);
                itemObj.transform.position = cfg.pos;
                var debris = itemObj.GetComponent<FloatingDebris>();
                if (debris == null) debris = itemObj.AddComponent<FloatingDebris>();
                Undo.RecordObject(debris, "Configure " + cfg.name);
                debris.debrisType = cfg.type;
                debris.tintColor = cfg.color;
                debris.BuildDebris();
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=#00FFAA><b>[PacificRim] Atmospheric Environment and Scale-Anchoring Elements successfully configured & saved!</b></color>");
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
#endif



