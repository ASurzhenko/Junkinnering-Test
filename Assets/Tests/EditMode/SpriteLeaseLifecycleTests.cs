using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Junkinnering.Workshop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Junkinnering.Tests
{
    /// <summary>
    /// The handle-lifecycle claims, asserted against the obligation rather than the implementation:
    /// what the grid promises is that live handles are bounded by the cell pool once loads settle, that
    /// closing releases everything, and that a rebind mid-load releases the stale result exactly once.
    /// </summary>
    public class SpriteLeaseLifecycleTests
    {
        private const float CellWidth = 200f;
        private const float CellHeight = 240f;
        private const float Spacing = 16f;
        private const int Columns = 3;
        private const int BufferRows = 1;
        private const float ViewportHeight = 480f;
        private const float ViewportWidth = 800f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private FakeSpriteSource _source;

        private static readonly BindingFlags Private =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        [SetUp]
        public void SetUp()
        {
            _source = new FakeSpriteSource();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            _source.DestroyCreatedSprites();
        }

        // ---------------------------------------------------------------- grid

        [Test]
        public void QuiescentGrid_LiveHandlesStayWithinThePool()
        {
            PartPickerView picker = BuildPicker();
            List<PartDefinition> items = MakeParts(PartSlot.Head, 600);

            picker.Open(PartSlot.Head, items, _source, CancellationToken.None, null, _ => { });

            float rowStride = CellHeight + Spacing;
            for (float offset = 0f; offset < 200f * rowStride; offset += rowStride * 0.5f)
            {
                ScrollTo(picker, offset);
                Assert.LessOrEqual(
                    _source.LiveCount,
                    picker.PoolSize,
                    $"Quiescent live count exceeded the pool at offset {offset}.");
            }

            Assert.Greater(_source.LoadStartCount, picker.PoolSize, "The grid never recycled — the test proved nothing.");
        }

        [Test]
        public void Close_ReleasesEveryGridHandle()
        {
            PartPickerView picker = BuildPicker();
            picker.Open(PartSlot.Head, MakeParts(PartSlot.Head, 600), _source, CancellationToken.None, null, _ => { });
            ScrollTo(picker, 40f * (CellHeight + Spacing));
            Assert.Greater(_source.LiveCount, 0, "Nothing was bound, so the release assertion would be vacuous.");

            picker.Close();

            Assert.AreEqual(0, _source.LiveCount);
        }

        [Test]
        public void PoolIsLargeEnoughForTheWindowItMustBind()
        {
            PartPickerView picker = BuildPicker();
            picker.Open(PartSlot.Head, MakeParts(PartSlot.Head, 600), _source, CancellationToken.None, null, _ => { });

            var window = GridWindow.Compute(
                0f, ViewportHeight, CellHeight, Spacing, Columns, 600, BufferRows);

            Assert.GreaterOrEqual(picker.PoolSize, window.lastIndex - window.firstIndex + 1);
        }

        // ---------------------------------------------------------------- cell

        [Test]
        public void FailedLoad_LeavesTheLiveCountUnchangedAndShowsThePlaceholder()
        {
            PartCardView cell = BuildCell();
            PartDefinition part = MakePart(PartSlot.Head, 0);
            _source.FailNext(part.IconAddress);

            cell.Bind(part, _source, CancellationToken.None);

            Assert.AreEqual(0, _source.LiveCount);
            Assert.AreEqual(PlaceholderOf(cell), IconOf(cell).sprite);
        }

        [UnityTest]
        public IEnumerator RebindMidLoad_DiscardsTheStaleResultAndReleasesItExactlyOnce()
        {
            PartCardView cell = BuildCell();
            _source.CompleteImmediately = false;

            PartDefinition stale = MakePart(PartSlot.Head, 0);
            PartDefinition fresh = MakePart(PartSlot.Head, 1);

            cell.Bind(stale, _source, CancellationToken.None);
            cell.Bind(fresh, _source, CancellationToken.None);
            Assert.AreEqual(2, _source.LiveCount, "Both loads are in flight; Addressables cannot abort a started load.");

            _source.CompleteAll();
            yield return null;                 // the continuations are posted, not inline
            yield return null;

            Assert.AreEqual(1, _source.LiveCount, "Exactly one lease survives: a leak leaves 2, a double release leaves 0.");
            Assert.AreEqual(1, _source.ReleaseCount);
            Assert.AreEqual(fresh.IconAddress, IconOf(cell).sprite.name, "The stale continuation painted over the newer binding.");

            cell.Unbind(_source);
            Assert.AreEqual(0, _source.LiveCount);
        }

        [UnityTest]
        public IEnumerator UnbindMidLoad_ReleasesTheResultWhenItArrives()
        {
            PartCardView cell = BuildCell();
            _source.CompleteImmediately = false;

            cell.Bind(MakePart(PartSlot.Head, 0), _source, CancellationToken.None);
            cell.Unbind(_source);

            _source.CompleteAll();
            yield return null;
            yield return null;

            Assert.AreEqual(0, _source.LiveCount);
        }

        // ---------------------------------------------------------------- equip

        [UnityTest]
        public IEnumerator TwoDifferentSlotsEquippedConcurrently_BothApply()
        {
            WorkshopController controller = BuildController();
            RobotRigView rig = (RobotRigView)controller.GetType().GetField("_rig", Private).GetValue(controller);
            _source.CompleteImmediately = false;

            PartDefinition head = MakePart(PartSlot.Head, 0);
            PartDefinition torso = MakePart(PartSlot.Torso, 0);

            controller.Equip(PartSlot.Head, head);
            controller.Equip(PartSlot.Torso, torso);

            _source.CompleteAll();
            yield return null;
            yield return null;

            Assert.AreEqual(head.FullAddress, SlotSpriteName(rig, PartSlot.Head), "Equipping another slot cancelled this one.");
            Assert.AreEqual(torso.FullAddress, SlotSpriteName(rig, PartSlot.Torso));
            Assert.AreEqual(2, _source.LiveCount, "One applied lease per equipped slot.");
        }

        [UnityTest]
        public IEnumerator SameSlotEquippedTwice_LastWinsAndTheSupersededLeaseIsReleased()
        {
            WorkshopController controller = BuildController();
            RobotRigView rig = (RobotRigView)controller.GetType().GetField("_rig", Private).GetValue(controller);
            _source.CompleteImmediately = false;

            PartDefinition first = MakePart(PartSlot.Head, 0);
            PartDefinition second = MakePart(PartSlot.Head, 1);

            controller.Equip(PartSlot.Head, first);
            controller.Equip(PartSlot.Head, second);

            _source.CompleteAll();
            yield return null;
            yield return null;

            Assert.AreEqual(second.FullAddress, SlotSpriteName(rig, PartSlot.Head));
            Assert.AreEqual(1, _source.LiveCount, "The superseded load must release on its discard path.");
        }

        // ---------------------------------------------------------------- harness

        private static PartDefinition MakePart(PartSlot slot, int index)
        {
            string id = $"{slot}_{index}".ToLowerInvariant();
            return new PartDefinition(
                id,
                id,
                slot,
                (index % 5) + 1,
                new PartStats(1 + index, 10 + index, 2, 3),
                id + ".icon",
                id + ".full");
        }

        private static List<PartDefinition> MakeParts(PartSlot slot, int count)
        {
            var list = new List<PartDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(MakePart(slot, i));
            }

            return list;
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _spawned.Add(go);
            return go;
        }

        private static RectTransform Fixed(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        private PartCardView BuildCell()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Workshop/PartCard.prefab");
            Assert.IsNotNull(prefab, "PartCard.prefab is missing — the cell tests would prove nothing.");
            var instance = Object.Instantiate(prefab);
            _spawned.Add(instance);
            return instance.GetComponent<PartCardView>();
        }

        private static Image IconOf(PartCardView cell)
        {
            return (Image)cell.GetType().GetField("_icon", Private).GetValue(cell);
        }

        private static Sprite PlaceholderOf(PartCardView cell)
        {
            return (Sprite)cell.GetType().GetField("_placeholder", Private).GetValue(cell);
        }

        private static string SlotSpriteName(RobotRigView rig, PartSlot slot)
        {
            var images = (Image[])rig.GetType().GetField("_slotImages", Private).GetValue(rig);
            Sprite sprite = images[(int)slot].sprite;
            return sprite == null ? null : sprite.name;
        }

        private PartPickerView BuildPicker()
        {
            GameObject rootGo = Spawn("Picker");
            GameObject scrollGo = Spawn("Scroll");
            scrollGo.transform.SetParent(rootGo.transform, false);
            GameObject viewportGo = Spawn("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            GameObject contentGo = Spawn("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            GameObject titleGo = Spawn("Title");
            titleGo.transform.SetParent(rootGo.transform, false);

            var viewport = Fixed(viewportGo.GetComponent<RectTransform>(), ViewportWidth, ViewportHeight);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;

            var title = titleGo.AddComponent<TextMeshProUGUI>();
            var close = titleGo.AddComponent<Button>();

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Workshop/PartCard.prefab");
            Assert.IsNotNull(prefab, "PartCard.prefab is missing — the grid tests would prove nothing.");

            var picker = rootGo.AddComponent<PartPickerView>();
            Set(picker, "_root", rootGo);
            Set(picker, "_scrollRect", scroll);
            Set(picker, "_viewport", viewport);
            Set(picker, "_content", content);
            Set(picker, "_cardPrefab", prefab.GetComponent<PartCardView>());
            Set(picker, "_titleText", title);
            Set(picker, "_closeButton", close);
            Set(picker, "_cellWidth", CellWidth);
            Set(picker, "_cellHeight", CellHeight);
            Set(picker, "_spacing", Spacing);
            Set(picker, "_columns", Columns);
            Set(picker, "_bufferRows", BufferRows);
            return picker;
        }

        /// <summary>
        /// Moves the content the way ScrollRect does — anchoredPosition.y grows as the user scrolls
        /// down — then drives the same refresh its onValueChanged callback drives at runtime.
        /// </summary>
        private static void ScrollTo(PartPickerView picker, float offset)
        {
            var content = (RectTransform)picker.GetType().GetField("_content", Private).GetValue(picker);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
            picker.GetType().GetMethod("Refresh", Private).Invoke(picker, null);
        }

        private WorkshopController BuildController()
        {
            GameObject rigGo = Spawn("Rig");
            var images = new Image[PartSlots.Count];
            var overlays = new GameObject[PartSlots.Count];
            for (int i = 0; i < PartSlots.Count; i++)
            {
                GameObject slotGo = Spawn("Slot" + i);
                slotGo.transform.SetParent(rigGo.transform, false);
                images[i] = slotGo.AddComponent<Image>();
                overlays[i] = Spawn("Loading" + i);
                overlays[i].transform.SetParent(slotGo.transform, false);
            }

            var rig = rigGo.AddComponent<RobotRigView>();
            Set(rig, "_slotImages", images);
            Set(rig, "_slotLoadingOverlays", overlays);

            GameObject statsGo = Spawn("Stats");
            var values = new TMP_Text[5];
            var rows = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject rowGo = Spawn("Row" + i);
                rowGo.transform.SetParent(statsGo.transform, false);
                rows[i] = rowGo.GetComponent<RectTransform>();
                values[i] = rowGo.AddComponent<TextMeshProUGUI>();
            }

            var stats = statsGo.AddComponent<StatsPanelView>();
            string[] valueFields = { "_attackValue", "_healthValue", "_speedValue", "_defenseValue", "_powerValue" };
            string[] rowFields = { "_attackRow", "_healthRow", "_speedRow", "_defenseRow", "_powerRow" };
            for (int i = 0; i < 5; i++)
            {
                Set(stats, valueFields[i], values[i]);
                Set(stats, rowFields[i], rows[i]);
            }

            GameObject controllerGo = Spawn("Workshop");
            var controller = controllerGo.AddComponent<WorkshopController>();
            var slotIcons = new Image[PartSlots.Count];
            var slotLabels = new TMP_Text[PartSlots.Count];
            for (int i = 0; i < PartSlots.Count; i++)
            {
                GameObject cardGo = Spawn("SlotCard" + i);
                cardGo.transform.SetParent(controllerGo.transform, false);
                slotIcons[i] = cardGo.AddComponent<Image>();

                // One Graphic per GameObject: adding TMP beside an Image silently returns null.
                GameObject labelGo = Spawn("SlotLabel" + i);
                labelGo.transform.SetParent(cardGo.transform, false);
                slotLabels[i] = labelGo.AddComponent<TextMeshProUGUI>();
            }

            Set(controller, "_rig", rig);
            Set(controller, "_statsPanel", stats);
            Set(controller, "_slotButtonIcons", slotIcons);
            Set(controller, "_slotButtonLabels", slotLabels);
            Set(controller, "_spriteSource", _source);
            Set(controller, "_cts", new CancellationTokenSource());
            return controller;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, Private);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field {field}");
            info.SetValue(target, value);
        }
    }
}
