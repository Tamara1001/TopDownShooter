using UnityEngine;
using UnityEngine.UI;

namespace TopDownShooter.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _continueButton;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                _continueButton.interactable = GameManager.Instance.HasActiveSession;
            }
            else
            {
                Debug.LogWarning("GameManager.Instance is null in MainMenuController.Start(). Disabling continue button as fallback.");
                if (_continueButton != null)
                {
                    _continueButton.interactable = false;
                }
            }
        }

        public void OnPlayClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartNewGame();
            }
            else
            {
                Debug.LogError("Failed to StartNewGame: GameManager is missing!");
            }
        }

        public void OnContinueClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ContinueGame();
            }
            else
            {
                Debug.LogError("Failed to ContinueGame: GameManager is missing!");
            }
        }

        public void OnBackClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenu();
            }
            else
            {
                Debug.LogError("Failed to ReturnToMainMenu: GameManager is missing!");
            }
        }

        public void OnQuitClicked()
        {
            Debug.Log("OnQuitClicked: Exiting application.");
            Application.Quit();
        }
    }
}
