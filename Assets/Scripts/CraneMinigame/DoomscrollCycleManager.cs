using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class DoomscrollCycleManager : MonoBehaviour
    {
        [SerializeField] private GameObject intermissionRoot;
        [SerializeField] private Text intermissionScoreText;
        [SerializeField] private Text intermissionTitleText;
        [SerializeField] private Transform HealthBar;

        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private Button playAgainButton;

        [SerializeField] private float intermissionDuration = 2f;
        [SerializeField] private float finishLevelDuration = 2f;
        [SerializeField] private string PathToGameControllers;

        private GameController currentMinigame;
        private Coroutine transitionRoutine;
        private int currentHealth = 3;
        private int clearedMinigamesCount;

        private GameController[] _gameControllers;
        private int _currentMinigameIndex = -1;

        private void Awake()
        {
            intermissionRoot.SetActive(false);
            gameOverRoot.SetActive(false);

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

            playAgainButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            ResetCycleState();
            SetAllMinigamesActive(false);
            StartNextRound();
        }

        private void OnDestroy()
        {
            UnsubscribeFromAll();
            playAgainButton.onClick.RemoveAllListeners();
        }

        private void StartNextRound()
        {
            var nextMinigame = ChooseNextMinigame();
            currentMinigame = nextMinigame;
            intermissionRoot.SetActive(false);

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

            if (isSuccess)
                clearedMinigamesCount++;
            else
            {
                currentHealth = Mathf.Max(0, currentHealth - 1);
                Destroy(HealthBar.GetChild(0).gameObject);
            }

            transitionRoutine = StartCoroutine(AdvanceLoop(isSuccess));
        }

        private IEnumerator AdvanceLoop(bool isSuccess)
        {
            yield return new WaitForSeconds(finishLevelDuration);

            StopCurrentRound();

            if (currentHealth == 0)
                LoseGame();
            else
            {
                ShowIntermission(isSuccess);
                yield return new WaitForSeconds(intermissionDuration);

                transitionRoutine = null;
                StartNextRound();
            }
        }

        private void LoseGame()
        {
            intermissionRoot.SetActive(false);

            gameOverScoreText.text = $"Score: {clearedMinigamesCount}";
            gameOverRoot.SetActive(true);

            transitionRoutine = null;
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
            intermissionScoreText.text = $"Score: {clearedMinigamesCount}";
            intermissionTitleText.text = isSuccess ? "Next Scroll" : "Ouch";

            intermissionRoot.SetActive(true);
        }

        private void UnsubscribeFromAll()
        {
            if (_gameControllers == null)
                return;

            foreach (var gameController in _gameControllers)
                gameController.RoundFinished -= HandleRoundFinished;
        }

        private void ResetCycleState()
        {
            currentHealth = 3;
            clearedMinigamesCount = 0;
            _currentMinigameIndex = -1;
            currentMinigame = null;
            transitionRoutine = null;
            intermissionRoot.SetActive(false);
            gameOverRoot.SetActive(false);
        }

    }
}
