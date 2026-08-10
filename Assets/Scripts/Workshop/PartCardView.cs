using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// One cell of the virtualized part grid. Pooled: the same instance is rebound to a different part
    /// as the user scrolls, which is what makes its icon load cancellable.
    /// </summary>
    public class PartCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _rarityFrame;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _powerText;
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _selectedIndicator;
        [SerializeField] private Sprite _placeholder;
        [SerializeField] private Color[] _rarityColors = new Color[5];

        private ISpriteSource _source;
        private SpriteLease _lease;
        private int _generation;
        private Action<PartDefinition> _onClicked;

        public PartDefinition Part { get; private set; }

        public void SetClickHandler(Action<PartDefinition> onClicked)
        {
            _onClicked = onClicked;
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        public void Bind(PartDefinition part, ISpriteSource source, CancellationToken ct)
        {
            _source = source;
            _generation++;
            Part = part;

            _nameText.text = part.DisplayName;
            _powerText.text = part.Stats.Power.ToString();
            _rarityFrame.color = _rarityColors[Mathf.Clamp(part.Rarity - 1, 0, _rarityColors.Length - 1)];

            // The cell stops showing the old sprite here, so releasing now costs nothing visually and
            // bounds the STORED lease at one per cell.
            _icon.sprite = _placeholder;
            source.Release(_lease);
            _lease = default;

            _ = BindAsync(part, source, _generation, ct);
        }

        public void Unbind(ISpriteSource source)
        {
            _source = source;
            _generation++;
            Part = null;
            _icon.sprite = _placeholder;
            source.Release(_lease);
            _lease = default;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            _selectedIndicator.SetActive(selected);
        }

        private async Task BindAsync(PartDefinition part, ISpriteSource source, int generation, CancellationToken ct)
        {
            SpriteLease lease = default;
            try
            {
                lease = await source.LoadAsync(part.IconAddress, ct);

                // The cell can be destroyed mid-load, and OnDestroy ordering between the cell and the
                // controller is undefined, so the master token may not have fired yet.
                if (generation != _generation || ct.IsCancellationRequested || !this)
                {
                    return;
                }

                if (!lease.HasSprite)
                {
                    _icon.sprite = _placeholder;
                    return;
                }

                _icon.sprite = lease.Sprite;
                _lease = lease;
                lease = default;                 // ownership transferred to the cell
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(PartCardView)}.{nameof(BindAsync)} part={part.Id}: {ex}");
            }
            finally
            {
                source.Release(lease);           // releases only what never became _lease
            }
        }

        private void HandleClick()
        {
            if (Part == null || _onClicked == null)
            {
                return;
            }

            _onClicked(Part);
        }

        private void OnDestroy()
        {
            if (_source != null)
            {
                _source.Release(_lease);
            }

            _lease = default;
        }
    }
}
