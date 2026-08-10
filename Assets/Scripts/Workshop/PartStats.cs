using System;
using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The combat stats a single part contributes. Parts aggregate by summation, so
    /// <c>default(PartStats)</c> is the identity element and an empty loadout aggregates to zero.
    /// </summary>
    [Serializable]
    public struct PartStats
    {
        // Power is a single display number derived from the four stats. Health moves in much larger
        // steps than the other three, so it carries the smallest weight.
        private static readonly int AttackWeight = 2;
        private static readonly int DefenseWeight = 2;
        private static readonly int SpeedWeight = 2;
        private static readonly int HealthWeight = 1;

        [SerializeField] private int _attack;
        [SerializeField] private int _health;
        [SerializeField] private int _speed;
        [SerializeField] private int _defense;

        public PartStats(int attack, int health, int speed, int defense)
        {
            _attack = attack;
            _health = health;
            _speed = speed;
            _defense = defense;
        }

        public int Attack => _attack;
        public int Health => _health;
        public int Speed => _speed;
        public int Defense => _defense;

        public int Power => (_attack * AttackWeight)
                          + (_defense * DefenseWeight)
                          + (_speed * SpeedWeight)
                          + (_health * HealthWeight);

        public static PartStats operator +(PartStats a, PartStats b)
        {
            return new PartStats(
                a._attack + b._attack,
                a._health + b._health,
                a._speed + b._speed,
                a._defense + b._defense);
        }

        public static PartStats Scaled(PartStats basis, float factor)
        {
            return new PartStats(
                Mathf.RoundToInt(basis._attack * factor),
                Mathf.RoundToInt(basis._health * factor),
                Mathf.RoundToInt(basis._speed * factor),
                Mathf.RoundToInt(basis._defense * factor));
        }

        public override string ToString()
        {
            return $"ATK {_attack} / HP {_health} / SPD {_speed} / DEF {_defense}";
        }
    }
}
