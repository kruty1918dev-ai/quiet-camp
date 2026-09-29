using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietCamp
{
    /// <summary>
    /// Persistent application root created once by the Boot scene.
    /// Applies the runtime frame-rate policy and forwards to the main menu.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private string _firstSceneName = "MainMenu";

        private void Awake()
        {
            if (FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = QualitySettings.GetQualityLevel() >= 2 ? 60 : 30;
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != _firstSceneName)
                SceneManager.LoadScene(_firstSceneName);
        }
    }
}
