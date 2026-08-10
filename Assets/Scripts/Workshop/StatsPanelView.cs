using TMPro;
using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The right-hand readout of the aggregated loadout. Only the stats section of the reference
    /// panel is built; the element-bonus row and skills grid are absent rather than faked.
    /// </summary>
    public class StatsPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _attackValue;
        [SerializeField] private TMP_Text _healthValue;
        [SerializeField] private TMP_Text _speedValue;
        [SerializeField] private TMP_Text _defenseValue;
        [SerializeField] private TMP_Text _powerValue;

        // The rows are horizontal layout groups sized by their text, so each assignment needs an
        // immediate rebuild against the container, not the label.
        [SerializeField] private RectTransform _attackRow;
        [SerializeField] private RectTransform _healthRow;
        [SerializeField] private RectTransform _speedRow;
        [SerializeField] private RectTransform _defenseRow;
        [SerializeField] private RectTransform _powerRow;

        public void Render(PartStats stats)
        {
            TextLayout.Set(_attackValue, stats.Attack.ToString(), _attackRow);
            TextLayout.Set(_healthValue, stats.Health.ToString(), _healthRow);
            TextLayout.Set(_speedValue, stats.Speed.ToString(), _speedRow);
            TextLayout.Set(_defenseValue, stats.Defense.ToString(), _defenseRow);
            TextLayout.Set(_powerValue, stats.Power.ToString(), _powerRow);
        }
    }
}
