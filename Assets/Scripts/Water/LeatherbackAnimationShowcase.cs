using System;
using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Showcase script for the Leatherback Kaiju.
    /// Smoothly rotates through all 14 animations in sequence to verify
    /// the mesh, skinned rig, textures, and animations in-game.
    /// Supports automatic rotation and manual hotkey controls (N/B/P/R/1-9).
    /// </summary>
    public class LeatherbackAnimationShowcase : MonoBehaviour
    {
        [System.Serializable]
        public struct ClipEntry
        {
            public string stateName;
            public string displayName;
            public float duration;
            public bool isLooping;

            public ClipEntry(string stateName, string displayName, float duration, bool isLooping)
            {
                this.stateName = stateName;
                this.displayName = displayName;
                this.duration = duration;
                this.isLooping = isLooping;
            }
        }

        [Header("Animation Reference")]
        public Animator animator;

        [Header("Showcase Rotation Settings")]
        [Tooltip("Seconds to show looping animations (Idle, Walk, Run, Block) before auto-advancing.")]
        public float loopClipDisplayDuration = 4.0f;
        [Tooltip("Delay in seconds after a one-shot attack/stagger finishes before advancing.")]
        public float postAttackHoldDuration = 0.6f;
        [Tooltip("Cross-fade transition duration between animations.")]
        public float crossFadeDuration = 0.25f;
        [Tooltip("Whether to automatically rotate through all animations.")]
        public bool autoRotate = true;

        [Header("Visual Floating HUD")]
        [Tooltip("Show an in-game GUI overlay displaying the active animation and hotkeys.")]
        public bool showOnScreenHUD = true;
        [Tooltip("Height offset above root to draw the 3D nameplate.")]
        public float hudHeightOffset = 22f;

        [Header("Water Splashes & Ocean Shockwaves")]
        [Tooltip("Trigger massive water displacement waves during slams and stomps.")]
        public bool enableWaterShockwaves = true;

        public readonly List<ClipEntry> clips = new List<ClipEntry>
        {
            new ClipEntry("Idle",          "01 // Idle Stance",               3.63f, true),
            new ClipEntry("Walk",          "02 // Ocean Wading Walk",         4.00f, true),
            new ClipEntry("Run",           "03 // Quadruped Charge Run",      1.33f, true),
            new ClipEntry("PunchLeft",     "04 // Left Heavy Hook",           0.97f, false),
            new ClipEntry("PunchRight",    "05 // Right Heavy Hook",          0.97f, false),
            new ClipEntry("Slam",          "06 // Two-Hand Ground Slam",      1.13f, false),
            new ClipEntry("EMP",           "07 // EMP Bio-Discharge",         2.63f, false),
            new ClipEntry("BlockEnter",    "08 // Armored Block (Enter)",     0.97f, false),
            new ClipEntry("BlockLoop",     "09 // Armored Block (Hold Loop)", 0.97f, true),
            new ClipEntry("StaggerLeft",   "10 // Stagger React (Left)",      1.97f, false),
            new ClipEntry("StaggerRight",  "11 // Stagger React (Right)",     1.97f, false),
            new ClipEntry("StaggerUp",     "12 // Heavy Recoil Upward",       2.30f, false),
            new ClipEntry("StaggerDown",   "13 // Heavy Stagger Downward",    2.30f, false),
            new ClipEntry("Death",         "14 // Fatal Collapse",            4.30f, false),
            new ClipEntry("DeathLoop",     "15 // Submerged Carcass",         3.30f, true),
        };

        private int currentIndex = 0;
        private float currentClipTimer = 0f;
        private float currentClipTargetDuration = 4f;
        private Camera mainCam;

        public int CurrentIndex => currentIndex;
        public ClipEntry CurrentClip => clips[currentIndex];
        public float TimeRemaining => Mathf.Max(0f, currentClipTargetDuration - currentClipTimer);

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            if (GetComponent<JaegerSplashClipGuard>() == null)
                gameObject.AddComponent<JaegerSplashClipGuard>();

            var waveBody = GetComponent<JaegerWaveBodyInteraction>();
            if (waveBody == null)
            {
                waveBody = gameObject.AddComponent<JaegerWaveBodyInteraction>();
                waveBody.enableCrestBubbles = false;
            }

            mainCam = Camera.main;
        }

        private void Start()
        {
            PlayClip(0);
        }

        private void Update()
        {
            HandleInput();

            if (autoRotate)
            {
                currentClipTimer += Time.deltaTime;
                if (currentClipTimer >= currentClipTargetDuration)
                {
                    NextClip();
                }
            }
        }

        private void HandleInput()
        {
            bool nextPressed = false;
            bool prevPressed = false;
            bool pausePressed = false;
            bool replayPressed = false;
            int directIndex = -1;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.nKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame) nextPressed = true;
                if (kb.bKey.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame) prevPressed = true;
                if (kb.pKey.wasPressedThisFrame) pausePressed = true;
                if (kb.rKey.wasPressedThisFrame) replayPressed = true;

                if (kb.digit1Key.wasPressedThisFrame) directIndex = 0;
                else if (kb.digit2Key.wasPressedThisFrame) directIndex = 1;
                else if (kb.digit3Key.wasPressedThisFrame) directIndex = 2;
                else if (kb.digit4Key.wasPressedThisFrame) directIndex = 3;
                else if (kb.digit5Key.wasPressedThisFrame) directIndex = 4;
                else if (kb.digit6Key.wasPressedThisFrame) directIndex = 5;
                else if (kb.digit7Key.wasPressedThisFrame) directIndex = 6;
                else if (kb.digit8Key.wasPressedThisFrame) directIndex = 7;
                else if (kb.digit9Key.wasPressedThisFrame) directIndex = 8;
                else if (kb.digit0Key.wasPressedThisFrame) directIndex = 9;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightBracket)) nextPressed = true;
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftBracket)) prevPressed = true;
            if (Input.GetKeyDown(KeyCode.P)) pausePressed = true;
            if (Input.GetKeyDown(KeyCode.R)) replayPressed = true;

            for (int i = 0; i < 9 && i < clips.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    directIndex = i;
                    break;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha0)) directIndex = 9;
