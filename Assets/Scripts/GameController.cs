using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Junkinnering
{
    /// <summary>
    /// Round-loop orchestrator: loads the target prefab and fallback texture via Addressables,
    /// spawns the object, then runs the game loop — each round loads a new random image
    /// (Addressables label), a hit on the object scores + starts the next round, a miss flashes
    /// the object red. A new round cancels the previous in-flight image load so a stale download
    /// can never overwrite a newer round's texture.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private const string StatusLoading = "Loading...";
        private const string StatusLoadingImage = "Loading image...";
        private const string StatusReady = "Tap the object!";
        private const string StatusFailed = "Load failed";
        private const string InitialScore = "0";

        private static readonly Color FlashColor = Color.red;

        [SerializeField] private AssetReferenceGameObject _prefabRef;
        [SerializeField] private AssetReference _fallbackTextureRef;
        [SerializeField] private AssetLabelReference _roundImageLabel;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private Camera _camera;

        private AddressableAssetService _service;
        private RoundImageLoader _roundLoader;
        private CancellationTokenSource _cts;
        private GameObject _spawnedInstance;
        private Renderer _targetRenderer;

        private int _score;

        // Holds the newest round's CTS so the next round can cancel it. The apply/discard
        // decision keys off each round's OWN captured local token, never this field.
        private CancellationTokenSource _roundCts;
        // The image handle currently applied to the object; released when replaced.
        private AsyncOperationHandle<Texture2D> _currentImageHandle;

        private Coroutine _flashRoutine;
        private readonly WaitForSeconds _flashWait = new WaitForSeconds(0.15f);

        private bool _isTornDown;

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
                _targetRenderer = _spawnedInstance.GetComponentInChildren<Renderer>();
                TextureApplier.Apply(_targetRenderer, _service.FallbackTexture);

                _roundLoader = new RoundImageLoader(_roundImageLabel);
                await _roundLoader.InitAsync(_cts.Token);

                await StartRoundAsync();
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

        /// <summary>
        /// Loads a new random image and applies it. A per-round CTS linked to the master _cts
        /// lets a newer round cancel this one; the apply/discard decision reads the CAPTURED
        /// local roundCts, never the reassignable _roundCts field.
        /// </summary>
        private async Task StartRoundAsync()
        {
            CancellationTokenSource roundCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            AsyncOperationHandle<Texture2D> pending = default;
            try
            {
                _roundCts?.Cancel();          // cancel the previous round (its still-live CTS)
                _roundCts = roundCts;         // newest becomes current
                _statusText.text = StatusLoadingImage;

                pending = _roundLoader.LoadRandom();
                await pending.Task;

                if (roundCts.IsCancellationRequested)   // LOCAL token — not the _roundCts field
                {
                    _roundLoader.Release(pending);       // superseded: discard, never apply
                    return;
                }

                if (pending.Status != AsyncOperationStatus.Succeeded)
                {
                    _roundLoader.Release(pending);
                    TextureApplier.Apply(_targetRenderer, _service.FallbackTexture);   // apply fallback FIRST
                    if (_currentImageHandle.IsValid())                                 // then release the old round handle
                    {
                        _roundLoader.Release(_currentImageHandle);
                        _currentImageHandle = default;
                    }
                    _statusText.text = StatusReady;
                    return;
                }

                TextureApplier.Apply(_targetRenderer, pending.Result);                  // apply new FIRST
                if (_currentImageHandle.IsValid())
                {
                    _roundLoader.Release(_currentImageHandle);                          // then release old
                }
                _currentImageHandle = pending;                                          // promote
                _statusText.text = StatusReady;
            }
            catch (OperationCanceledException)
            {
                if (pending.IsValid())
                {
                    _roundLoader.Release(pending);       // silent teardown
                }
            }
            catch (Exception ex)
            {
                if (pending.IsValid())
                {
                    _roundLoader.Release(pending);
                }
                Debug.LogError($"{nameof(GameController)}.{nameof(StartRoundAsync)} failed: {ex}");
                _statusText.text = StatusFailed;
            }
            finally
            {
                if (_roundCts == roundCts)   // still current → leave the field null, not a disposed CTS
                {
                    _roundCts = null;
                }
                roundCts.Dispose();          // owner disposes ITS OWN linked CTS
            }
        }

        private void Update()
        {
            if (_isTornDown || _spawnedInstance == null)
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

            if (isHit)
            {
                OnCorrectTap();
            }
            else
            {
                OnIncorrectTap();
            }
        }

        private void OnCorrectTap()
        {
            _score++;
            _scoreText.text = _score.ToString();
            _ = StartRoundAsync();   // fire-and-forget: cancels any in-flight round, self-contains exceptions
        }

        private void OnIncorrectTap()
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }
            _flashRoutine = StartCoroutine(FlashRed());
        }

        private IEnumerator FlashRed()
        {
            TextureApplier.SetTint(_targetRenderer, FlashColor);
            yield return _flashWait;
            TextureApplier.SetTint(_targetRenderer, Color.white);   // restore from white each start → interrupted flash re-restores cleanly
            _flashRoutine = null;
        }

        private void OnDestroy()
        {
            _isTornDown = true;

            _cts?.Cancel();   // cascades to the linked round token; a parked round self-disposes in its finally
            _cts?.Dispose();

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            // Destroy the spawned instance BEFORE releasing the Addressables handles that back its
            // textures, so a released texture can never be unloaded while the renderer still exists.
            if (_spawnedInstance != null)
            {
                Destroy(_spawnedInstance);
                _spawnedInstance = null;
            }

            if (_currentImageHandle.IsValid())
            {
                _roundLoader.Release(_currentImageHandle);
                _currentImageHandle = default;
            }
            _roundLoader?.ReleaseAll();

            _service?.ReleaseAll();
        }
    }
}
