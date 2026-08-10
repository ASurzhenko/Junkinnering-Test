using System;
using Junkinnering.Workshop;
using NUnit.Framework;

namespace Junkinnering.Tests
{
    public class RobotLoadoutTests
    {
        private static PartDefinition Part(string id, PartSlot slot, int attack, int health, int speed, int defense)
        {
            return new PartDefinition(
                id,
                id,
                slot,
                rarity: 1,
                new PartStats(attack, health, speed, defense),
                $"{id}.icon",
                $"{id}.full");
        }

        [Test]
        public void EmptyLoadout_AggregatesToZero()
        {
            PartStats total = new RobotLoadout().AggregateStats();

            Assert.AreEqual(0, total.Attack);
            Assert.AreEqual(0, total.Health);
            Assert.AreEqual(0, total.Speed);
            Assert.AreEqual(0, total.Defense);
            Assert.AreEqual(0, total.Power);
        }

        [Test]
        public void Aggregate_IsTheSumOfEveryEquippedPart()
        {
            RobotLoadout loadout = new RobotLoadout();
            loadout.Equip(PartSlot.Head, Part("head_a", PartSlot.Head, 1, 10, 2, 3));
            loadout.Equip(PartSlot.Torso, Part("torso_a", PartSlot.Torso, 4, 40, 1, 6));
            loadout.Equip(PartSlot.Weapon, Part("weapon_a", PartSlot.Weapon, 9, 0, 0, 1));

            PartStats total = loadout.AggregateStats();

            Assert.AreEqual(14, total.Attack);
            Assert.AreEqual(50, total.Health);
            Assert.AreEqual(3, total.Speed);
            Assert.AreEqual(10, total.Defense);
        }

        [Test]
        public void EquippingIntoAnOccupiedSlot_ReplacesRatherThanStacks()
        {
            RobotLoadout loadout = new RobotLoadout();
            loadout.Equip(PartSlot.Legs, Part("legs_a", PartSlot.Legs, 0, 20, 5, 2));
            loadout.Equip(PartSlot.Legs, Part("legs_b", PartSlot.Legs, 0, 30, 1, 4));

            PartStats total = loadout.AggregateStats();

            Assert.AreEqual("legs_b", loadout.Get(PartSlot.Legs).Id);
            Assert.AreEqual(30, total.Health, "The replaced part must not still contribute.");
            Assert.AreEqual(1, total.Speed);
            Assert.AreEqual(4, total.Defense);
        }

        [Test]
        public void Equip_ThrowsWhenTheSlotAndThePartDisagree()
        {
            RobotLoadout loadout = new RobotLoadout();

            Assert.Throws<ArgumentException>(
                () => loadout.Equip(PartSlot.Head, Part("weapon_a", PartSlot.Weapon, 9, 0, 0, 1)));
        }

        [Test]
        public void Equip_LeavesTheSlotUntouchedWhenItThrows()
        {
            RobotLoadout loadout = new RobotLoadout();
            loadout.Equip(PartSlot.Head, Part("head_a", PartSlot.Head, 1, 10, 2, 3));

            Assert.Throws<ArgumentException>(
                () => loadout.Equip(PartSlot.Head, Part("torso_a", PartSlot.Torso, 4, 40, 1, 6)));

            Assert.AreEqual("head_a", loadout.Get(PartSlot.Head).Id);
        }

        [Test]
        public void Unequip_RemovesThePartFromTheAggregate()
        {
            RobotLoadout loadout = new RobotLoadout();
            loadout.Equip(PartSlot.Arms, Part("arms_a", PartSlot.Arms, 3, 12, 1, 1));
            loadout.Unequip(PartSlot.Arms);

            Assert.IsNull(loadout.Get(PartSlot.Arms));
            Assert.AreEqual(0, loadout.AggregateStats().Attack);
        }

        [Test]
        public void Power_IsTheWeightedSumOfTheAggregate()
        {
            RobotLoadout loadout = new RobotLoadout();
            loadout.Equip(PartSlot.Head, Part("head_a", PartSlot.Head, 5, 10, 3, 2));

            PartStats total = loadout.AggregateStats();

            Assert.AreEqual(total.Power, new PartStats(5, 10, 3, 2).Power);
            Assert.Greater(total.Power, 0);
        }
    }
}
