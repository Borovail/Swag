using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class DoomscrollCycleManager : MonoBehaviour
    {
        [SerializeField] private Transform intermissionRoot;
        [SerializeField] private float intermissionDuration = 2f;
        [SerializeField] private int startingHealth = 3;

        private enum MinigameId
        {
            None,
            Crane,
            CloseTheAd,
            TimingHit
        }

        private const string CraneRootName = "Crane Minigame Demo";
        private const string CloseAdRootName = "Close The Ad Demo";
        private const string TimingRootName = "Timing Hit Demo";

        private GameObject craneRoot;
        private GameObject closeAdRoot;
        private GameObject timingRoot;
        private CraneMinigameController craneController;
        private CloseTheAdMinigameController closeAdController;
        private TimingHitMinigameController timingController;
        private MinigameId currentMinigame = MinigameId.None;
        private Coroutine transitionRoutine;
        private bool isConfigured;
        private bool showIntermission;
        private int currentHealth;
        private string intermissionTitle = string.Empty;
        private string intermissionBody = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private void Awake()
        {
            intermissionRoot = transform.Find("LoopUiRoot");
            intermissionRoot.gameObject.SetActive(false);

            ConfigureCamera();
        }

        private void ConfigureCamera()
        {
            Camera mainCamera = Camera.main;

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5.4f;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.backgroundColor = new Color(0.82f, 0.9f, 0.98f, 1f);
        }

        private void Start()
        {
            CacheSceneReferences();

            if (!isConfigured)
            {
                Debug.LogWarning($"{nameof(DoomscrollCycleManager)} is missing required scene references.", this);
                enabled = false;
                return;
            }

            currentHealth = Mathf.Max(1, startingHealth);
            SetAllMinigamesActive(false);
            SetIntermissionVisible(false);
            StartNextRound();
        }

        private void OnDestroy()
        {
            UnsubscribeFromAll();
        }

        private void OnGUI()
        {
            if (!showIntermission)
            {
                return;
            }

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(420f, Screen.width - 60f);
            Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - 128f) * 0.5f, panelWidth, 128f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 20f, panelRect.y + 18f, panelRect.width - 40f, 30f);
            Rect bodyRect = new Rect(panelRect.x + 20f, panelRect.y + 56f, panelRect.width - 40f, 48f);

            GUI.Label(titleRect, intermissionTitle, titleStyle);
            GUI.Label(bodyRect, intermissionBody, bodyStyle);
        }

        private void CacheSceneReferences()
        {
            craneRoot = FindSceneRoot(CraneRootName);
            closeAdRoot = FindSceneRoot(CloseAdRootName);
            timingRoot = FindSceneRoot(TimingRootName);
            isConfigured = intermissionRoot != null && craneRoot != null && closeAdRoot != null && timingRoot != null;
        }

        private GameObject FindSceneRoot(string rootName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject rootObject in activeScene.GetRootGameObjects())
            {
                if (rootObject.name == rootName)
                {
                    return rootObject;
                }
            }

            return null;
        }

        private void StartNextRound()
        {
            MinigameId nextMinigame = ChooseNextMinigame();
            currentMinigame = nextMinigame;
            SetIntermissionVisible(false);

            switch (nextMinigame)
            {
                case MinigameId.Crane:
                    craneRoot.SetActive(true);
                    craneController = craneRoot.GetComponent<CraneMinigameController>();
                    if (craneController == null)
                    {
                        Debug.LogWarning("Crane minigame controller was not created.", craneRoot);
                        return;
                    }

                    craneController.RoundFinished -= HandleRoundFinished;
                    craneController.RoundFinished += HandleRoundFinished;
                    craneController.BeginManagedRound();
                    break;
                case MinigameId.CloseTheAd:
                    closeAdRoot.SetActive(true);
                    closeAdController = closeAdRoot.GetComponent<CloseTheAdMinigameController>();
                    if (closeAdController == null)
                    {
                        Debug.LogWarning("Close The Ad controller was not created.", closeAdRoot);
                        return;
                    }

                    closeAdController.RoundFinished -= HandleRoundFinished;
                    closeAdController.RoundFinished += HandleRoundFinished;
                    closeAdController.BeginManagedRound();
                    break;
                case MinigameId.TimingHit:
                    timingRoot.SetActive(true);
                    timingController = timingRoot.GetComponent<TimingHitMinigameController>();
                    if (timingController == null)
                    {
                        Debug.LogWarning("Timing Hit controller was not created.", timingRoot);
                        return;
                    }

                    timingController.RoundFinished -= HandleRoundFinished;
                    timingController.RoundFinished += HandleRoundFinished;
                    timingController.BeginManagedRound();
                    break;
            }
        }

        private MinigameId ChooseNextMinigame()
        {
            MinigameId[] pool = { MinigameId.Crane, MinigameId.CloseTheAd, MinigameId.TimingHit };
            MinigameId next = pool[Random.Range(0, pool.Length)];

            if (pool.Length > 1 && next == currentMinigame)
            {
                next = pool[(System.Array.IndexOf(pool, next) + 1 + Random.Range(0, pool.Length - 1)) % pool.Length];
            }

            return next;
        }

        private void HandleRoundFinished(bool isSuccess)
        {
            if (transitionRoutine != null)
            {
                return;
            }

            transitionRoutine = StartCoroutine(AdvanceLoop(isSuccess));
        }

        private IEnumerator AdvanceLoop(bool isSuccess)
        {
            if (!isSuccess)
            {
                currentHealth = Mathf.Max(0, currentHealth - 1);
            }

            StopCurrentRound();
            ShowIntermission(isSuccess);
            yield return new WaitForSeconds(intermissionDuration);

            transitionRoutine = null;
            StartNextRound();
        }

        private void StopCurrentRound()
        {
            switch (currentMinigame)
            {
                case MinigameId.Crane:
                    if (craneController != null)
                    {
                        craneController.RoundFinished -= HandleRoundFinished;
                        craneController.EndManagedRound();
                    }

                    if (craneRoot != null)
                    {
                        craneRoot.SetActive(false);
                    }
                    break;
                case MinigameId.CloseTheAd:
                    if (closeAdController != null)
                    {
                        closeAdController.RoundFinished -= HandleRoundFinished;
                        closeAdController.EndManagedRound();
                    }

                    if (closeAdRoot != null)
                    {
                        closeAdRoot.SetActive(false);
                    }
                    break;
                case MinigameId.TimingHit:
                    if (timingController != null)
                    {
                        timingController.RoundFinished -= HandleRoundFinished;
                        timingController.EndManagedRound();
                    }

                    if (timingRoot != null)
                    {
                        timingRoot.SetActive(false);
                    }
                    break;
            }
        }

        private void SetAllMinigamesActive(bool isActive)
        {
            if (craneRoot != null)
            {
                craneRoot.SetActive(isActive);
            }

            if (closeAdRoot != null)
            {
                closeAdRoot.SetActive(isActive);
            }

            if (timingRoot != null)
            {
                timingRoot.SetActive(isActive);
            }
        }

        private void ShowIntermission(bool isSuccess)
        {
            intermissionTitle = isSuccess ? "Next Scroll" : "Ouch";
            intermissionBody = $"HP: {currentHealth}/{Mathf.Max(1, startingHealth)}\nNext mini-game is loading...";
            SetIntermissionVisible(true);
        }

        private void SetIntermissionVisible(bool isVisible)
        {
            showIntermission = isVisible;

            if (intermissionRoot != null)
            {
                intermissionRoot.gameObject.SetActive(isVisible);
            }
        }

        private void UnsubscribeFromAll()
        {
            if (craneController != null)
            {
                craneController.RoundFinished -= HandleRoundFinished;
            }

            if (closeAdController != null)
            {
                closeAdController.RoundFinished -= HandleRoundFinished;
            }

            if (timingController != null)
            {
                timingController.RoundFinished -= HandleRoundFinished;
            }
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.16f, 0.11f, 0.31f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = new Color(0.18f, 0.18f, 0.26f) }
            };
        }
    }
}
