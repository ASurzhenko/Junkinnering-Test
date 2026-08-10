using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// A sprite plus the handle that keeps it alive. Whoever stores one owns exactly one release.
    /// </summary>
    public readonly struct SpriteLease
    {
        public readonly Sprite Sprite;
        public readonly AsyncOperationHandle<Sprite> Handle;

        public SpriteLease(Sprite sprite, AsyncOperationHandle<Sprite> handle)
        {
            Sprite = sprite;
            Handle = handle;
        }

        /// <summary>
        /// Whether this lease carries art to display. Deliberately named apart from Handle.IsValid():
        /// they answer different questions and guarding on the wrong one is silent.
        /// </summary>
        public bool HasSprite => Sprite != null;
    }

    /// <summary>
    /// The one abstraction in this screen, and it exists for testability: handle-lifecycle correctness
    /// cannot be asserted against real Addressables in EditMode.
    /// </summary>
    public interface ISpriteSource
    {
        /// <summary>
        /// Never throws — not on failure, not on cancellation. Both return a lease with no sprite,
        /// so the caller's placeholder branch is a real branch rather than a swallowed exception.
        /// </summary>
        Task<SpriteLease> LoadAsync(string address, CancellationToken ct);

        /// <summary>No-op on a default or already-released lease.</summary>
        void Release(SpriteLease lease);

        /// <summary>
        /// Handles held right now, INCLUDING loads still in flight — an in-flight handle is exactly
        /// what consumes memory, so a count that ignored them would not bound anything.
        /// </summary>
        int LiveCount { get; }
    }
}
