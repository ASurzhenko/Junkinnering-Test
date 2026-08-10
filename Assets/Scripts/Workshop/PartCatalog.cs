using System.Collections.Generic;
using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// The full part inventory. Small enough to hold as a serialized reference on the controller —
    /// it carries address strings only, so it pulls no art into memory when it loads.
    /// </summary>
    [CreateAssetMenu(menuName = "Junkinnering/Part Catalog", fileName = "PartCatalog")]
    public class PartCatalog : ScriptableObject
    {
        [SerializeField] private List<PartDefinition> _parts = new List<PartDefinition>();

        public IReadOnlyList<PartDefinition> Parts => _parts;

        public bool TryGet(string id, out PartDefinition part)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Id == id)
                {
                    part = _parts[i];
                    return true;
                }
            }

            part = null;
            return false;
        }

        /// <summary>
        /// Entries for one slot, in catalog order. Allocates — called when the picker opens, not per frame.
        /// </summary>
        public List<PartDefinition> ForSlot(PartSlot slot)
        {
            List<PartDefinition> result = new List<PartDefinition>();
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Slot == slot)
                {
                    result.Add(_parts[i]);
                }
            }

            return result;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring entry point used by the catalog generator.</summary>
        public void SetParts(List<PartDefinition> parts)
        {
            _parts = parts;
        }
#endif
    }
}
