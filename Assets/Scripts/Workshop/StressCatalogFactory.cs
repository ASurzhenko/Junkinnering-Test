using System.Collections.Generic;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Multiplies a slot's entries so the grid can be driven far past the real catalog. Addressables
    /// refcounts per key, so N entries over the same address give ONE texture with refcount N: this
    /// harness proves handle-count boundedness and recycle correctness, NOT texture-memory scaling.
    /// The memory claim is measured separately.
    /// </summary>
    public static class StressCatalogFactory
    {
        public static List<PartDefinition> Expand(IReadOnlyList<PartDefinition> parts, int multiplier)
        {
            List<PartDefinition> result = new List<PartDefinition>(parts.Count * multiplier);
            for (int copy = 0; copy < multiplier; copy++)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    PartDefinition source = parts[i];
                    if (copy == 0)
                    {
                        result.Add(source);
                        continue;
                    }

                    result.Add(new PartDefinition(
                        $"{source.Id}_s{copy}",
                        source.DisplayName,
                        source.Slot,
                        source.Rarity,
                        source.Stats,
                        source.IconAddress,
                        source.FullAddress));
                }
            }

            return result;
        }
    }
}
