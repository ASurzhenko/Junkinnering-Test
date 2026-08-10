using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// How the picker fills its grid. Naive is the default implementation anyone would write first —
    /// a cell per item, each loading its own icon, nothing released — kept so the windowed path's
    /// numbers can be compared against something real rather than asserted.
    /// </summary>
    public enum PickerLoadStrategy
    {
        Windowed = 0,
        Naive = 1
    }

    /// <summary>
    /// The part inventory overlay: a fixed pool of cards positioned by index over a scroll content
    /// sized from the item count. The content carries NO layout group — a GridLayoutGroup would
    /// re-lay the pooled cells at indices 0..poolSize-1 every frame and silently defeat virtualization.
    /// </summary>
    public class PartPickerView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private PartCardView _cardPrefab;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _closeButton;

        [Header("Grid metrics")]
        [SerializeField] private float _cellWidth = 200f;
        [SerializeField] private float _cellHeight = 240f;
        [SerializeField] private float _spacing = 16f;
        [SerializeField] private int _columns = 3;
        [SerializeField] private int _bufferRows = 1;

        private readonly List<PartCardView> _pool = new List<PartCardView>();
        private readonly List<int> _boundIndex = new List<int>();

        private IReadOnlyList<PartDefinition> _items;
        private ISpriteSource _source;
        private CancellationToken _ct;
        private Action<PartDefinition> _onPicked;
        private string _selectedId;
        private bool _isOpen;
        private PartSlot _slot;
        private float _lastOpenMilliseconds;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly List<PartCardView> _naiveCells = new List<PartCardView>();
        private PickerLoadStrategy _strategy = PickerLoadStrategy.Windowed;
#endif

        public int PoolSize => _pool.Count;

        public bool IsOpen => _isOpen;

        /// <summary>Milliseconds the last <see cref="Open"/> spent, which is the whole naive/windowed point.</summary>
        public float LastOpenMilliseconds => _lastOpenMilliseconds;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public PickerLoadStrategy Strategy => _strategy;

        /// <summary>Cells that currently exist: the pool in windowed mode, one per item in naive mode.</summary>
        public int InstantiatedCellCount => _strategy == PickerLoadStrategy.Naive ? _naiveCells.Count : _pool.Count;

        /// <summary>
        /// Switches the fill strategy live. Unbinds everything first, so each comparison starts from a
        /// live-handle count of zero rather than from whatever the previous mode was still holding.
        /// </summary>
        public void SetStrategy(PickerLoadStrategy strategy)
        {
            if (strategy == _strategy)
            {
                return;
            }

            bool wasOpen = _isOpen;
            PartSlot slot = _slot;
            IReadOnlyList<PartDefinition> items = _items;
            ISpriteSource source = _source;
            CancellationToken ct = _ct;
            string selectedId = _selectedId;
            Action<PartDefinition> onPicked = _onPicked;

            Close();
            _strategy = strategy;

            if (wasOpen)
            {
                Open(slot, items, source, ct, selectedId, onPicked);
            }
        }
#else
        public int InstantiatedCellCount => _pool.Count;
#endif

        private void Awake()
        {
            _scrollRect.onValueChanged.AddListener(HandleScrolled);
            _closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            _scrollRect.onValueChanged.RemoveListener(HandleScrolled);
            _closeButton.onClick.RemoveListener(Close);
        }

        public void Open(
            PartSlot slot,
            IReadOnlyList<PartDefinition> items,
            ISpriteSource source,
            CancellationToken ct,
            string selectedId,
            Action<PartDefinition> onPicked)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Open is reachable without a Close in between — reopening on the same slot with a
            // different list size is one caller that does it. FillNaive only appends, so the previous
            // cells would survive under the content and keep their leases. Torn down here, before the
            // timer starts (teardown is not part of the measured open cost) and before _source is
            // reassigned (these cells were leased from the PREVIOUS source).
            ClearNaiveCells();
#endif

            var openTimer = System.Diagnostics.Stopwatch.StartNew();

            _slot = slot;
            _items = items;
            _source = source;
            _ct = ct;
            _selectedId = selectedId;
            _onPicked = onPicked;
            _titleText.text = slot.ToString().ToUpperInvariant();

            _root.SetActive(true);
            _isOpen = true;

            // The pool size and the window both come off the viewport's height, so it has to be laid
            // out before either is computed.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_viewport);
            AssertContentAnchoring();

            _content.sizeDelta = new Vector2(
                _content.sizeDelta.x,
                GridWindow.ContentHeight(items.Count, _cellHeight, _spacing, _columns));

            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 1f;
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_strategy == PickerLoadStrategy.Naive)
            {
                FillNaive();
                openTimer.Stop();
                _lastOpenMilliseconds = (float)openTimer.Elapsed.TotalMilliseconds;
                return;
            }
#endif

            EnsurePool();
            ReleaseAllCells();
            Refresh();

            openTimer.Stop();
            _lastOpenMilliseconds = (float)openTimer.Elapsed.TotalMilliseconds;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// The strawman: instantiate a cell per entry and start every icon load in this one frame.
        /// Nothing is released until Close, which is exactly what the windowed path exists to avoid.
        /// </summary>
        private void FillNaive()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                PartCardView cell = Instantiate(_cardPrefab, _content);
                cell.name = $"NaiveCard_{i}";
                cell.SetClickHandler(HandlePicked);
                Place(cell, i);
                cell.Bind(_items[i], _source, _ct);
                cell.SetSelected(_items[i].Id == _selectedId);
                _naiveCells.Add(cell);
            }
        }

        private void ClearNaiveCells()
        {
            for (int i = 0; i < _naiveCells.Count; i++)
            {
                PartCardView cell = _naiveCells[i];
                if (cell == null)
                {
                    continue;
                }

                cell.Unbind(_source);
                Destroy(cell.gameObject);
            }

            _naiveCells.Clear();
        }
