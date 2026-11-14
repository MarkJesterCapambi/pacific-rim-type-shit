using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Expanding procedural foam shockwave ring that erupts along the ocean surface.
    /// </summary>
    public class ProceduralSplash : MonoBehaviour
    {
        private float currentRadius = 0.5f;
        private float maxRadius = 6.0f;
        private float expansionSpeed = 8.0f;
        private float lifetime = 1.4f;
        private float currentLifetime = 0f;
        private LineRenderer ringRenderer;

        public static void CreateFoamRing(Vector3 position, float radius, float duration = 1.4f)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return;
            GameObject obj = new GameObject("ProceduralOceanFoamRing");
            obj.transform.position = position + Vector3.up * 0.05f;

            ProceduralSplash splash = obj.AddComponent<ProceduralSplash>();
            splash.maxRadius = radius;
            splash.lifetime = duration;
            splash.expansionSpeed = radius / Mathf.Max(0.1f, duration * 0.7f);
        }

        void Start()
        {
            ringRenderer = gameObject.AddComponent<LineRenderer>();
            ringRenderer.useWorldSpace = true;
            ringRenderer.loop = true;
            ringRenderer.positionCount = 36;
            ringRenderer.startWidth = 0.8f;
            ringRenderer.endWidth = 0.8f;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            if (s != null)
            {
                Material m = new Material(s);
                m.color = new Color(1f, 1f, 1f, 0.85f);
                ringRenderer.material = m;
            }
        }

        void Update()
        {
            currentLifetime += Time.deltaTime;
            currentRadius += expansionSpeed * Time.deltaTime;

            float alpha = Mathf.Clamp01(1f - (currentLifetime / lifetime));
            ringRenderer.startColor = new Color(1f, 1f, 1f, alpha * 0.8f);
            ringRenderer.endColor = new Color(0.7f, 0.9f, 1f, alpha * 0.4f);
            ringRenderer.startWidth = Mathf.Lerp(1.2f, 0.2f, currentLifetime / lifetime);
            ringRenderer.endWidth = ringRenderer.startWidth;

            float angleStep = 360f / 36;
            for (int i = 0; i < 36; i++)
            {
                float a = i * angleStep * Mathf.Deg2Rad;
                float px = transform.position.x + Mathf.Cos(a) * currentRadius;
                float pz = transform.position.z + Mathf.Sin(a) * currentRadius;
                float py = WaterSystem.Instance != null ? WaterSystem.Instance.GetWaterHeight(new Vector3(px, 0f, pz)) + 0.05f : transform.position.y;
                ringRenderer.SetPosition(i, new Vector3(px, py, pz));
            }

            if (currentLifetime >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
