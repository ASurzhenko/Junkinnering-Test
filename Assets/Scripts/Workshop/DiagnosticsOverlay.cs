using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Live handle-count and memory readout, plus the naive/windowed toggle. Development-only: it
    /// removes itself from a release build, so an APK sent for review must be a Development Build for
    /// any of it to appear.
    /// </summary>
    public class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _strategyButton;
        [SerializeField] private TMP_Text _strategyButtonLabel;
        [SerializeField] private Button _stressButton;
        [SerializeField] private TMP_Text _stressButtonLabel;
        [SerializeField] private float _refreshInterval = 0.25f;

        private static readonly int[] StressSteps = { 1, 5 };

        private ISpriteSource _source;
        private PartPickerView _picker;
        private WorkshopController _controller;
        private float _nextRefresh;
        private string _failureAddress;
        private string _failureReason;

        private void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Destroy(gameObject);
#else
            _text.text = string.Empty;
            _strategyButton.onClick.AddListener(ToggleStrategy);
            _stressButton.onClick.AddListener(CycleStress);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnDestroy()
        {
            _strategyButton.onClick.RemoveListener(ToggleStrategy);
            _stressButton.onClick.RemoveListener(CycleStress);
        }
#endif

        public void Bind(ISpriteSource source, PartPickerView picker, WorkshopController controller)
        {
            _source = source;
            _picker = picker;
            _controller = controller;
        }

        /// <summary>
        /// Names WHICH safeguard produced the placeholder. A missing address, an unreachable host and a
        /// timeout all end in the same visible outcome, so without the reason they are indistinguishable.
        /// </summary>
        public void ReportFailure(string address, string reason)
        {
            _failureAddress = address;
            _failureReason = reason;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ToggleStrategy()
        {
            if (_picker == null)
            {
                return;
            }

            _picker.SetStrategy(_picker.Strategy == PickerLoadStrategy.Windowed
                ? PickerLoadStrategy.Naive
                : PickerLoadStrategy.Windowed);
            UpdateStrategyLabel();
        }

        private void UpdateStrategyLabel()
        {
            if (_picker == null)
            {
                return;
            }

            _strategyButtonLabel.text = _picker.Strategy == PickerLoadStrategy.Windowed
                ? "MODE: WINDOWED"
                : "MODE: NAIVE";
        }

        /// <summary>
        /// Steps the list size so the demo can be recorded at the scale where the contrast is felt,
        /// without a second build.
        /// </summary>
        private void CycleStress()
        {
            if (_controller == null)
            {
                return;
            }

            int current = _controller.StressMultiplier;
            int next = StressSteps[0];
            for (int i = 0; i < StressSteps.Length; i++)
            {
                if (StressSteps[i] == current)
                {
                    next = StressSteps[(i + 1) % StressSteps.Length];
                    break;
                }
            }

            _controller.SetStressMultiplier(next);
            UpdateStressLabel();
        }

        private void UpdateStressLabel()
        {
            if (_controller == null)
            {
                return;
            }

            _stressButtonLabel.text = $"LIST: x{_controller.StressMultiplier}";
        }
#endif

        private void Update()
        {
            if (_source == null || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + _refreshInterval;

            int poolSize = _picker != null ? _picker.PoolSize : 0;
            long allocated = Profiler.GetTotalAllocatedMemoryLong();
            ulong textureMemory = Texture.currentTextureMemory;

            string line = $"live handles: {_source.LiveCount}  (grid pool {poolSize} — instantaneous, may exceed the pool mid-scroll)\n" +
                          $"texture memory: {ToMb(textureMemory)} MB    allocated: {ToMb((ulong)allocated)} MB";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_picker != null)
            {
                // The naive label states what it does, so the comparison reads as honest rather than rigged.
                string mode = _picker.Strategy == PickerLoadStrategy.Windowed
                    ? "windowed: pooled cells, released on recycle"
                    : "naive: one cell per item, no release";
                int listSize = _controller != null ? _controller.StressMultiplier : 1;
                line += $"\nmode: {mode}    list x{listSize}\n" +
                        $"cells instantiated: {_picker.InstantiatedCellCount}    picker open cost: {_picker.LastOpenMilliseconds:F1} ms";
                UpdateStrategyLabel();
                UpdateStressLabel();
            }
#endif

            if (!string.IsNullOrEmpty(_failureReason))
            {
                line += $"\nlast failure: {_failureAddress} — {_failureReason}";
            }

            _text.text = line;
        }

        private static string ToMb(ulong bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("F1");
        }
    }
}
