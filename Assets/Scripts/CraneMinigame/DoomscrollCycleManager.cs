using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CraneMinigame
{
    [Serializable]
    public sealed class ControlSchemeIcon
    {
        public GameController.ControlScheme scheme;
        public Sprite icon;
    }

    [DisallowMultipleComponent]
    public sealed class DoomscrollCycleManager : MonoBehaviour
    {
        [SerializeField] private GameObject intermissionRoot;
        [SerializeField] private Image _intermissionVisual;
        [SerializeField] private Sprite _intermissionVisualWin;
        [SerializeField] private Sprite _intermissionVisualLose;
        [SerializeField] private Text intermissionScoreText;
        [SerializeField] private Text intermissionTitleText;
        [SerializeField] private Transform HealthBar;

        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private Button playAgainButton;

        [SerializeField] private AudioClip _winSound;
        [SerializeField] private AudioClip _loseSound;
        [SerializeField] private float _winSoundVolume = 0.9f;
        [SerializeField] private float _loseSoundVolume = 0.6f;
        [SerializeField] private AudioSource _intermissionAudioSource;

        [Header("Next Game Preview")]
        [SerializeField] private Image _controlSchemeImage;
        [SerializeField] private Text _controlDescriptionText;
        [SerializeField] private List<ControlSchemeIcon> _controlSchemeIcons = new();

        [SerializeField] private float intermissionDuration = 2f;
        [SerializeField] private float finishLevelDuration = 2f;
        [SerializeField] private string PathToGameControllers;

        [Header("Difficulty Scaling")]
        [SerializeField] private int roundsPerDifficultyStep = 3;

        private GameController currentMinigame;
        private Coroutine transitionRoutine;
        private int currentHealth = 3;
        private int clearedMinigamesCount;
        private int totalRoundsPlayed;

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
            StartCoroutine(StartWindow());
        }

        private IEnumerator StartWindow()
        {
            ShowIntermission(true, false);
            yield return new WaitForSeconds(intermissionDuration);
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

            int step = Mathf.Min(totalRoundsPlayed / Mathf.Max(1, roundsPerDifficultyStep), 3);
            nextMinigame.ApplyDifficulty((GameController.Difficulty)step);
            Debug.Log($"Starting round {totalRoundsPlayed + 1} with difficulty {(GameController.Difficulty)step}.");
            nextMinigame.RoundFinished -= HandleRoundFinished;
            nextMinigame.RoundFinished += HandleRoundFinished;
            nextMinigame.BeginManagedRound();

            totalRoundsPlayed++;
        }

        private GameController ChooseNextMinigame()
        {
            _currentMinigameIndex = (_currentMinigameIndex + 1) % _gameControllers.Length;
            return _gameControllers[_currentMinigameIndex];
        }

        private GameController PeekNextMinigame()
        {
            int nextIndex = (_currentMinigameIndex + 1) % _gameControllers.Length;
            return _gameControllers[nextIndex];
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

        private void ShowIntermission(bool isSuccess, bool showTitle = true)
        {
            intermissionScoreText.text = $"Score: {clearedMinigamesCount}";
            intermissionTitleText.gameObject.SetActive(showTitle);
            intermissionTitleText.text = isSuccess ? "Nice" : "Ouch";

            _intermissionVisual.sprite = isSuccess ? _intermissionVisualWin : _intermissionVisualLose;

            _intermissionAudioSource.clip = isSuccess ? _winSound : _loseSound;
            _intermissionAudioSource.volume = isSuccess ? _winSoundVolume : _loseSoundVolume;

            GameController next = PeekNextMinigame();

            if (_controlSchemeImage != null)
            {
                ControlSchemeIcon entry = _controlSchemeIcons.Find(e => e.scheme == next.RequiredControls);
                _controlSchemeImage.sprite = entry?.icon;
            }

            if (_controlDescriptionText != null)
                _controlDescriptionText.text = next.ControlDescription;

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
            totalRoundsPlayed = 0;
            _currentMinigameIndex = -1;
            currentMinigame = null;
            transitionRoutine = null;
            intermissionRoot.SetActive(false);
            gameOverRoot.SetActive(false);
        }

    }
}
