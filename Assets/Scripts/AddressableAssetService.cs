using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Junkinnering
{
    /// <summary>
    /// Thrown when an Addressables load completes without a Succeeded status. Carries the
    /// underlying OperationException so the caller can tell a load failure apart from a
    /// cancellation (which surfaces as OperationCanceledException instead).
    /// </summary>
    public class AddressableLoadException : Exception
    {
        public AddressableLoadException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Loads the target-object prefab and the fallback texture exactly once each via
    /// Addressables, holds their handles, and releases them on teardown. Has no Unity
    /// lifecycle of its own — owned and driven by GameController.
    /// </summary>
    public class AddressableAssetService
    {
        private readonly AssetReferenceGameObject _prefabRef;
        private readonly AssetReference _fallbackTextureRef;

        private AsyncOperationHandle<GameObject> _prefabHandle;
        private AsyncOperationHandle<Texture2D> _fallbackHandle;

        private Task _loadTask;

        public GameObject Prefab { get; private set; }
        public Texture2D FallbackTexture { get; private set; }

        public AddressableAssetService(AssetReferenceGameObject prefabRef, AssetReference fallbackTextureRef)
        {
            _prefabRef = prefabRef;
            _fallbackTextureRef = fallbackTextureRef;
        }

        /// <summary>
        /// Loads both assets once. A second call returns the same in-flight / completed task
        /// instead of re-loading. Throws OperationCanceledException on cancel, or
        /// AddressableLoadException on a failed load.
        /// </summary>
        public Task LoadAsync(CancellationToken ct)
        {
            return _loadTask ??= LoadInternalAsync(ct);
        }

        private async Task LoadInternalAsync(CancellationToken ct)
        {
            _prefabHandle = _prefabRef.LoadAssetAsync<GameObject>();
            await _prefabHandle.Task;
            // Cancellation and failure are checked right after the await, before the result is
            // read or the next load starts — the guard lives with the async hop
            // (async-cancellation-matrix.md). handle.Task does not throw on failure; the error
            // is on handle.Status / OperationException, so the status check is what catches it.
            ct.ThrowIfCancellationRequested();
            if (_prefabHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"{nameof(AddressableAssetService)}.{nameof(LoadInternalAsync)} prefab load failed: {_prefabHandle.OperationException}");
                throw new AddressableLoadException("Prefab load failed", _prefabHandle.OperationException);
            }

            Prefab = _prefabHandle.Result;

            _fallbackHandle = _fallbackTextureRef.LoadAssetAsync<Texture2D>();
            await _fallbackHandle.Task;
            ct.ThrowIfCancellationRequested();
            if (_fallbackHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"{nameof(AddressableAssetService)}.{nameof(LoadInternalAsync)} fallback texture load failed: {_fallbackHandle.OperationException}");
                throw new AddressableLoadException("Fallback texture load failed", _fallbackHandle.OperationException);
            }

            FallbackTexture = _fallbackHandle.Result;
        }

        /// <summary>
        /// Releases both handles via the single owning path (Addressables.Release) and clears
        /// the cached asset references. Idempotent and safe on a pending / never-loaded handle.
        /// </summary>
        public void ReleaseAll()
        {
            if (_prefabHandle.IsValid())
            {
                Addressables.Release(_prefabHandle);
                _prefabHandle = default;
            }

            if (_fallbackHandle.IsValid())
            {
                Addressables.Release(_fallbackHandle);
                _fallbackHandle = default;
            }

            Prefab = null;
            FallbackTexture = null;
        }
    }
}
