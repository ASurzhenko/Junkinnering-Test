using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Loads sprites by Addressable address and counts every handle it holds. Every exit — success,
    /// failure, cancellation — passes through one decrement, so the count cannot drift.
    /// </summary>
    public class AddressableSpriteSource : ISpriteSource
    {
        private int _liveCount;

        /// <summary>Raised once per failed load with the address and a classified reason.</summary>
        public event Action<string, string> LoadFailed;

        public int LiveCount => _liveCount;

        public async Task<SpriteLease> LoadAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address))
            {
                Report(address, "empty address");
                return default;
            }

            if (ct.IsCancellationRequested)
            {
                return default;
            }

            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(address);
            _liveCount++;

            try
            {
                await handle.Task;
            }
            catch (Exception ex)
            {
                // The operation itself never faults its Task; this covers the handle-invalid case,
                // where the Task getter throws.
                ReleaseInternal(handle);
                Report(address, Classify(ex));
                return default;
            }

            if (ct.IsCancellationRequested)
            {
                // Cancellation is a normal teardown, not a failure: release, decrement, no throw,
                // no log, and an invalid lease for the caller to discard.
                ReleaseInternal(handle);
                return default;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Exception operationException = handle.OperationException;
                ReleaseInternal(handle);
                Report(address, Classify(operationException));
                return default;
            }

            return new SpriteLease(handle.Result, handle);
        }

        public void Release(SpriteLease lease)
        {
            if (!lease.Handle.IsValid())
            {
                return;
            }

            ReleaseInternal(lease.Handle);
        }

        private void ReleaseInternal(AsyncOperationHandle<Sprite> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            _liveCount--;
        }

        private void Report(string address, string reason)
        {
            Debug.LogWarning($"{nameof(AddressableSpriteSource)}.{nameof(LoadAsync)} failed address={address} reason={reason}");
            Action<string, string> handler = LoadFailed;
            if (handler != null)
            {
                handler(address, reason);
            }
        }

        /// <summary>
        /// Three safeguards resolve to the same visible outcome — a placeholder — so the terminal has
        /// to say which one fired or they are indistinguishable.
        /// </summary>
        private static string Classify(Exception ex)
        {
            if (ex == null)
            {
                return "load did not succeed";
            }

            string message = ex.ToString();
            if (message.Contains("InvalidKey") || message.Contains("no locations"))
            {
                return "missing address";
            }

            if (message.Contains("imeout") || message.Contains("imed out"))
            {
                return "request timed out";
            }

            if (message.Contains("Cannot connect") || message.Contains("Unable to complete SSL")
                || message.Contains("UnityWebRequest") || message.Contains("RemoteProvider"))
            {
                return "host unreachable";
            }

            return ex.GetType().Name;
        }
    }
}
