using UnityEngine;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The assembled robot: five fixed rects whose sprites are swapped on equip. Arms and legs are
    /// each ONE sprite holding a mirrored pair, so no per-limb attachment points are needed.
    /// Both arrays are indexed by (int)PartSlot.
    /// </summary>
    public class RobotRigView : MonoBehaviour
    {
        [SerializeField] private Image[] _slotImages = new Image[PartSlotArrayLength];
        [SerializeField] private GameObject[] _slotLoadingOverlays = new GameObject[PartSlotArrayLength];

        // PartSlots.Count is computed at type load, which a field initializer cannot use for an array
        // size the Inspector must show. OnValidate keeps the two in step.
        private const int PartSlotArrayLength = 5;

        public void SetSlotSprite(PartSlot slot, Sprite sprite)
        {
            _slotImages[(int)slot].sprite = sprite;
        }

        /// <summary>
        /// The in-progress signal. The previously equipped art stays visible underneath, so a slow
        /// load never blanks the robot.
        /// </summary>
        public void SetSlotLoading(PartSlot slot, bool loading)
        {
            _slotLoadingOverlays[(int)slot].SetActive(loading);
        }

        private void OnValidate()
        {
            if (_slotImages.Length != PartSlots.Count || _slotLoadingOverlays.Length != PartSlots.Count)
            {
                Debug.LogWarning($"{nameof(RobotRigView)}.{nameof(OnValidate)} expects {PartSlots.Count} entries per array, " +
                                 $"got images={_slotImages.Length} overlays={_slotLoadingOverlays.Length}");
            }
        }
    }
}
