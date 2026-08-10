using System;
using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The visible-index math of the virtualized part grid, as a pure function. Off-by-one in the
    /// first/last index is where recycler bugs live, so it is kept free of Unity runtime state and
    /// tested directly.
    /// </summary>
    public static class GridWindow
    {
        /// <summary>
        /// Indices that must be bound for a given scroll position, inclusive on both ends and clamped
        /// to [0, itemCount-1].
        ///
        /// scrollOffset: distance scrolled from the top, in content-local pixels, 0 at the top.
        /// The view feeds it as content.anchoredPosition.y.
        ///
        /// Sign: ScrollRect's verticalNormalizedPosition 1 is the top, and moving toward 0 raises the
        /// content's anchoredPosition by the hidden length, so anchoredPosition.y INCREASES as the user
        /// scrolls down. This assumes the content RectTransform is top-anchored with a top pivot
        /// (anchorMin/anchorMax y = 1, pivot y = 1) sitting at y == 0 when scrolled to the top — the
        /// view asserts that rather than trusting the authoring.
        ///
        /// Returns lastIndex &lt; firstIndex for an empty window (itemCount == 0).
        /// </summary>
        public static (int firstIndex, int lastIndex) Compute(
            float scrollOffset,
            float viewportHeight,
            float cellHeight,
            float spacing,
            int columns,
            int itemCount,
            int bufferRows)
        {
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "Must be positive.");
            }

            if (cellHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellHeight), cellHeight, "Must be positive.");
            }

            if (spacing < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Must not be negative.");
            }

            if (viewportHeight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewportHeight), viewportHeight, "Must not be negative.");
            }

            if (bufferRows < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferRows), bufferRows, "Must not be negative.");
            }

            if (itemCount <= 0)
            {
                return (0, -1);
            }

            // Elastic overscroll drives anchoredPosition.y below zero at the top; that is a normal
            // ScrollRect state, so it clamps rather than warning.
            float offset = Mathf.Max(0f, scrollOffset);
            float rowStride = cellHeight + spacing;

            int rowCount = (itemCount + columns - 1) / columns;
            int lastRow = rowCount - 1;

            int firstVisibleRow = Mathf.FloorToInt(offset / rowStride);
            // The row containing the viewport's bottom edge. Ceil-minus-one so a bottom edge landing
            // exactly on a row boundary does not pull in the row that starts there.
            int lastVisibleRow = Mathf.CeilToInt((offset + viewportHeight) / rowStride) - 1;
            if (lastVisibleRow < firstVisibleRow)
            {
                lastVisibleRow = firstVisibleRow;
            }

            int firstRow = Mathf.Clamp(firstVisibleRow - bufferRows, 0, lastRow);
            int lastBufferedRow = Mathf.Clamp(lastVisibleRow + bufferRows, 0, lastRow);

            int firstIndex = firstRow * columns;
            int lastIndex = Mathf.Min((lastBufferedRow * columns) + columns - 1, itemCount - 1);

            return (firstIndex, lastIndex);
        }

        /// <summary>Height the scroll content must have for <paramref name="itemCount"/> entries.</summary>
        public static float ContentHeight(int itemCount, float cellHeight, float spacing, int columns)
        {
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "Must be positive.");
            }

            if (itemCount <= 0)
            {
                return 0f;
            }

            int rowCount = (itemCount + columns - 1) / columns;
            return (rowCount * cellHeight) + ((rowCount - 1) * spacing);
        }
    }
}
