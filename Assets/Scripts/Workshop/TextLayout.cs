using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    public static class TextLayout
    {
        /// <summary>
        /// Assigning .text only MARKS layout dirty, so a container sized by the label lays out against
        /// the old width and the row visibly overlaps until something else triggers a rebuild.
        /// </summary>
        public static void Set(TMP_Text label, string value, RectTransform container)
        {
            label.text = value;
            label.ForceMeshUpdate();
            if (container != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            }
        }
    }
}
