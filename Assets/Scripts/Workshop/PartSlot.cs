using System;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The five places a part can be equipped on the robot. Values are contiguous from zero so a
    /// slot can index a per-slot array directly.
    /// </summary>
    public enum PartSlot
    {
        Head = 0,
        Torso = 1,
        Arms = 2,
        Legs = 3,
        Weapon = 4
    }

    public static class PartSlots
    {
        public static readonly PartSlot[] All = (PartSlot[])Enum.GetValues(typeof(PartSlot));

        /// <summary>Length of every per-slot array in the workshop.</summary>
        public static readonly int Count = All.Length;
    }
}
