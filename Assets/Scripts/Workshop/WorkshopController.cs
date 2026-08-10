using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Owns the loadout, the equip path and the picker. The only Unity lifecycle owner on this
    /// screen — the sprite source and the loadout are plain C# objects it constructs and tears down.
    /// </summary>
    public class WorkshopController : MonoBehaviour
    {
        [SerializeField] private PartCatalog _catalog;
        [SerializeField] private RobotRigView _rig;
        [SerializeField] private StatsPanelView _statsPanel;
        [SerializeField] private PartPickerView _picker;
        [SerializeField] private Sprite _placeholder;
        [SerializeField] private DiagnosticsOverlay _diagnostics;

        // Indexed by (int)PartSlot: the left column's equipped-slot buttons and the art they show.
        [SerializeField] private Button[] _slotButtons = new Button[5];
        [SerializeField] private Image[] _slotButtonIcons = new Image[5];
        [SerializeField] private TMP_Text[] _slotButtonLabels = new TMP_Text[5];

        // One-directional: the workshop can reach the tap game, the tap game has no button back.
        // That scene has no EventSystem and taps unconditionally, so a button there would raycast,
        // miss the target and register as an incorrect tap.
        [SerializeField] private Button _tapGameButton;

        // Dev amplifier: 1 uses the real catalog, which already recycles at 30 entries per slot.
        [SerializeField] private int _stressMultiplier = 1;

        private const string TapGameSceneName = "TapGameScene";

        private readonly RobotLoadout _loadout = new RobotLoadout();
        private readonly SpriteLease[] _slotLeases = new SpriteLease[PartSlots.Count];
        private readonly int[] _slotGenerations = new int[PartSlots.Count];

        // Held as the interface so a test can substitute a fake; the concrete reference is kept only
        // for the failure event, which is not part of the interface.
        private ISpriteSource _spriteSource;
        private AddressableSpriteSource _addressableSource;
        private CancellationTokenSource _cts;

        public ISpriteSource SpriteSource => _spriteSource;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PartSlot _lastPickerSlot;

        public int StressMultiplier => Mathf.Max(1, _stressMultiplier);

        /// <summary>
        /// Amplifies the open list so the naive/windowed contrast is measured at demo scale. Reopens
        /// the picker on the same slot, because the multiplier is read when the list is built.
        /// </summary>
        public void SetStressMultiplier(int multiplier)
        {
            _stressMultiplier = Mathf.Max(1, multiplier);
            if (_picker.IsOpen)
            {
                OpenPicker(_lastPickerSlot);
            }
        }
#endif

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _addressableSource = new AddressableSpriteSource();
            _addressableSource.LoadFailed += HandleLoadFailed;
            _spriteSource = _addressableSource;
        }

        private void Start()
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                PartSlot slot = (PartSlot)i;
                _slotButtons[i].onClick.AddListener(() => OpenPicker(slot));
            }

            _tapGameButton.onClick.AddListener(LoadTapGame);

            // The overlay deletes itself in a non-development build, so it is the one serialized
            // reference on this screen that is legitimately absent at runtime.
            if (_diagnostics != null)
            {
                _diagnostics.Bind(_spriteSource, _picker, this);
            }

            ComposeStartingRobot();
        }

        private void OnDestroy()
        {
            _addressableSource.LoadFailed -= HandleLoadFailed;

            _cts.Cancel();
            _cts.Dispose();

            for (int i = 0; i < _slotLeases.Length; i++)
            {
                _spriteSource.Release(_slotLeases[i]);
                _slotLeases[i] = default;
            }
        }

        /// <summary>Equips the first tier-1 part of every slot, so the screen opens on a whole robot.</summary>
        private void ComposeStartingRobot()
        {
            _statsPanel.Render(_loadout.AggregateStats());

            foreach (PartSlot slot in PartSlots.All)
            {
                List<PartDefinition> parts = _catalog.ForSlot(slot);
                if (parts.Count == 0)
                {
                    Debug.LogWarning($"{nameof(WorkshopController)}.{nameof(ComposeStartingRobot)} catalog has no parts for {slot}");
                    continue;
                }

                Equip(slot, parts[0]);
            }
        }

        public void Equip(PartSlot slot, PartDefinition part)
        {
            _loadout.Equip(slot, part);              // throws if slot and part.Slot disagree
            _statsPanel.Render(_loadout.AggregateStats());
            _slotButtonLabels[(int)slot].text = part.DisplayName;
            _rig.SetSlotLoading(slot, true);         // in-progress signal; the old art stays visible under it
            _ = EquipArtAsync(slot, part);           // no UniTask in this project, so no .Forget()
        }

        private async Task EquipArtAsync(PartSlot slot, PartDefinition part)
        {
            int index = (int)slot;
            int generation = ++_slotGenerations[index];   // supersedes this slot only, never another
            SpriteLease lease = default;
            try
            {
                lease = await _spriteSource.LoadAsync(part.FullAddress, _cts.Token);

                if (generation != _slotGenerations[index] || _cts.IsCancellationRequested || !this)
                {
                    return;                                // superseded; the finally releases it
                }

                if (!lease.HasSprite)
                {
                    // The part IS equipped — only its art failed. Show the placeholder rather than the
                    // previous part's art, so the rig cannot disagree with the stats panel.
                    ApplySlotSprite(slot, _placeholder);
                    _spriteSource.Release(_slotLeases[index]);
                    _slotLeases[index] = default;
                    return;
                }

                ApplySlotSprite(slot, lease.Sprite);       // apply new first — the robot stays visible
                _spriteSource.Release(_slotLeases[index]); // then release the previous
                _slotLeases[index] = lease;
                lease = default;                           // ownership transferred; the finally must not release it
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(WorkshopController)}.{nameof(EquipArtAsync)} slot={slot} part={part.Id}: {ex}");
            }
            finally
            {
                _spriteSource.Release(lease);              // releases only a lease that never became _slotLeases[i]
                if (this && generation == _slotGenerations[index])
                {
                    _rig.SetSlotLoading(slot, false);
                }
            }
        }

        private void ApplySlotSprite(PartSlot slot, Sprite sprite)
        {
            _rig.SetSlotSprite(slot, sprite);
            _slotButtonIcons[(int)slot].sprite = sprite;
        }

        private void OpenPicker(PartSlot slot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _lastPickerSlot = slot;
#endif
            PartDefinition equipped = _loadout.Get(slot);
            List<PartDefinition> parts = _catalog.ForSlot(slot);
            int multiplier = Mathf.Max(1, _stressMultiplier);
            if (multiplier > 1)
            {
                parts = StressCatalogFactory.Expand(parts, multiplier);
            }

            _picker.Open(
                slot,
                parts,
                _spriteSource,
                _cts.Token,
                equipped != null ? equipped.Id : null,
                part => Equip(slot, part));
        }

        private void HandleLoadFailed(string address, string reason)
        {
            if (_diagnostics != null)
            {
                _diagnostics.ReportFailure(address, reason);
            }
        }

        private void LoadTapGame()
        {
            SceneManager.LoadScene(TapGameSceneName);
        }
    }
}
