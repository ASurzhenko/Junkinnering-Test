using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Junkinnering.Workshop;
using UnityEngine;

namespace Junkinnering.Tests
{
    /// <summary>
    /// Stands in for Addressables so handle lifecycle can be asserted without a content build.
    /// Counts a lease as live from the moment the load STARTS, which is what the real source does and
    /// what makes an in-flight handle visible to the invariants.
    /// </summary>
    public class FakeSpriteSource : ISpriteSource
    {
        private readonly HashSet<string> _failAddresses = new HashSet<string>();
        private readonly List<TaskCompletionSource<SpriteLease>> _pending = new List<TaskCompletionSource<SpriteLease>>();
        private readonly List<SpriteLease> _pendingLeases = new List<SpriteLease>();
        private readonly List<Object> _created = new List<Object>();

        private int _live;

        /// <summary>
        /// True hands back an already-completed Task, so the caller's await continues INLINE and the
        /// whole continuation runs before the call returns. False defers to <see cref="CompleteAll"/>.
        /// </summary>
        public bool CompleteImmediately { get; set; } = true;

        public int LiveCount => _live;

        public int ReleaseCount { get; private set; }

        public int LoadStartCount { get; private set; }

        public int PendingCount => _pending.Count;

        public void FailNext(string address)
        {
            _failAddresses.Add(address);
        }

        public Task<SpriteLease> LoadAsync(string address, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(default(SpriteLease));
            }

            if (_failAddresses.Contains(address))
            {
                _failAddresses.Remove(address);
                // A failed load owns and releases its own handle, so the count is unchanged either side.
                return Task.FromResult(default(SpriteLease));
            }

            LoadStartCount++;
            _live++;
            SpriteLease lease = new SpriteLease(CreateSprite(address), default);

            if (CompleteImmediately)
            {
                return Task.FromResult(lease);
            }

            var tcs = new TaskCompletionSource<SpriteLease>();
            _pending.Add(tcs);
            _pendingLeases.Add(lease);
            return tcs.Task;
        }

        /// <summary>Resolves every deferred load, oldest first.</summary>
        public void CompleteAll()
        {
            var tasks = new List<TaskCompletionSource<SpriteLease>>(_pending);
            var leases = new List<SpriteLease>(_pendingLeases);
            _pending.Clear();
            _pendingLeases.Clear();
            for (int i = 0; i < tasks.Count; i++)
            {
                tasks[i].SetResult(leases[i]);
            }
        }

        /// <summary>Resolves the oldest deferred load only, so an interleaving can be built.</summary>
        public void CompleteOldest()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var tcs = _pending[0];
            SpriteLease lease = _pendingLeases[0];
            _pending.RemoveAt(0);
            _pendingLeases.RemoveAt(0);
            tcs.SetResult(lease);
        }

        public void Release(SpriteLease lease)
        {
            if (!lease.HasSprite)
            {
                return;
            }

            ReleaseCount++;
            _live--;
        }

        public void DestroyCreatedSprites()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        private Sprite CreateSprite(string address)
        {
            var texture = new Texture2D(2, 2);
            texture.name = address;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            sprite.name = address;
            _created.Add(texture);
            _created.Add(sprite);
            return sprite;
        }
    }
}
