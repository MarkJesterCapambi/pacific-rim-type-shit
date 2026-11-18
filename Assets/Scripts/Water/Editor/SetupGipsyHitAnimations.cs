using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace PacificRimWater.Editor
{
    public static class SetupGipsyHitAnimations
    {
        private const string ControllerPath = "Assets/Animations/GipsyDanger_Locomotion.controller";
        private const string SourceFbxPath = "Assets/Models/GipsyDanger.fbx";
        private const string HitLeftClipAssetPath = "Assets/Animations/Gipsy_CombatHitLeft.anim";
        private const string HitRightClipAssetPath = "Assets/Animations/Gipsy_CombatHitRight.anim";

        [MenuItem("Pacific Rim/Setup Gipsy Hit Animations")]
        public static void Setup()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("Could not find GipsyDanger_Locomotion.controller");
                return;
            }

            var fbxAssets = AssetDatabase.LoadAllAssetsAtPath(SourceFbxPath);
            AnimationClip hit1Clip = null; // Stagger Right
            AnimationClip hit2Clip = null; // Stagger Left
            foreach (var a in fbxAssets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    if (c.name == "Armature|Gipsy.Combat Hit1") hit1Clip = c;
                    if (c.name == "Armature|Gipsy.Combat Hit2") hit2Clip = c;
                }
            }

            if (hit1Clip == null || hit2Clip == null)
            {
                Debug.LogError($"Clips not found! hit1={hit1Clip}, hit2={hit2Clip}");
                return;
            }

            // Build smooth impact -> heavy hold -> return-to-stance clips from the existing FBX animations
            // (The raw FBX hit takes are 0.30s one-way clips ending at peak recoil with no return frames.)
            AnimationClip smoothedRightClip = BuildSmoothedImpactRecoveryClip(hit1Clip, HitRightClipAssetPath, "Gipsy_CombatHitRight");
            AnimationClip smoothedLeftClip = BuildSmoothedImpactRecoveryClip(hit2Clip, HitLeftClipAssetPath, "Gipsy_CombatHitLeft");

            EnsureTrigger(controller, "HitLeft");
            EnsureTrigger(controller, "HitRight");

            var baseStateMachine = controller.layers[0].stateMachine;

            AnimatorState combatIdleState = null;
            AnimatorState locomotionState = null;
            AnimatorState idleState = null;

            foreach (var childState in baseStateMachine.states)
            {
                if (childState.state.name == "CombatIdle") combatIdleState = childState.state;
                else if (childState.state.name == "Locomotion") locomotionState = childState.state;
                else if (childState.state.name == "Idle") idleState = childState.state;
            }

            var hitLeftState = GetOrCreateState(baseStateMachine, "HitLeft", smoothedLeftClip, new Vector3(320, 260, 0));
            var hitRightState = GetOrCreateState(baseStateMachine, "HitRight", smoothedRightClip, new Vector3(320, 320, 0));
            hitLeftState.speed = 1.0f;
            hitRightState.speed = 1.0f;

            SetupAnyStateTransition(baseStateMachine, hitLeftState, "HitLeft");
            SetupAnyStateTransition(baseStateMachine, hitRightState, "HitRight");

            SetupExitTransitions(hitLeftState, combatIdleState, locomotionState, idleState);
            SetupExitTransitions(hitRightState, combatIdleState, locomotionState, idleState);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00ff88><b>[SetupGipsyHitAnimations]</b></color> Built smooth impact-and-recovery clips and configured HitLeft / HitRight!");
        }

        private static AnimationClip BuildSmoothedImpactRecoveryClip(AnimationClip sourceClip, string assetPath, string clipName)
        {
            const float impactEnd = 0.22f;
            const float holdEnd = 0.28f;
            const float recoverEnd = 0.58f;

            float srcLen = Mathf.Max(0.05f, sourceClip.length);
            float recoverSpan = recoverEnd - holdEnd;

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            AnimationClip dst = existing != null ? existing : new AnimationClip();
            dst.ClearCurves();
            dst.name = clipName;
            dst.frameRate = 30f;
            dst.wrapMode = WrapMode.Once;

            var settings = AnimationUtility.GetAnimationClipSettings(dst);
            settings.loopTime = false;
            settings.loopBlend = false;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(dst, settings);

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            var keyList = new List<Keyframe>(24);

            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding b = bindings[i];
                AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(sourceClip, b);
                if (srcCurve == null || srcCurve.length == 0) continue;

                keyList.Clear();

                // 1. Impact phase: map [0 .. srcLen] -> [0 .. impactEnd]
                for (int k = 0; k < srcCurve.length; k++)
                {
                    float norm = Mathf.Clamp01(srcCurve[k].time / srcLen);
                    float t = norm * impactEnd;
                    keyList.Add(new Keyframe(t, srcCurve[k].value));
                }

                // 2. Heavy recoil hold at peak [impactEnd .. holdEnd]
                float peakVal = srcCurve.Evaluate(srcLen);
                if (Mathf.Abs(keyList[keyList.Count - 1].time - impactEnd) > 0.001f)
                {
                    keyList.Add(new Keyframe(impactEnd, peakVal));
                }
                keyList.Add(new Keyframe(holdEnd, peakVal));

                // 3. Smooth eased return back to t=0 rest pose [holdEnd .. recoverEnd]
                const int recoverSteps = 9;
                for (int s = 1; s <= recoverSteps; s++)
                {
                    float u = (float)s / recoverSteps;
                    float eased = Mathf.SmoothStep(0f, 1f, u);
                    float srcT = Mathf.Lerp(srcLen, 0f, eased);
                    float dstT = holdEnd + u * recoverSpan;
                    keyList.Add(new Keyframe(dstT, srcCurve.Evaluate(srcT)));
                }

                AnimationCurve newCurve = new AnimationCurve(keyList.ToArray());
                for (int k = 0; k < newCurve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(newCurve, k, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(newCurve, k, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(dst, b, newCurve);
            }

            dst.EnsureQuaternionContinuity();

            if (existing == null)
            {
                AssetDatabase.CreateAsset(dst, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(dst);
            }

            return dst;
        }

        private static void EnsureTrigger(AnimatorController controller, string name)
        {
            if (!controller.parameters.Any(p => p.name == name))
            {
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            }
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 position)
        {
            foreach (var cs in sm.states)
            {
                if (cs.state.name == name)
                {
                    cs.state.motion = clip;
                    return cs.state;
                }
            }
            var state = sm.AddState(name, position);
            state.motion = clip;
            return state;
        }

        private static void SetupAnyStateTransition(AnimatorStateMachine sm, AnimatorState targetState, string triggerName)
        {
            for (int i = sm.anyStateTransitions.Length - 1; i >= 0; i--)
            {
                var tr = sm.anyStateTransitions[i];
                if (tr.destinationState == targetState)
                {
                    sm.RemoveAnyStateTransition(tr);
                }
            }

            var t = sm.AddAnyStateTransition(targetState);
            t.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            t.duration = 0.08f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
        }

        private static void SetupExitTransitions(AnimatorState hitState, AnimatorState combatIdle, AnimatorState locomotion, AnimatorState idle)
        {
            hitState.transitions = new AnimatorStateTransition[0];

            if (combatIdle != null)
            {
                var t = hitState.AddTransition(combatIdle);
                t.hasExitTime = true;
                t.exitTime = 0.88f;
                t.duration = 0.16f;
                t.AddCondition(AnimatorConditionMode.If, 0, "InCombat");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            if (locomotion != null)
            {
                var t = hitState.AddTransition(locomotion);
                t.hasExitTime = true;
                t.exitTime = 0.88f;
                t.duration = 0.16f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }

            if (idle != null)
            {
                var t = hitState.AddTransition(idle);
                t.hasExitTime = true;
                t.exitTime = 0.88f;
                t.duration = 0.16f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombat");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
        }
    }
}
