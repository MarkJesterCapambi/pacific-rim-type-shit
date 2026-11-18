using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimMeme
{
    /// <summary>
    /// Coordinates meme cheat commands ("freddy" and "america") across gameplay scenes.
    /// </summary>
    public class MemeCommandController : MonoBehaviour
    {
        private static MemeCommandController instance;
        public static MemeCommandController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<MemeCommandController>();
                    if (instance == null)
                    {
                        var go = new GameObject("PacificRim_MemeCommands");
                        instance = go.AddComponent<MemeCommandController>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public static bool IsFreddyPlaying => instance != null && instance.isFreddyPlaying;
        public static bool IsAmericaActive => AmericaAirstrikeCutscene.IsRunning;
        public static bool ShouldSuppressCrosshair => IsFreddyPlaying || IsAmericaActive;

        private MemePackCatalog catalog;

        // Freddy Jumpscare overlay state
        private Canvas freddyCanvas;
        private GameObject freddyOverlayRoot;
        private RawImage freddyRawImage;
        private AspectRatioFitter freddyAspectFitter;
        private VideoPlayer freddyVideoPlayer;
        private AudioSource freddyAudioSource;
        private RenderTexture freddyRenderTexture;
        private bool isFreddyPlaying;
        private Coroutine freddyRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmupOnLoad()
        {
            // Ensure controller exists early so FreddyJumpscare.mp4 is pre-prepared with zero trigger lag
            var ctrl = Instance;
            ctrl.EnsureCatalogLoaded();
            ctrl.EnsureFreddyOverlayBuilt();
            ctrl.PrewarmFreddyVideo();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureCatalogLoaded();
            EnsureFreddyOverlayBuilt();
            PrewarmFreddyVideo();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
            {
                instance = null;
            }

            if (freddyRenderTexture != null)
            {
                freddyRenderTexture.Release();
                Destroy(freddyRenderTexture);
                freddyRenderTexture = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (isFreddyPlaying)
            {
                StopFreddyImmediate();
            }
        }

        public static void TriggerFreddyJumpscare()
        {
            Instance.PlayFreddyJumpscareInternal();
        }

        public static void TriggerAmericaAirstrike()
        {
            Instance.LaunchAmericaAirstrikeInternal();
        }

        private void EnsureCatalogLoaded()
        {
            if (catalog == null)
            {
                catalog = Resources.Load<MemePackCatalog>("MemePackCatalog");
            }
        }

        private VideoClip ResolveFreddyClip()
        {
            EnsureCatalogLoaded();
            if (catalog != null && catalog.freddyJumpscareClip != null)
            {
                return catalog.freddyJumpscareClip;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/MemePack/FreddyJumpscare.mp4");
#else
            return null;
#endif
        }

        public GameObject ResolveF22Prefab()
        {
            EnsureCatalogLoaded();
            if (catalog != null && catalog.f22RaptorPrefab != null)
            {
                return catalog.f22RaptorPrefab;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MemePack/F22_Raptor_Meme.prefab");
#else
            return null;
#endif
        }

        private void EnsureFreddyOverlayBuilt()
        {
            if (freddyCanvas != null && freddyVideoPlayer != null) return;

            var canvasGo = new GameObject("FreddyJumpscare_OverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            freddyCanvas = canvasGo.AddComponent<Canvas>();
            freddyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            freddyCanvas.sortingOrder = 32000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            freddyOverlayRoot = new GameObject("FreddyOverlayRoot", typeof(RectTransform), typeof(Image));
            freddyOverlayRoot.transform.SetParent(canvasGo.transform, false);
            var rootRt = freddyOverlayRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var backdrop = freddyOverlayRoot.GetComponent<Image>();
            backdrop.color = Color.black;
            backdrop.raycastTarget = false;

            var videoGo = new GameObject("FreddyVideoRawImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            videoGo.transform.SetParent(freddyOverlayRoot.transform, false);
            var videoRt = videoGo.GetComponent<RectTransform>();
            videoRt.anchorMin = Vector2.zero;
            videoRt.anchorMax = Vector2.one;
            videoRt.offsetMin = Vector2.zero;
            videoRt.offsetMax = Vector2.zero;

            freddyRawImage = videoGo.GetComponent<RawImage>();
            freddyRawImage.color = Color.white;
            freddyRawImage.raycastTarget = false;

            freddyAspectFitter = videoGo.GetComponent<AspectRatioFitter>();
            freddyAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            freddyAspectFitter.aspectRatio = 16f / 9f;

            freddyRenderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "FreddyJumpscare_RT"
            };
            freddyRenderTexture.Create();
            ClearRenderTexture(freddyRenderTexture);
            freddyRawImage.texture = freddyRenderTexture;

            freddyAudioSource = canvasGo.AddComponent<AudioSource>();
            freddyAudioSource.playOnAwake = false;
            freddyAudioSource.spatialBlend = 0f;
            freddyAudioSource.bypassListenerEffects = true;
            freddyAudioSource.ignoreListenerPause = true;
            freddyAudioSource.volume = 1f;

            freddyVideoPlayer = canvasGo.AddComponent<VideoPlayer>();
            freddyVideoPlayer.playOnAwake = false;
            freddyVideoPlayer.isLooping = false;
            freddyVideoPlayer.waitForFirstFrame = true;
            freddyVideoPlayer.skipOnDrop = false;
            freddyVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            freddyVideoPlayer.targetTexture = freddyRenderTexture;
            freddyVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            freddyVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            freddyVideoPlayer.EnableAudioTrack(0, true);
            freddyVideoPlayer.SetTargetAudioSource(0, freddyAudioSource);

            freddyOverlayRoot.SetActive(false);
        }

        private void PrewarmFreddyVideo()
        {
            VideoClip clip = ResolveFreddyClip();
            if (clip == null || freddyVideoPlayer == null) return;

            if (freddyVideoPlayer.clip != clip)
            {
                freddyVideoPlayer.clip = clip;
            }

            if (!freddyVideoPlayer.isPrepared && !freddyVideoPlayer.isPlaying)
            {
                freddyVideoPlayer.Prepare();
            }
        }

        private void PlayFreddyJumpscareInternal()
        {
            if (isFreddyPlaying) return;

            VideoClip clip = ResolveFreddyClip();
            if (clip == null)
            {
                Debug.LogWarning("[MemePack] Could not locate FreddyJumpscare.mp4 VideoClip.");
                return;
            }

            EnsureFreddyOverlayBuilt();

            if (freddyAspectFitter != null && clip.height > 0)
            {
                freddyAspectFitter.aspectRatio = (float)clip.width / clip.height;
            }

            if (freddyRoutine != null)
            {
                StopCoroutine(freddyRoutine);
            }
            freddyRoutine = StartCoroutine(FreddyPlaybackRoutine(clip));
        }

        private IEnumerator FreddyPlaybackRoutine(VideoClip clip)
        {
            isFreddyPlaying = true;
            bool finished = false;
            VideoPlayer.EventHandler onFinished = _ => finished = true;
            freddyVideoPlayer.loopPointReached += onFinished;

            try
            {
                if (freddyVideoPlayer.clip != clip)
                {
                    freddyVideoPlayer.clip = clip;
                }

                ClearRenderTexture(freddyRenderTexture);

                if (!freddyVideoPlayer.isPrepared)
                {
                    freddyVideoPlayer.Prepare();
                    float prepTimeout = 3f;
                    while (!freddyVideoPlayer.isPrepared && prepTimeout > 0f)
                    {
                        prepTimeout -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                freddyVideoPlayer.time = 0.0;
                freddyOverlayRoot.SetActive(true);
                freddyRawImage.enabled = false;
                freddyVideoPlayer.Play();

                // Wait until the first decoded video frame paints to avoid any black/stale flash
                float frameWaitTimeout = 0.75f;
                while (!finished && freddyVideoPlayer.frame < 0 && frameWaitTimeout > 0f)
                {
                    frameWaitTimeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                freddyRawImage.enabled = true;

                double maxDuration = clip.length > 0.1 ? clip.length + 0.6 : 5.6;
                float elapsed = 0f;
                while (!finished && elapsed < maxDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                freddyVideoPlayer.loopPointReached -= onFinished;
                StopFreddyImmediate();
                PrewarmFreddyVideo();
                freddyRoutine = null;
            }
        }

        private void StopFreddyImmediate()
        {
            isFreddyPlaying = false;
            if (freddyVideoPlayer != null && freddyVideoPlayer.isPlaying)
            {
                freddyVideoPlayer.Stop();
            }
            if (freddyAudioSource != null && freddyAudioSource.isPlaying)
            {
                freddyAudioSource.Stop();
            }
            if (freddyRenderTexture != null)
            {
                ClearRenderTexture(freddyRenderTexture);
            }
            if (freddyOverlayRoot != null)
            {
                freddyOverlayRoot.SetActive(false);
            }
        }

        private static void ClearRenderTexture(RenderTexture rt)
        {
            if (rt == null) return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        private void LaunchAmericaAirstrikeInternal()
        {
            if (isFreddyPlaying || AmericaAirstrikeCutscene.IsRunning) return;
            if (MainMenuUI.IsMenuOpen) return;

            if ((PacificRimCutscene.PacificRimDeploymentCutscene.Instance != null && PacificRimCutscene.PacificRimDeploymentCutscene.Instance.isCutsceneRunning) ||
                (PacificRimCutscene.OperationPitfallIntroCutscene.Instance != null && PacificRimCutscene.OperationPitfallIntroCutscene.Instance.isCutsceneRunning) ||
                (PacificRimCutscene.OperationPitfallEndingCutscene.Instance != null && PacificRimCutscene.OperationPitfallEndingCutscene.Instance.isCutsceneRunning) ||
                (PacificRimCutscene.AnteverseEndingCutscene.Instance != null && PacificRimCutscene.AnteverseEndingCutscene.Instance.isCutsceneRunning))
            {
                JaegerHUD.Instance?.ShowBanner("AIRSTRIKE STANDBY // WAIT FOR CUTSCENE COMPLETION", 2.0f);
                return;
            }

            KaijuAI kaiju = KaijuAI.Instance != null ? KaijuAI.Instance : FindAnyObjectByType<KaijuAI>();
            if (kaiju == null || kaiju.IsDead)
            {
                JaegerHUD.Instance?.ShowBanner("AIRSTRIKE ABORTED // NO LIVE KAIJU TARGET", 2.0f);
                return;
            }

            if (PacificRimAdminUI.IsOpen)
            {
                PacificRimAdminUI.Close();
            }

            var go = new GameObject("AmericaAirstrike_CutsceneDirector");
            var director = go.AddComponent<AmericaAirstrikeCutscene>();
            director.BeginAirstrike(kaiju, ResolveF22Prefab());
        }
    }
}
