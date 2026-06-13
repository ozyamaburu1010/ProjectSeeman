using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Seeman
{
    /// <summary>Click on the tank glass -> the fish comes to the front of the camera.</summary>
    public class CameraInteraction : MonoBehaviour
    {
        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var g = GameManager.I;
            if (g == null) return;
            if (g.Fish != null) g.Fish.Summon();
            if (g.CRT != null) g.CRT.Tap();
        }
    }
}
