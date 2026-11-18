using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PacificRimCutscene
{
    /// <summary>
    /// Tracks cross-scene transition state for the Level 1 -> Level 3 Harbor-to-Hong Kong Neon
    /// Phase 3 throw cutscene and half-HP boss handoff.
    /// </summary>
    public static class HarborToNeonTransitionState
    {
        public const string HarborSceneName = "Save Striker Eureka";
        public const string NeonSceneName = "Hong Kong Neon Highway";

        public static bool PendingPart2Cutscene { get; set; }
        public static bool PendingSkipPart2ToCombat { get; set; }
        public static bool StartNeonCityAtPhase3HalfHp { get; set; }
        public static bool IsPart1Running { get; set; }
        public static bool IsPart2Running { get; set; }

        /// <summary>
        /// Elapsed seconds carried across the 0:13 hard cut into Hong Kong Neon Highway so music,
        /// timing logs, and overall 30s pacing stay continuous.
        /// </summary>
        public static float ElapsedAtHandoff { get; set; }

        public static bool IsAnyCutsceneRunning => IsPart1Running || IsPart2Running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomainState()
        {
            ClearAll();
        }

        public static void ClearAll()
        {
            PendingPart2Cutscene = false;
            PendingSkipPart2ToCombat = false;
            StartNeonCityAtPhase3HalfHp = false;
            IsPart1Running = false;
            IsPart2Running = false;
            ElapsedAtHandoff = 0f;
        }

        public static bool IsHarborScene(Scene scene)
        {
            return scene.IsValid() && string.Equals(scene.name, HarborSceneName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNeonCityScene(Scene scene)
        {
            return scene.IsValid() && string.Equals(scene.name, NeonSceneName, StringComparison.OrdinalIgnoreCase);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureNeonCityControllerOnLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttachNeonController(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAttachNeonController(scene);
        }

        private static void TryAttachNeonController(Scene scene)
        {
            if (!IsNeonCityScene(scene)) return;

            bool needsController = PendingPart2Cutscene || PendingSkipPart2ToCombat || StartNeonCityAtPhase3HalfHp;
            if (!needsController) return;

            if (UnityEngine.Object.FindAnyObjectByType<NeonCityThrowCutscene>() == null)
            {
                var go = new GameObject("HarborToNeon_Phase2CutsceneRuntime");
                go.AddComponent<NeonCityThrowCutscene>();
            }
        }
    }
}
