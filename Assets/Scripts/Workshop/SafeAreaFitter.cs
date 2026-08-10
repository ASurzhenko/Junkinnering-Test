using UnityEngine;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Insets a UI root to <see cref="Screen.safeArea"/>. In landscape, Android puts camera cutouts and
    /// the gesture bar exactly where the back button and the left column sit, so interactive UI lives
    /// under this and only the full-bleed background sits outside it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedSafeArea;
        private int _appliedWidth;
        private int _appliedHeight;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedSafeArea || Screen.width != _appliedWidth || Screen.height != _appliedHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;
            min.x /= width;
            min.y /= height;
            max.x /= width;
            max.y /= height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _appliedSafeArea = safeArea;
            _appliedWidth = width;
            _appliedHeight = height;
        }
    }
}
