using System;
using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// One catalog entry. Holds Addressable ADDRESS STRINGS, never Sprite references: a direct
    /// reference would make every part texture a build dependency of the catalog's bundle, so loading
    /// the catalog would pull the whole art set into memory — the opposite of the bounded-window
    /// streaming this screen exists to demonstrate.
    /// </summary>
    [Serializable]
    public class PartDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private PartSlot _slot;
        [SerializeField, Range(1, 5)] private int _rarity = 1;
        [SerializeField] private PartStats _stats;
        [SerializeField] private string _iconAddress;
        [SerializeField] private string _fullAddress;

        /// <summary>Required by Unity serialization.</summary>
        public PartDefinition()
        {
        }

        public PartDefinition(
            string id,
            string displayName,
            PartSlot slot,
            int rarity,
            PartStats stats,
            string iconAddress,
            string fullAddress)
        {
            _id = id;
            _displayName = displayName;
            _slot = slot;
            _rarity = rarity;
            _stats = stats;
            _iconAddress = iconAddress;
            _fullAddress = fullAddress;
        }

        public string Id => _id;
        public string DisplayName => _displayName;
        public PartSlot Slot => _slot;

        /// <summary>1..5. Higher tiers scale the same base art's stats; the art is shared.</summary>
        public int Rarity => _rarity;

        public PartStats Stats => _stats;

        /// <summary>128 px grid icon.</summary>
        public string IconAddress => _iconAddress;

        /// <summary>512 px art applied when the part is equipped.</summary>
        public string FullAddress => _fullAddress;
    }
}
