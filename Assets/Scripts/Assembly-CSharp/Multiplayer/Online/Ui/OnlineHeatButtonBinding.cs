using Michsky.UI.Heat;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online.Ui
{
    /// <summary>Keeps native Heat transitions aligned with the lobby's uGUI action bindings.</summary>
    public sealed class OnlineHeatButtonBinding : MonoBehaviour
    {
        private UnityEngine.UI.Button button;
        private ButtonManager manager;
        private PanelButton panel;

        public void Initialize(UnityEngine.UI.Button target, ButtonManager heat)
        {
            button = target;
            manager = heat;
        }

        public void InitializePanel(UnityEngine.UI.Button target, PanelButton heat)
        {
            button = target;
            panel = heat;
        }

        private void LateUpdate()
        {
            if (button == null) return;
            if (panel != null && panel.isInteractable != button.interactable) panel.IsInteractable(button.interactable);
            if (manager == null) return;
            if (manager.isInteractable != button.interactable)
                manager.Interactable(button.interactable);
            // Existing room controls update the normal caption after ready-state changes.
            if (manager.normalTextObj != null && manager.buttonText != manager.normalTextObj.text)
                manager.SetText(manager.normalTextObj.text);
        }
    }
}