#endif

            if (nextPressed)
                NextClip();
            else if (prevPressed)
                PrevClip();
            else if (pausePressed)
                autoRotate = !autoRotate;
            else if (replayPressed)
                PlayClip(currentIndex);
            else if (directIndex >= 0 && directIndex < clips.Count)
                PlayClip(directIndex);
        }

        public void NextClip()
        {
            int next = (currentIndex + 1) % clips.Count;
            PlayClip(next);
        }

        public void PrevClip()
        {
            int prev = (currentIndex - 1 + clips.Count) % clips.Count;
            PlayClip(prev);
        }

        public void PlayClip(int index)
        {
            if (clips == null || clips.Count == 0) return;
            currentIndex = Mathf.Clamp(index, 0, clips.Count - 1);
            currentClipTimer = 0f;

            var entry = clips[currentIndex];
            bool isPreLoopTransition = entry.stateName == "BlockEnter" || entry.stateName == "Death";
            currentClipTargetDuration = entry.isLooping
                ? loopClipDisplayDuration
                : (entry.duration + (isPreLoopTransition ? 0f : postAttackHoldDuration));

            if (animator != null)
            {
                animator.CrossFadeInFixedTime(entry.stateName, crossFadeDuration, 0, 0f);
            }

            // Trigger environmental ocean displacement for impactful actions
            if (enableWaterShockwaves && WaterSystem.Instance != null)
            {
                Vector3 origin = transform.position;
                if (entry.stateName == "Slam")
                {
                    Vector3 slamPos = origin + transform.forward * 4.5f;
                    WaterSystem.Instance.AddFootstepWave(slamPos, 28f, 2.8f);
                    if (WaterSplashManager.Instance != null)
                    {
                        WaterSplashManager.Instance.SpawnFootstepSplash(slamPos, 2.5f);
                    }
                }
                else if (entry.stateName == "EMP")
                {
                    WaterSystem.Instance.AddFootstepWave(origin, 35f, 2.2f);
                }
                else if (entry.stateName == "Death")
                {
                    WaterSystem.Instance.AddFootstepWave(origin, 30f, 2.5f);
                    if (WaterSplashManager.Instance != null)
                    {
                        WaterSplashManager.Instance.SpawnBodySplash(origin, Vector3.up, 2.0f, 2.0f);
                    }
                }
            }

            Debug.Log($"[LeatherbackShowcase] Playing animation [{currentIndex + 1}/{clips.Count}] '{entry.displayName}' (Duration: {entry.duration:F2}s, IsLooping: {entry.isLooping})");
        }

        private void OnGUI()
        {
            if (!showOnScreenHUD) return;

            // Draw a stylish sci-fi tactical telemetry HUD box in top-left
            GUI.skin = null;
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = Texture2D.whiteTexture;

            Color origColor = GUI.color;
            GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);
            GUI.Box(new Rect(18, 18, 420, 155), GUIContent.none);
            GUI.color = origColor;

            // Draw outer neon border
            DrawBorder(new Rect(18, 18, 420, 155), new Color(0.2f, 0.75f, 1.0f, 0.9f), 2);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.82f, 0.2f, 1.0f); // Amber / Gold
            GUI.Label(new Rect(28, 24, 400, 24), "TARGET TELEMETRY: KAIJU LEATHERBACK", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            subStyle.normal.textColor = new Color(0.6f, 0.85f, 1.0f, 0.9f);

            var entry = clips[currentIndex];
            string statusStr = autoRotate
                ? $"<color=#00ff88>AUTO-ROTATING</color> (Next in {TimeRemaining:0.0}s)"
                : "<color=#ffaa00>PAUSED</color> (Press [P] to Resume)";

            GUI.Label(new Rect(28, 50, 400, 20), $"Current Anim [{currentIndex + 1}/{clips.Count}]: <color=#ffffff><b>{entry.displayName}</b></color>", subStyle);
            GUI.Label(new Rect(28, 72, 400, 20), $"State: {entry.stateName} | Clip Length: {entry.duration:F2}s | Mode: {(entry.isLooping ? "Loop" : "One-Shot")}", subStyle);
            GUI.Label(new Rect(28, 94, 400, 20), $"Cycle Status: {statusStr}", subStyle);

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic
            };
            hintStyle.normal.textColor = new Color(0.5f, 0.7f, 0.9f, 0.75f);
            GUI.Label(new Rect(28, 120, 400, 20), "Controls: [N] Next  |  [B] Prev  |  [P] Pause/Play  |  [R] Replay  |  [1-9] Direct", hintStyle);
            GUI.Label(new Rect(28, 140, 400, 20), "Model: Assets/Models/Leatherback V2.fbx  |  Textures: bodyout & nmout", hintStyle);
        }

        private void DrawBorder(Rect rect, Color color, int thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
