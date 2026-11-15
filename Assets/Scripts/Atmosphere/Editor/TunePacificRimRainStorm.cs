#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PacificRimAtmosphere;
using PacificRimWater;

namespace PacificRimAtmosphere.Editor
{
    public static class TunePacificRimRainStorm
    {
        [MenuItem("Pacific Rim/Tune Storm Rain and Clouds", false, 14)]
        public static void Tune()
        {
            // 1. Locate StormWeatherSystem & RainStreakParticles
            GameObject weatherSystem = GameObject.Find("StormWeatherSystem");
            if (weatherSystem == null)
            {
                Debug.LogError("[TuneRainStorm] StormWeatherSystem GameObject not found!");
                return;
            }

            Transform rainTrans = weatherSystem.transform.Find("RainStreakParticles");
            if (rainTrans == null)
            {
                Debug.LogError("[TuneRainStorm] RainStreakParticles child not found under StormWeatherSystem!");
                return;
            }

            var rainPs = rainTrans.GetComponent<ParticleSystem>();
            if (rainPs == null)
            {
                rainPs = rainTrans.gameObject.AddComponent<ParticleSystem>();
            }

            Undo.RecordObject(rainPs, "Tune Intense Rain Particles");

            var main = rainPs.main;
            main.maxParticles = 15000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(85f, 125f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.038f, 0.075f); // Thin, needle-like precipitation
            main.startColor = new Color(0.92f, 0.96f, 1.0f, 0.85f); // Crisp, bright visibility
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = rainPs.emission;
            emission.enabled = true;
            emission.rateOverTime = 12000f; // More intense torrential downpour

            var shape = rainPs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(160f, 160f, 16f);

            var vel = rainPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 32.0f;
            vel.y = -14.0f;
            vel.z = 22.0f;

            var noise = rainPs.noise;
            noise.enabled = true;
            noise.strength = 4.5f;
            noise.frequency = 0.85f;
            noise.scrollSpeed = 2.6f;

            var rend = rainTrans.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                Undo.RecordObject(rend, "Tune Rain Streak Renderer");
                rend.renderMode = ParticleSystemRenderMode.Stretch;
                rend.velocityScale = 0.095f;
                rend.lengthScale = 8.5f; // Slender, stretched needle streaks
                rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RainStreak.mat");
            }

            // 2. Tune Skybox Material for active cloud movement & boiling churn
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_StormSkybox.mat");
            if (skyMat != null)
            {
                Undo.RecordObject(skyMat, "Tune Cloud Movement");
                skyMat.SetFloat("_CloudSpeed", 3.2f);
                skyMat.SetFloat("_CloudChurnSpeed", 1.1f);
                skyMat.SetFloat("_ScudSpeedMultiplier", 3.4f);
                skyMat.SetFloat("_CloudCover", 0.65f);
                EditorUtility.SetDirty(skyMat);
            }

            // 3. Main Camera screen-space rain lens drenching
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var lensRain = mainCam.GetComponent<CameraLensRainController>();
                if (lensRain != null)
                {
                    Undo.RecordObject(lensRain, "Tune Lens Rain");
                    lensRain.lensRainIntensity = 1.35f;
                    lensRain.dropletDensity = 1.30f;
                    lensRain.downwardStreaksIntensity = 1.45f;
                    lensRain.distortionStrength = 0.0055f;
                    EditorUtility.SetDirty(lensRain);
                }
            }

            EditorUtility.SetDirty(rainTrans.gameObject);
            EditorUtility.SetDirty(weatherSystem);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00e5ff><b>[StormWeather]</b></color> Successfully tuned Pacific Rim storm! Clouds now actively drift across the sky with boiling billow churn and racing scud shreds. Rain particles intensified to 12,000/s with high-contrast needle glints (razor-thin, yet vividly visible).");
        }
    }
}
#endif
