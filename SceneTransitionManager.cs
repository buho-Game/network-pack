using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace com.buho.NetworkPack.Scene
{
    /// <summary>
    /// Manages scene transitions in networked Unity applications with fade effects.
    /// Provides both network-synchronized and local scene transitions.
    /// </summary>
    public class SceneTransitionManager : NetworkBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.5f;

        [Header("Fade")]
        [SerializeField] private GameObject _fadePanel;
        [SerializeField] private Image _panelImage;

        /// <summary>
        /// The duration of fade transitions in seconds.
        /// </summary>
        public float FadeDuration => _fadeDuration;

        /// <summary>
        /// Triggered when a scene loading operation starts.
        /// </summary>
        public event Action OnLoadingStarted;

        /// <summary>
        /// Triggered when scene loading is completed.
        /// </summary>
        public event Action OnLoadingCompleted;

        /// <summary>
        /// Triggered when all network clients have completed loading.
        /// </summary>
        public event Action OnAllClientsReady;

        private bool _isTransitioning = false;
        private HashSet<ulong> _readyClients = new HashSet<ulong>();
        private Coroutine _currentTransition;

        /// <summary>
        /// The NetworkObject associated with this manager.
        /// </summary>
        public NetworkObject NetworkObj => NetworkObject;

        /// <summary>
        /// Returns whether a transition is currently in progress.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeCanvasGroup();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeCanvasGroup()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>
        /// Load a networked scene with fade transition. All clients will be synchronized.
        /// </summary>
        /// <param name="sceneName">Name of the scene to load</param>
        public void LoadNetworkedScene(string sceneName)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            _readyClients.Clear();
            OnLoadingStarted?.Invoke();
            StartTransitionClientRpc();

            // Wait for fade in to complete before loading scene
            StartCoroutine(DelayedAction(_fadeDuration, () =>
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }));
        }

        /// <summary>
        /// Load a networked scene with fade transition using LoadSceneMode parameter.
        /// </summary>
        /// <param name="sceneName">Name of the scene to load</param>
        /// <param name="loadSceneMode">Scene loading mode (Single or Additive)</param>
        public void LoadNetworkedScene(string sceneName, LoadSceneMode loadSceneMode)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            _readyClients.Clear();
            OnLoadingStarted?.Invoke();
            StartTransitionClientRpc();

            // Wait for fade in to complete before loading scene
            StartCoroutine(DelayedAction(_fadeDuration, () =>
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, loadSceneMode);
            }));
        }

        [ClientRpc]
        private void StartTransitionClientRpc()
        {
            _canvasGroup.gameObject.SetActive(true);
            StartCoroutine(FadeCanvasGroup(_canvasGroup, 0f, 1f, _fadeDuration, () =>
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }));
        }

        private void HandleLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;

            if (clientsTimedOut.Count > 0)
            {
                Debug.LogWarning($"Some clients timed out while loading: {string.Join(", ", clientsTimedOut)}");
            }

            OnLoadingCompleted?.Invoke();

            // Notify server that this client is ready
            if (!IsServer)
            {
                NotifyClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                AddReadyClient(NetworkManager.Singleton.LocalClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void NotifyClientReadyServerRpc(ulong clientId)
        {
            AddReadyClient(clientId);
        }

        private void AddReadyClient(ulong clientId)
        {
            if (!IsServer) return;

            _readyClients.Add(clientId);

            // Check if all clients are ready
            if (_readyClients.Count == NetworkManager.Singleton.ConnectedClientsIds.Count)
            {
                NotifyAllClientsReadyClientRpc();
                OnAllClientsReady?.Invoke();
            }
        }

        [ClientRpc]
        private void NotifyAllClientsReadyClientRpc()
        {
            OnAllClientsReady?.Invoke();
        }

        /// <summary>
        /// Fades out the transition overlay. Called after scene loading is complete.
        /// </summary>
        public void FadeOut()
        {
            if (!_isTransitioning) return;

            FadeOutClientRpc();
            _isTransitioning = false;
        }

        [ClientRpc]
        private void FadeOutClientRpc()
        {
            StartCoroutine(FadeCanvasGroup(_canvasGroup, 1f, 0f, _fadeDuration, () =>
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _canvasGroup.gameObject.SetActive(false);
            }));

            _isTransitioning = false;
        }

        /// <summary>
        /// Performs a local screen transition with customizable parameters.
        /// </summary>
        /// <param name="targetSceneName">Optional scene to load during transition (can be null)</param>
        /// <param name="onMidTransition">Optional action to execute mid-transition</param>
        /// <param name="holdDuration">How long to hold at full fade before fading out</param>
        public void LocalScreenTransition(string targetSceneName = null, Action onMidTransition = null, float holdDuration = 0f)
        {
            Debug.Log($"LocalScreenTransition called. Target scene: {targetSceneName}");

            if (_isTransitioning)
            {
                Debug.Log("Transition already in progress, returning");
                return;
            }

            if (_fadePanel == null || _panelImage == null)
            {
                Debug.LogError("SceneTransitionManager: _fadePanel or _panelImage is null. Please check inspector references.");
                return;
            }

            _isTransitioning = true;
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(LocalTransitionSequence(targetSceneName, onMidTransition, holdDuration));
        }

        private IEnumerator LocalTransitionSequence(string targetSceneName, Action onMidTransition, float holdDuration)
        {
            // Setup initial state
            _fadePanel.SetActive(true);
            Debug.Log("Fade panel activated");
            _panelImage.raycastTarget = true;
            _panelImage.color = new Color(_panelImage.color.r, _panelImage.color.g, _panelImage.color.b, 0f);

            // Fade in
            yield return FadeImage(_panelImage, 0f, 1f, _fadeDuration);

            // Execute mid-transition action
            onMidTransition?.Invoke();

            // Load scene if needed
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName);
                yield return loadOperation;
            }

            // Hold if specified
            if (holdDuration > 0)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            // Fade out
            yield return FadeImage(_panelImage, 1f, 0f, _fadeDuration);

            // Cleanup
            _panelImage.raycastTarget = false;
            _fadePanel.SetActive(false);
            _isTransitioning = false;
            _currentTransition = null;
        }

        /// <summary>
        /// Performs a simple fade to black and back without changing scenes.
        /// </summary>
        /// <param name="onMidTransition">Action to execute when fully faded to black</param>
        /// <param name="holdDuration">How long to hold at full fade before fading out</param>
        public void FadeTransition(Action onMidTransition = null, float holdDuration = 0f)
        {
            LocalScreenTransition(null, onMidTransition, holdDuration);
        }

        /// <summary>
        /// Immediately fades to black without fading back out. Use FadeOut when ready.
        /// </summary>
        public void FadeToBlack()
        {
            if (_isTransitioning) return;

            _fadePanel.SetActive(true);
            _panelImage.raycastTarget = true;
            _panelImage.color = new Color(_panelImage.color.r, _panelImage.color.g, _panelImage.color.b, 0f);

            _isTransitioning = true;
            StartCoroutine(FadeImage(_panelImage, 0f, 1f, _fadeDuration));
        }

        /// <summary>
        /// Sets or changes the fade color for transitions.
        /// </summary>
        /// <param name="color">The new fade color</param>
        public void SetFadeColor(Color color)
        {
            if (_panelImage == null) return;

            color.a = _panelImage.color.a;
            _panelImage.color = color;
        }

        private IEnumerator DelayedAction(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, Action onComplete = null)
        {
            float elapsedTime = 0;
            canvasGroup.alpha = startAlpha;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            onComplete?.Invoke();
        }

        private IEnumerator FadeImage(Image image, float startAlpha, float endAlpha, float duration, Action onComplete = null)
        {
            float elapsedTime = 0;
            Color startColor = image.color;
            startColor.a = startAlpha;
            Color endColor = image.color;
            endColor.a = endAlpha;
            image.color = startColor;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;
                Color newColor = image.color;
                newColor.a = Mathf.Lerp(startAlpha, endAlpha, normalizedTime);
                image.color = newColor;
                yield return null;
            }

            Color finalColor = image.color;
            finalColor.a = endAlpha;
            image.color = finalColor;
            onComplete?.Invoke();
        }

        private void OnDisable()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }
        }
    }
}