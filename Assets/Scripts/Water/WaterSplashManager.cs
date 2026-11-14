using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Spawns procedural water eruptions, spray cascades, and expanding foam rings
    /// when giant feet strike the ocean surface in Pacific Rim style.
    /// </summary>
    public class WaterSplashManager : MonoBehaviour
    {
        public static WaterSplashManager Instance { get; private set; }

        [Header("Procedural Splash Settings")]
        [Tooltip("Number of splash particles per footstep impact.")]
        public int splashParticleCount = 80;
        [Tooltip("Vertical launch velocity of water spray.")]
        public float splashUpwardForce = 14f;
        [Tooltip("Horizontal spread radius of water spray.")]
        public float splashSpread = 4.5f;

        private ParticleSystem proceduralParticleSystem;

        void Awake()
        {
            if (Instance == null)
                Instance = this;

            EnsureParticleSystem();
        }

        private void EnsureParticleSystem()
        {
            if (proceduralParticleSystem == null)
            {
                proceduralParticleSystem = GetComponentInChildren<ParticleSystem>();
                if (proceduralParticleSystem == null)
                {
                    GameObject pObj = new GameObject("ProceduralOceanSplashParticles");
                    pObj.transform.SetParent(transform);
                    proceduralParticleSystem = pObj.AddComponent<ParticleSystem>();

                    var main = proceduralParticleSystem.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    main.startLifetime = 1.6f;
                    main.startSpeed = 8f;
                    main.startSize = 1.2f;
                    main.startColor = new Color(0.85f, 0.95f, 1.0f, 0.85f);
                    main.gravityModifier = 1.8f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var emission = proceduralParticleSystem.emission;
                    emission.rateOverTime = 0;

                    var shape = proceduralParticleSystem.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25f;
                    shape.radius = 2.0f;

                    var colOverLifetime = proceduralParticleSystem.colorOverLifetime;
                    colOverLifetime.enabled = true;
                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.85f, 1f), 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                    );
                    colOverLifetime.color = grad;

                    var sizeOverLifetime = proceduralParticleSystem.sizeOverLifetime;
                    sizeOverLifetime.enabled = true;
                    AnimationCurve curve = new AnimationCurve();
                    curve.AddKey(0f, 0.6f);
                    curve.AddKey(0.3f, 1.5f);
                    curve.AddKey(1f, 0.2f);
                    sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

                    var renderer = proceduralParticleSystem.GetComponent<ParticleSystemRenderer>();
                    Shader diffuse = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    if (diffuse != null)
                    {
                        Material splashMat = new Material(diffuse);
                        splashMat.color = new Color(0.9f, 0.96f, 1f, 0.8f);
                        renderer.material = splashMat;
                    }
                }
            }
        }

        /// <summary>
        /// Emits a massive water splash explosion at the specified contact point.
        /// </summary>
        public void SpawnFootstepSplash(Vector3 position, float scale = 1.0f)
        {
            EnsureParticleSystem();
            if (proceduralParticleSystem != null)
            {
                proceduralParticleSystem.transform.position = position;

                var emitParams = new ParticleSystem.EmitParams();
                int count = Mathf.RoundToInt(splashParticleCount * scale);

                for (int i = 0; i < count; i++)
                {
                    Vector2 randCircle = Random.insideUnitCircle * (splashSpread * scale);
                    Vector3 offset = new Vector3(randCircle.x, Random.Range(0f, 0.8f), randCircle.y);
                    emitParams.position = position + offset;

                    float upVel = Random.Range(splashUpwardForce * 0.6f, splashUpwardForce * 1.2f) * scale;
                    Vector3 horizVel = new Vector3(randCircle.x, 0f, randCircle.y).normalized * Random.Range(3f, 7f) * scale;
                    emitParams.velocity = horizVel + Vector3.up * upVel;

                    emitParams.startSize = Random.Range(0.8f, 2.2f) * scale;
                    emitParams.startLifetime = Random.Range(1.0f, 1.8f);
                    proceduralParticleSystem.Emit(emitParams, 1);
                }
            }
        }
    }
}