#endif

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            // Deactivating the panel releases nothing: every pooled cell has to be unbound explicitly.
            ReleaseAllCells();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ClearNaiveCells();
#endif
            _isOpen = false;
            _items = null;
            _onPicked = null;
            _root.SetActive(false);
        }

        private void HandleScrolled(Vector2 _)
        {
            if (_isOpen)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_items == null)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_strategy == PickerLoadStrategy.Naive)
            {
                // Every cell already exists and is bound; there is no window to recompute.
                return;
            }
#endif

            // Positive and growing as the user scrolls down: ScrollRect raises the content's
            // anchoredPosition.y by the hidden length as verticalNormalizedPosition falls to 0.
            float scrollOffset = _content.anchoredPosition.y;

            var window = GridWindow.Compute(
                scrollOffset, _viewport.rect.height, _cellHeight, _spacing, _columns, _items.Count, _bufferRows);

            if (window.lastIndex < window.firstIndex)
            {
                ReleaseAllCells();
                return;
            }

            int windowSize = window.lastIndex - window.firstIndex + 1;
            if (windowSize > _pool.Count)
            {
                Debug.LogError($"{nameof(PartPickerView)}.{nameof(Refresh)} window {windowSize} exceeds pool {_pool.Count} — " +
                               "one cell would be bound to two indices in the same frame.");
                window.lastIndex = window.firstIndex + _pool.Count - 1;
            }

            for (int c = 0; c < _pool.Count; c++)
            {
                int bound = _boundIndex[c];
                if (bound >= 0 && (bound < window.firstIndex || bound > window.lastIndex))
                {
                    _pool[c].Unbind(_source);
                    _pool[c].gameObject.SetActive(false);
                    _boundIndex[c] = -1;
                }
            }

            for (int i = window.firstIndex; i <= window.lastIndex; i++)
            {
                int c = i % _pool.Count;
                if (_boundIndex[c] == i)
                {
                    continue;
                }

                PartCardView cell = _pool[c];
                cell.gameObject.SetActive(true);
                Place(cell, i);
                cell.Bind(_items[i], _source, _ct);
                cell.SetSelected(_items[i].Id == _selectedId);
                _boundIndex[c] = i;
            }
        }

        private void Place(PartCardView cell, int index)
        {
            int row = index / _columns;
            int column = index % _columns;
            float gridWidth = (_columns * _cellWidth) + ((_columns - 1) * _spacing);
            float leftPad = Mathf.Max(0f, (_viewport.rect.width - gridWidth) * 0.5f);

            var rt = (RectTransform)cell.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_cellWidth, _cellHeight);
            rt.anchoredPosition = new Vector2(
                leftPad + (column * (_cellWidth + _spacing)),
                -(row * (_cellHeight + _spacing)));
        }

        /// <summary>
        /// Derives the pool from the same inputs the window math uses. If the window could exceed the
        /// pool, one cell would be bound to two indices at once — a display bug and the worst case of
        /// double-in-flight.
        /// </summary>
        private void EnsurePool()
        {
            float rowStride = _cellHeight + _spacing;
            int visibleRows = Mathf.CeilToInt(_viewport.rect.height / rowStride) + 1;
            int poolSize = (visibleRows + (2 * _bufferRows)) * _columns;

            while (_pool.Count < poolSize)
            {
                PartCardView cell = Instantiate(_cardPrefab, _content);
                cell.name = $"PartCard_{_pool.Count}";
                cell.SetClickHandler(HandlePicked);
                cell.gameObject.SetActive(false);
                _pool.Add(cell);
                _boundIndex.Add(-1);
            }
        }

        private void ReleaseAllCells()
        {
            for (int c = 0; c < _pool.Count; c++)
            {
                _pool[c].Unbind(_source);
                _pool[c].gameObject.SetActive(false);
                _boundIndex[c] = -1;
            }
        }

        private void HandlePicked(PartDefinition part)
        {
            Action<PartDefinition> handler = _onPicked;
            _selectedId = part.Id;
            if (handler != null)
            {
                handler(part);
            }
        }

        /// <summary>
        /// The window math reads anchoredPosition.y as a positive distance from the top, which only
        /// holds for a top-anchored, top-pivot content. Authoring drift here produces a grid frozen on
        /// row 0 with an entirely green test suite, because the tests feed Compute their own numbers.
        /// </summary>
        private void AssertContentAnchoring()
        {
            bool topAnchored = Mathf.Approximately(_content.anchorMin.y, 1f) && Mathf.Approximately(_content.anchorMax.y, 1f);
            bool topPivot = Mathf.Approximately(_content.pivot.y, 1f);
            if (!topAnchored || !topPivot)
            {
                Debug.LogError($"{nameof(PartPickerView)}.{nameof(AssertContentAnchoring)} content must be top-anchored with a top pivot " +
                               $"(anchor y = 1, pivot y = 1); got anchorMin.y={_content.anchorMin.y} anchorMax.y={_content.anchorMax.y} pivot.y={_content.pivot.y}");
            }
        }
    }
}
