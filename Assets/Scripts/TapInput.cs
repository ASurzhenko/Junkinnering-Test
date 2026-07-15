using UnityEngine;
using UnityEngine.InputSystem;

namespace Junkinnering
{
    /// <summary>
    /// Reads a single tap performed this frame from the active pointer — mouse in the Editor,
    /// primary touch on device — via the Input System. Null-safe when no pointer is present.
    /// </summary>
    public static class TapInput
    {
        public static bool TryGetTap(out Vector2 screenPos)
        {
            screenPos = default;

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return false;
            }

            screenPos = pointer.position.ReadValue();
            return true;
        }
    }
}
