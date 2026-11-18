using System.Collections.Generic;
using UnityEngine;
using PacificRimAudio;
using PacificRimWater;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Attached to 100% static, indestructible urban skyscrapers, mid-rises, and concrete retaining walls.
    /// Buildings never collapse or deform; instead, strikes from Jaegers, Kaijus, or Plasmacaster bolts
    /// emit bright impact sparks, concrete dust puffs, and pooled scorch/impact decals flush on the facade.
    /// </summary>
    public class UrbanBuildingImpactReceiver : MonoBehaviour
    {
        private const int MaxPooledDecals = 36;
        private static readonly Queue<GameObject> DecalPool = new Queue<GameObject>(MaxPooledDecals);
        private static Material sharedScorchMat;
        private static Texture2D sharedScorchTex;
        private static Mesh sharedQuadMesh;

        private float nextImpactTime;

        public void RegisterImpact(Vector3 hitPoint, Vector3 surfaceNormal, bool isHeavy = false)
        {
            if (Time.time < nextImpactTime) return;
            nextImpactTime = Time.time + 0.14f;

            Vector3 n = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnCombatSparks(hitPoint + n * 0.25f, n, isBlocked: false, isHeavy: isHeavy, isKaijuTarget: false);
                WaterSplashManager.Instance.EmitVolumetricMist(
                    hitPoint + n * 0.8f,
                    n * Random.Range(2.5f, 6.0f) + Vector3.up * Random.Range(1.0f, 3.0f),
                    isHeavy ? 8.5f : 5.2f,
                    isHeavy ? 0.28f : 0.18f,
                    2.6f);
            }

            PacificRimAudioManager.Instance?.PlayDebrisImpact(hitPoint, isHeavy ? 0.95f : 0.65f);
            SpawnScorchDecal(hitPoint, n, isHeavy);
        }

        private static void SpawnScorchDecal(Vector3 worldPos, Vector3 normal, bool isHeavy)
        {
            EnsureDecalAssets();
            if (sharedScorchMat == null || sharedQuadMesh == null) return;

            GameObject decal;
            if (DecalPool.Count >= MaxPooledDecals)
            {
                decal = DecalPool.Dequeue();
                if (decal == null) decal = CreateDecalGO();
            }
            else
            {
                decal = CreateDecalGO();
            }

            DecalPool.Enqueue(decal);

            // Place slightly proud of the facade normal so Z-fighting never occurs
            Vector3 upHint = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion look = Quaternion.LookRotation(-normal, upHint) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = isHeavy ? Random.Range(5.2f, 7.8f) : Random.Range(3.0f, 4.8f);

            decal.transform.position = worldPos + normal * 0.09f;
            decal.transform.rotation = look;
            decal.transform.localScale = new Vector3(size, size, 1f);
            decal.SetActive(true);
        }

        private static GameObject CreateDecalGO()
        {
            GameObject go = new GameObject("UrbanImpactScorchDecal");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sharedQuadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sharedScorchMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static void EnsureDecalAssets()
        {
            if (sharedQuadMesh == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sharedQuadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tmp);
            }

            if (sharedScorchTex == null)
            {
                const int sz = 128;
                sharedScorchTex = new Texture2D(sz, sz, TextureFormat.RGBA32, true)
                {
                    name = "UrbanScorchDecalTex",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var px = new Color32[sz * sz];
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float u = (x + 0.5f) / sz * 2f - 1f;
                        float v = (y + 0.5f) / sz * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Atan2(v, u);
                        float crack = Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 7f + r * 6f)), 6f) * Mathf.Clamp01(1f - r);
                        float n = Mathf.PerlinNoise(u * 3.5f + 9.2f, v * 3.5f + 4.7f);
                        float soot = Mathf.Clamp01(1f - r * (0.85f + n * 0.35f));
                        float alpha = Mathf.Clamp01(soot * 0.85f + crack * 0.55f);
                        byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(18f, 44f, crack));
                        px[y * sz + x] = new Color32(shade, shade, (byte)(shade + 4), (byte)Mathf.RoundToInt(alpha * 225f));
                    }
                }
                sharedScorchTex.SetPixels32(px);
                sharedScorchTex.Apply(true, true);
            }

            if (sharedScorchMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                sharedScorchMat = new Material(shader)
                {
                    name = "UrbanScorchDecalMat",
                    hideFlags = HideFlags.HideAndDontSave
                };
                sharedScorchMat.mainTexture = sharedScorchTex;
                if (sharedScorchMat.HasProperty("_BaseMap")) sharedScorchMat.SetTexture("_BaseMap", sharedScorchTex);
                if (sharedScorchMat.HasProperty("_BaseColor")) sharedScorchMat.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.16f, 0.92f));
                if (sharedScorchMat.HasProperty("_Surface")) sharedScorchMat.SetFloat("_Surface", 1f);
                sharedScorchMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sharedScorchMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sharedScorchMat.SetInt("_ZWrite", 0);
                sharedScorchMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedScorchMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            }
        }
    }
}
