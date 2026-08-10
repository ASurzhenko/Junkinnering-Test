using System;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// What is equipped, and nothing else — no Unity types, no art, no async. In-memory only:
    /// the loadout is not persisted.
    /// </summary>
    public class RobotLoadout
    {
        private readonly PartDefinition[] _equipped = new PartDefinition[PartSlots.Count];

        /// <summary>The part in <paramref name="slot"/>, or null when the slot is empty.</summary>
        public PartDefinition Get(PartSlot slot)
        {
            return _equipped[(int)slot];
        }

        /// <summary>
        /// Puts <paramref name="part"/> in <paramref name="slot"/>, replacing whatever was there.
        /// Throws when the part does not belong in that slot: the picker is opened filtered to a
        /// single slot, so a mismatch is a caller bug, not a state the UI can legitimately reach.
        /// </summary>
        public void Equip(PartSlot slot, PartDefinition part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            if (part.Slot != slot)
            {
                throw new ArgumentException(
                    $"Part '{part.Id}' belongs in {part.Slot} and cannot be equipped into {slot}.",
                    nameof(part));
            }

            _equipped[(int)slot] = part;
        }

        public void Unequip(PartSlot slot)
        {
            _equipped[(int)slot] = null;
        }

        /// <summary>Sum of every equipped part's stats. An empty loadout aggregates to zero.</summary>
        public PartStats AggregateStats()
        {
            PartStats total = default;
            for (int i = 0; i < _equipped.Length; i++)
            {
                if (_equipped[i] != null)
                {
                    total += _equipped[i].Stats;
                }
            }

            return total;
        }
    }
}
