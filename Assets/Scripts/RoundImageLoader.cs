using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Junkinnering
{
    /// <summary>
    /// Owns the per-round images: resolves a shared Addressables label to its texture
    /// locations once at startup, then hands out a fresh randomly-picked texture load per
    /// round. Unlike AddressableAssetService (load-once prefab + fallback), these image
    /// handles are loaded and released repeatedly — the caller (GameController) owns each
    /// per-round handle's release; this loader owns only the cached locations handle. Has no
    /// Unity lifecycle of its own.
    /// </summary>
    public class RoundImageLoader
    {
        private readonly AssetLabelReference _label;

        private AsyncOperationHandle<IList<IResourceLocation>> _locationsHandle;
        private IList<IResourceLocation> _locations;
        private int _lastIndex = -1;

        public RoundImageLoader(AssetLabelReference label)
        {
            _label = label;
        }

        /// <summary>
        /// Resolves the round-image label to its texture locations exactly once. Throws
        /// OperationCanceledException on cancel, or AddressableLoadException on a failed lookup
        /// OR an empty result. The empty-result check is load-bearing: LoadResourceLocationsAsync
        /// Succeeds with an empty list when the label matches nothing, so a missing/mistyped
        /// label would otherwise surface later as an index error in LoadRandom.
        /// </summary>
        public async Task InitAsync(CancellationToken ct)
        {
            _locationsHandle = Addressables.LoadResourceLocationsAsync(_label, typeof(Texture2D));
            await _locationsHandle.Task;
            // If cancelled, the handle is left assigned so OnDestroy's ReleaseAll releases it.
            ct.ThrowIfCancellationRequested();

            if (_locationsHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Exception inner = _locationsHandle.OperationException;
                ReleaseLocations();
                throw new AddressableLoadException(
                    $"Round-image location lookup failed for label '{_label.labelString}'", inner);
            }

            IList<IResourceLocation> result = _locationsHandle.Result;
            if (result == null || result.Count == 0)
            {
                ReleaseLocations();
                throw new AddressableLoadException(
                    $"No addressables tagged with round-image label '{_label.labelString}'", null);
            }

            _locations = result;
            Debug.Log($"{nameof(RoundImageLoader)}.{nameof(InitAsync)} resolved {_locations.Count} round images for label '{_label.labelString}'");
        }

        /// <summary>
        /// Starts loading a random round texture and returns its handle without awaiting. The
        /// caller awaits handle.Task, checks Status + its own cancellation token, and owns the
        /// release — this method never throws so a handle can never be trapped and leaked. The
        /// pick avoids repeating the previous image only when the set has more than one.
        /// </summary>
        public AsyncOperationHandle<Texture2D> LoadRandom()
        {
            int index = _locations.Count > 1 ? PickDifferentIndex() : 0;
            _lastIndex = index;
            return Addressables.LoadAssetAsync<Texture2D>(_locations[index]);
        }

        /// <summary>
        /// Releases a per-round image handle through the single owning path. Safe on a
        /// pending / already-released handle (IsValid guards it), so a cancelled round's late
        /// release after teardown is a no-op.
        /// </summary>
        public void Release(AsyncOperationHandle<Texture2D> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        /// <summary>
        /// Releases the cached locations handle. Idempotent; the per-round image handles are
        /// owned and released by the caller, not here.
        /// </summary>
        public void ReleaseAll()
        {
            ReleaseLocations();
        }

        private void ReleaseLocations()
        {
            if (_locationsHandle.IsValid())
            {
                Addressables.Release(_locationsHandle);
            }

            _locationsHandle = default;
            _locations = null;
        }

        private int PickDifferentIndex()
        {
            int index;
            do
            {
                index = UnityEngine.Random.Range(0, _locations.Count);
            }
            while (index == _lastIndex);

            return index;
        }
    }
}
