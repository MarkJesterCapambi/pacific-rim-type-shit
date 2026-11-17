using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PacificRimCutscene
{
    /// <summary>
    /// Persistent full-screen black transition overlay used to carry a seamless fade-to-black
    /// from the Operation Pitfall intro cutscene into the Slattern boss encounter scene,
    /// then smoothly fade back in once the target scene is ready.
    /// </summary>
    public class CutsceneBlackTransitionOverlay : MonoBehaviour
    {
        public static CutsceneBlackTransitionOverlay Instance { get; private set; }
        public static bool HasPendingFadeIn => Instance != null && Instance.pendingFadeIn;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Image blackImage;
        private bool pendingFadeIn;
        private bool isTransitioning;

        public static CutsceneBlackTransitionOverlay GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PacificRim_CutsceneBlackTransition");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CutsceneBlackTransitionOverlay>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildUI();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void BuildUI()
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10050;

            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var imgGo = new GameObject("BlackScreen", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            blackImage = imgGo.GetComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;

            canvas.enabled = false;
        }

        public void SetBlackAlpha(float alpha)
        {
            if (canvas == null) BuildUI();
            alpha = Mathf.Clamp01(alpha);
            canvas.enabled = alpha > 0.001f;
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = alpha > 0.01f;
        }

        public void TransitionToScene(string targetSceneName, float preFadeOutDuration = 0f, float postFadeInDuration = 0.9f)
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetSceneName, preFadeOutDuration, postFadeInDuration));
        }

        private IEnumerator TransitionRoutine(string targetSceneName, float preFadeOutDuration, float postFadeInDuration)
        {
            isTransitioning = true;
            if (canvas == null) BuildUI();

            float startAlpha = canvasGroup.alpha;
            if (preFadeOutDuration > 0.01f && startAlpha < 0.999f)
            {
                float elapsed = 0f;
                while (elapsed < preFadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / preFadeOutDuration));
                    SetBlackAlpha(Mathf.Lerp(startAlpha, 1f, t));
                    yield return null;
                }
            }
            SetBlackAlpha(1f);
            pendingFadeIn = true;

            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(targetSceneName);
            while (asyncOp != null && !asyncOp.isDone)
            {
                yield return null;
            }

            // Wait up to a few frames for the destination scene controller to claim FadeIn, or fade in automatically.
            float safetyWait = 0f;
            while (pendingFadeIn && safetyWait < 1.0f)
            {
                safetyWait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (pendingFadeIn)
            {
                yield return StartCoroutine(FadeInRoutine(postFadeInDuration));
            }
            isTransitioning = false;
        }

        public void BeginFadeInAndDestroy(float duration = 0.9f)
        {
            pendingFadeIn = false;
            StartCoroutine(FadeInAndDestroyRoutine(duration));
        }

        private IEnumerator FadeInRoutine(float duration)
        {
            pendingFadeIn = false;
            float elapsed = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                SetBlackAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }
            SetBlackAlpha(0f);
        }

        private IEnumerator FadeInAndDestroyRoutine(float duration)
        {
            yield return StartCoroutine(FadeInRoutine(duration));
            if (Instance == this) Instance = null;
            Destroy(gameObject);
        }
    }
}
