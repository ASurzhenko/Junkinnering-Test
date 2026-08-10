using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Junkinnering.Workshop
{
    /// <summary>
    /// Loads a scene by name when its button is pressed. Subscribing in code rather than through a
    /// serialized UnityEvent keeps the wiring visible in the file that owns it and survives a rename.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SceneSwitchButton : MonoBehaviour
    {
        [SerializeField] private string _sceneName;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(LoadScene);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(LoadScene);
        }

        private void LoadScene()
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                Debug.LogWarning($"{nameof(SceneSwitchButton)}.{nameof(LoadScene)} no scene name set on {name}");
                return;
            }

            SceneManager.LoadScene(_sceneName);
        }
    }
}
