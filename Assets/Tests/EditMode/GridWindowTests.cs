using System;
using Junkinnering.Workshop;
using NUnit.Framework;

namespace Junkinnering.Tests
{
    /// <summary>
    /// The window math is what bounds how many icons are ever in memory, so these assert the index
    /// range the grid is OBLIGED to cover — including the two ends, where recycler off-by-ones live.
    /// </summary>
    public class GridWindowTests
    {
        private const float CellHeight = 100f;
        private const float Spacing = 0f;
        private const float ViewportHeight = 300f;
        private const int Columns = 3;
        private const int ItemCount = 30;      // 10 rows

        private static (int first, int last) Window(float scrollOffset, int bufferRows = 0)
        {
            return GridWindow.Compute(scrollOffset, ViewportHeight, CellHeight, Spacing, Columns, ItemCount, bufferRows);
        }

        [Test]
        public void AtTop_CoversTheFirstThreeRows()
        {
            var window = Window(0f);

            Assert.AreEqual(0, window.first);
            Assert.AreEqual(8, window.last);
        }

        [Test]
        public void MidScroll_StartsAtThePartiallyVisibleRow()
        {
            // 250 px down: row 2 is half off the top but still on screen, and the viewport bottom at
            // 550 px falls inside row 5.
            var window = Window(250f);

            Assert.AreEqual(6, window.first);
            Assert.AreEqual(17, window.last);
        }

        [Test]
        public void AtExactBottom_CoversTheLastRowAndStopsThere()
        {
            // Content is 1000 px over a 300 px viewport, so the bottom of the scroll is 700.
            var window = Window(700f);

            Assert.AreEqual(21, window.first);
            Assert.AreEqual(ItemCount - 1, window.last);
        }

        [Test]
        public void BottomEdgeLandingOnARowBoundary_DoesNotPullInTheNextRow()
        {
            // Viewport bottom at exactly 300 px: row 3 starts there and is not visible.
            var window = GridWindow.Compute(0f, 300f, 100f, 0f, 1, 10, 0);

            Assert.AreEqual(0, window.firstIndex);
            Assert.AreEqual(2, window.lastIndex);
        }

        [Test]
        public void ViewportTallerThanContent_CoversEverythingAndNoMore()
        {
            var window = GridWindow.Compute(0f, 2000f, CellHeight, Spacing, Columns, 6, 0);

            Assert.AreEqual(0, window.firstIndex);
            Assert.AreEqual(5, window.lastIndex);
        }

        [Test]
        public void EmptyCatalog_ReturnsAnEmptyWindow()
        {
            var window = GridWindow.Compute(0f, ViewportHeight, CellHeight, Spacing, Columns, 0, 1);

            Assert.Less(window.lastIndex, window.firstIndex, "lastIndex < firstIndex is the empty-window signal.");
        }

        [Test]
        public void FewerItemsThanOneRow_ClampsToTheItemCount()
        {
            var window = GridWindow.Compute(0f, ViewportHeight, CellHeight, Spacing, Columns, 2, 0);

            Assert.AreEqual(0, window.firstIndex);
            Assert.AreEqual(1, window.lastIndex);
        }

        [Test]
        public void BufferRows_ExtendBothEnds()
        {
            var unbuffered = Window(250f);
            var buffered = Window(250f, bufferRows: 1);

            Assert.AreEqual(unbuffered.first - Columns, buffered.first);
            Assert.AreEqual(unbuffered.last + Columns, buffered.last);
        }

        [Test]
        public void BufferRows_DoNotLeaveTheValidRangeAtTheTop()
        {
            var window = Window(0f, bufferRows: 2);

            Assert.AreEqual(0, window.first);
        }

        [Test]
        public void BufferRows_DoNotLeaveTheValidRangeAtTheBottom()
        {
            var window = Window(700f, bufferRows: 2);

            Assert.AreEqual(ItemCount - 1, window.last);
        }

        [Test]
        public void ElasticOverscrollAboveTheTop_ClampsToTheFirstRow()
        {
            var window = Window(-80f);

            Assert.AreEqual(0, window.first);
        }

        [Test]
        public void Spacing_IsPartOfTheRowStride()
        {
            // stride 120: 250 px down is inside row 2, and the bottom at 550 is inside row 4.
            var window = GridWindow.Compute(250f, ViewportHeight, CellHeight, 20f, Columns, ItemCount, 0);

            Assert.AreEqual(6, window.firstIndex);
            Assert.AreEqual(14, window.lastIndex);
        }

        [Test]
        public void ContentHeight_CountsGapsBetweenRowsOnly()
        {
            // 30 items over 3 columns is 10 rows: 10 cells plus 9 gaps.
            Assert.AreEqual((10f * CellHeight) + (9f * 20f), GridWindow.ContentHeight(ItemCount, CellHeight, 20f, Columns));
        }

        [Test]
        public void ZeroColumns_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GridWindow.Compute(0f, ViewportHeight, CellHeight, Spacing, 0, ItemCount, 0));
        }
    }
}
