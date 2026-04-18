using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class DoomscrollCycleManager : MonoBehaviour
    {
        [SerializeField] private Transform intermissionRoot;
        [SerializeField] private float intermissionDuration = 2f;
        [SerializeField] private float finishLevelDuration = 2f;
        [SerializeField] private int startingHealth = 3;
        [SerializeField] private string PathToGameControllers;

        private enum MinigameId
        {
            None,
            Crane,
            CloseTheAd,
            TimingHit
        }


        private GameController currentMinigame;
        private Coroutine transitionRoutine;
        private bool showIntermission;
        private int currentHealth;
        private string intermissionTitle = string.Empty;
        private string intermissionBody = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private GameController[] _gameControllers;
        private int _currentMinigameIndex = -1;

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
            var prefabs = Resources.LoadAll<GameController>(PathToGameControllers);
            _gameControllers = new GameController[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                _gameControllers[i] = Instantiate(prefabs[i]);
            }

            if (_gameControllers.Length == 0)
            {
                Debug.LogWarning($"{nameof(DoomscrollCycleManager)} did not find any minigames in Resources path '{PathToGameControllers}'.", this);
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
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(420f, Screen.width - 60f);
            Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - 128f) * 0.5f, panelWidth, 128f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 20f, panelRect.y + 18f, panelRect.width - 40f, 30f);
            Rect bodyRect = new Rect(panelRect.x + 20f, panelRect.y + 56f, panelRect.width - 40f, 48f);

            GUI.Label(titleRect, intermissionTitle, titleStyle);
            GUI.Label(bodyRect, intermissionBody, bodyStyle);
        }

        private void StartNextRound()
        {
            var nextMinigame = ChooseNextMinigame();
            currentMinigame = nextMinigame;
            SetIntermissionVisible(false);
            nextMinigame.gameObject.SetActive(true);

            nextMinigame.RoundFinished -= HandleRoundFinished;
            nextMinigame.RoundFinished += HandleRoundFinished;
            nextMinigame.BeginManagedRound();
        }

        private GameController ChooseNextMinigame()
        {
            _currentMinigameIndex = (_currentMinigameIndex + 1) % _gameControllers.Length;
            return _gameControllers[_currentMinigameIndex];
        }

        private void HandleRoundFinished(bool isSuccess)
        {
            if (transitionRoutine != null)
                return;

            transitionRoutine = StartCoroutine(AdvanceLoop(isSuccess));
        }

        private IEnumerator AdvanceLoop(bool isSuccess)
        {
            if (!isSuccess)
                currentHealth = Mathf.Max(0, currentHealth - 1);

            yield return new WaitForSeconds(finishLevelDuration);
            
            StopCurrentRound();
            ShowIntermission(isSuccess);
            yield return new WaitForSeconds(intermissionDuration);

            transitionRoutine = null;
            StartNextRound();
        }

        private void StopCurrentRound()
        {
            currentMinigame.RoundFinished -= HandleRoundFinished;
            currentMinigame.EndManagedRound();
            currentMinigame.gameObject.SetActive(false);
        }

        private void SetAllMinigamesActive(bool isActive)
        {
            foreach (var gameController in _gameControllers)
                gameController.gameObject.SetActive(isActive);
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
                intermissionRoot.gameObject.SetActive(isVisible);
        }

        private void UnsubscribeFromAll()
        {
            foreach (var gameController in _gameControllers)
                gameController.RoundFinished -= HandleRoundFinished;
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
                return;

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
