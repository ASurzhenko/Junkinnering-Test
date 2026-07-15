using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Junkinnering
{
    /// <summary>
    /// Task-A orchestrator: loads the target prefab and fallback texture via Addressables,
    /// spawns the object wearing the fallback texture, and logs hit/miss on tap. No round
    /// loop, scoring, image swap, or negative feedback yet — those are Task B.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private const string StatusLoading = "Loading...";
        private const string StatusReady = "Tap the object!";
        private const string StatusFailed = "Load failed";
        private const string InitialScore = "0";

        [SerializeField] private AssetReferenceGameObject _prefabRef;
        [SerializeField] private AssetReference _fallbackTextureRef;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private Camera _camera;

        private AddressableAssetService _service;
        private CancellationTokenSource _cts;
        private GameObject _spawnedInstance;

        private async void Start()
        {
            _cts = new CancellationTokenSource();
            _service = new AddressableAssetService(_prefabRef, _fallbackTextureRef);

            _scoreText.text = InitialScore;
            _statusText.text = StatusLoading;

            try
            {
                await _service.LoadAsync(_cts.Token);

                _spawnedInstance = Instantiate(_service.Prefab, _spawnAnchor.position, _spawnAnchor.rotation);
                Renderer renderer = _spawnedInstance.GetComponentInChildren<Renderer>();
                TextureApplier.Apply(renderer, _service.FallbackTexture);

                _statusText.text = StatusReady;
            }
            catch (OperationCanceledException)
            {
                // Teardown cancelled the load mid-flight — expected, nothing to report.
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(GameController)}.{nameof(Start)} init failed: {ex}");
                _statusText.text = StatusFailed;
            }
        }

        private void Update()
        {
            if (_spawnedInstance == null)
            {
                return;
            }

            if (!TapInput.TryGetTap(out Vector2 screenPos))
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(screenPos);
            bool isHit = Physics.Raycast(ray, out RaycastHit hit)
                         && hit.collider.transform.IsChildOf(_spawnedInstance.transform);

            Debug.Log($"{nameof(GameController)}.{nameof(Update)} {(isHit ? "hit" : "miss")}");
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            if (_spawnedInstance != null)
            {
                Destroy(_spawnedInstance);
                _spawnedInstance = null;
            }

            _service?.ReleaseAll();
        }
    }
}
