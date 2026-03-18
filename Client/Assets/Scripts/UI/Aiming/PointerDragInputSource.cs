using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PointerDragInputSource : MonoBehaviour
{
    public event Action<Vector2> DragStarted;
    public event Action<Vector2> DragMoved;
    public event Action<Vector2> DragEnded;

    public bool IsDragging { get; private set; }
    public Vector2 CurrentScreenPosition { get; private set; }

    private bool usingTouchInput;

    private void Update()
    {
        if (UpdateTouchInput())
        {
            return;
        }

        UpdateMouseInput();
    }

    private void UpdateMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        var screenPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginDrag(screenPosition);
            return;
        }

        if (!IsDragging)
        {
            return;
        }

        if (mouse.leftButton.isPressed)
        {
            MoveDrag(screenPosition);
            return;
        }

        EndDrag(screenPosition);
    }

    private bool UpdateTouchInput()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            usingTouchInput = false;
            return false;
        }

        var touch = touchscreen.primaryTouch;
        var screenPosition = touch.position.ReadValue();
        var touchIsActive = touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame || usingTouchInput;
        if (!touchIsActive)
        {
            usingTouchInput = false;
            return false;
        }

        if (touch.press.wasPressedThisFrame)
        {
            usingTouchInput = true;
            BeginDrag(screenPosition);
            return true;
        }

        if (touch.press.isPressed)
        {
            usingTouchInput = true;
            if (!IsDragging)
            {
                BeginDrag(screenPosition);
                return true;
            }

            MoveDrag(screenPosition);
            return true;
        }

        if (touch.press.wasReleasedThisFrame || usingTouchInput)
        {
            EndDrag(screenPosition);
            usingTouchInput = false;
            return true;
        }

        return false;
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        IsDragging = true;
        CurrentScreenPosition = screenPosition;
        DragStarted?.Invoke(CurrentScreenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        CurrentScreenPosition = screenPosition;
        DragMoved?.Invoke(CurrentScreenPosition);
    }

    private void EndDrag(Vector2 screenPosition)
    {
        CurrentScreenPosition = screenPosition;
        if (!IsDragging)
        {
            return;
        }

        IsDragging = false;
        DragEnded?.Invoke(CurrentScreenPosition);
    }
}
