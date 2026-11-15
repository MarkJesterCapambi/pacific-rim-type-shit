using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

namespace PacificRimWater.Editor
{
    public static class SetupGipsyHitAnimations
    {
        [MenuItem("Pacific Rim/Setup Gipsy Hit Animations")]
        public static void Setup()
        {
            string controllerPath = "Assets/Animations/GipsyDanger_Locomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError("Could not find GipsyDanger_Locomotion.controller");
                return;
            }

            var fbxAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/GipsyDanger.fbx");
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

            // Ensure parameters exist
            EnsureTrigger(controller, "HitLeft");
            EnsureTrigger(controller, "HitRight");

            var baseStateMachine = controller.layers[0].stateMachine;

            // Find existing exit states
            AnimatorState combatIdleState = null;
            AnimatorState locomotionState = null;
            AnimatorState idleState = null;

            foreach (var childState in baseStateMachine.states)
            {
                if (childState.state.name == "CombatIdle") combatIdleState = childState.state;
                else if (childState.state.name == "Locomotion") locomotionState = childState.state;
                else if (childState.state.name == "Idle") idleState = childState.state;
            }

            // Setup HitLeft (uses Hit2 clip: shifts body left)
            var hitLeftState = GetOrCreateState(baseStateMachine, "HitLeft", hit2Clip, new Vector3(320, 260, 0));
            // Setup HitRight (uses Hit1 clip: shifts body right)
            var hitRightState = GetOrCreateState(baseStateMachine, "HitRight", hit1Clip, new Vector3(320, 320, 0));

            // AnyState transitions
            SetupAnyStateTransition(baseStateMachine, hitLeftState, "HitLeft");
            SetupAnyStateTransition(baseStateMachine, hitRightState, "HitRight");

            // Exit transitions
            SetupExitTransitions(hitLeftState, combatIdleState, locomotionState, idleState);
            SetupExitTransitions(hitRightState, combatIdleState, locomotionState, idleState);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00ff88><b>[SetupGipsyHitAnimations]</b></color> Successfully configured HitLeft and HitRight states and transitions!");
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
            t.duration = 0.06f;
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
                t.exitTime = 0.85f;
                t.duration = 0.12f;
                t.AddCondition(AnimatorConditionMode.If, 0, "InCombat");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            if (locomotion != null)
            {
                var t = hitState.AddTransition(locomotion);
                t.hasExitTime = true;
                t.exitTime = 0.85f;
                t.duration = 0.12f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }

            if (idle != null)
            {
                var t = hitState.AddTransition(idle);
                t.hasExitTime = true;
                t.exitTime = 0.85f;
                t.duration = 0.12f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "InCombat");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
        }
    }
}
