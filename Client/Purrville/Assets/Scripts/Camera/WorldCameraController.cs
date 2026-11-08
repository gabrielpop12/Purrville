using UnityEngine;

namespace Purrville.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float movementSpeed = 15f;
        [SerializeField, Min(.1f)] private float zoomStep = 2f;
        [SerializeField] private float minimumZoom = 4f;
        [SerializeField] private float maximumZoom = 30f;
        private Camera worldCamera;

        private void Awake()
        {
            worldCamera = GetComponent<Camera>();
            worldCamera.orthographic = true;
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                        - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                transform.position += new Vector3(x, y, 0).normalized * movementSpeed * Time.unscaledDeltaTime;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f)
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - Mathf.Sign(scroll) * zoomStep,
                    minimumZoom, maximumZoom);
        }
    }
}
