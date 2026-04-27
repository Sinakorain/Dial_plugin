// Copyright (c) 2026 Danil Kashulin. All rights reserved.

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewDial.DialogueEditor
{
    internal sealed class DialogueCanvasInputController
    {
        private const float KeyboardPanSpeed = 900f;
        private const float MaxKeyboardPanDeltaTime = 0.05f;

        private readonly DialogueGraphView _view;
        private readonly IVisualElementScheduledItem _keyboardPanTick;

        private bool _hasCanvasFocus;
        private bool _moveUpPressed;
        private bool _moveDownPressed;
        private bool _moveLeftPressed;
        private bool _moveRightPressed;
        private double _lastPanTickTime;

        public DialogueCanvasInputController(DialogueGraphView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));

            _view.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _view.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
            _view.RegisterCallback<FocusInEvent>(_ => SetCanvasFocusState(true));
            _view.RegisterCallback<FocusOutEvent>(_ => SetCanvasFocusState(false));
            _view.RegisterCallback<BlurEvent>(_ => SetCanvasFocusState(false));
            _view.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                SetCanvasFocusState(false);
                ResetMovementKeys();
                PauseKeyboardPanTick();
            });
            _view.RegisterCallback<AttachToPanelEvent>(_ => UpdateKeyboardPanTickState());

            _lastPanTickTime = EditorApplication.timeSinceStartup;
            _keyboardPanTick = _view.schedule.Execute(OnKeyboardPanTick).Every(16);
            _keyboardPanTick.Pause();
        }

        public bool HasCanvasFocus => _hasCanvasFocus;

        public void FocusCanvas()
        {
            _view.Focus();
            SetCanvasFocusState(true);
        }

        public void ReleaseCanvasFocus()
        {
            SetCanvasFocusState(false);
            _view.Blur();
        }

        public void ResetInputState()
        {
            ResetMovementKeys();
        }

        public void SetMovementKeyState(KeyCode keyCode, bool isPressed)
        {
            var wasPressed = IsMovementKeyPressed(keyCode);
            switch (keyCode)
            {
                case KeyCode.W:
                    _moveUpPressed = isPressed;
                    break;
                case KeyCode.S:
                    _moveDownPressed = isPressed;
                    break;
                case KeyCode.A:
                    _moveLeftPressed = isPressed;
                    break;
                case KeyCode.D:
                    _moveRightPressed = isPressed;
                    break;
            }

            if (wasPressed == isPressed)
            {
                UpdateKeyboardPanTickState();
                return;
            }

            if (isPressed && _hasCanvasFocus)
            {
                _lastPanTickTime = EditorApplication.timeSinceStartup;
            }

            UpdateKeyboardPanTickState();
        }

        public void StepKeyboardPan(float deltaTimeSeconds)
        {
            if (!_hasCanvasFocus || deltaTimeSeconds <= 0f)
            {
                return;
            }

            var panDelta = CalculateKeyboardPanDelta(deltaTimeSeconds);
            if (panDelta == Vector2.zero)
            {
                return;
            }

            _view.ApplyKeyboardPanDelta(panDelta);
        }

        public bool TryHandlePaletteShortcut(DialoguePaletteShortcut shortcut)
        {
            if (!_hasCanvasFocus)
            {
                return false;
            }

            var itemType = _view.PaletteShortcutResolver?.Invoke(shortcut);
            if (itemType == null)
            {
                return false;
            }

            _view.PaletteShortcutAction?.Invoke(itemType.Value);
            return true;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var movementKey = GetMovementKey(evt);
            var isMovementKey = movementKey != KeyCode.None;
            if (!_hasCanvasFocus)
            {
                if (isMovementKey)
                {
                    SetMovementKeyState(movementKey, false);
                }

                return;
            }

            if (DialogueGraphView.IsInlineInteractiveTarget(evt.target as VisualElement))
            {
                ResetMovementKeys();
                return;
            }

            if (IsAltModifierKey(evt.keyCode))
            {
                ResetMovementKeys();
                ConsumeKeyboardEvent(evt);
                return;
            }

            if (!evt.altKey && !evt.shiftKey && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
            {
                if (_view.DeleteSelectionFromHotkey())
                {
                    ConsumeKeyboardEvent(evt);
                }

                return;
            }

            if (isMovementKey && HasKeyboardPanModifier(evt))
            {
                SetMovementKeyState(movementKey, false);
            }

            if (evt.actionKey)
            {
                var handled = evt.keyCode switch
                {
                    KeyCode.C => _view.CopySelectionToClipboard(),
                    KeyCode.X => _view.CutSelectionToClipboard(),
                    KeyCode.V => _view.PasteClipboard(),
                    KeyCode.Y => DialogueGraphView.PerformRedo(),
                    KeyCode.Z => evt.shiftKey ? DialogueGraphView.PerformRedo() : DialogueGraphView.PerformUndo(),
                    _ => false
                };

                if (handled)
                {
                    ConsumeKeyboardEvent(evt);
                    return;
                }
            }

            if (TryHandlePaletteShortcut(DialoguePaletteShortcut.FromEvent(evt)))
            {
                ConsumeKeyboardEvent(evt);
                return;
            }

            if (isMovementKey && HasKeyboardPanModifier(evt))
            {
                ConsumeKeyboardEvent(evt);
                return;
            }

            if (evt.actionKey || evt.altKey || evt.shiftKey)
            {
                return;
            }

            if (isMovementKey)
            {
                SetMovementKeyState(movementKey, true);
                ConsumeKeyboardEvent(evt);
            }
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (_hasCanvasFocus &&
                (IsAltModifierKey(evt.keyCode) || (evt.altKey && IsPaletteShortcutCandidateKey(evt.keyCode))))
            {
                ConsumeKeyboardEvent(evt);
                return;
            }

            var movementKey = GetMovementKey(evt.keyCode);
            var isMovementKey = movementKey != KeyCode.None;
            var wasMovementKeyPressed = isMovementKey && IsMovementKeyPressed(movementKey);
            if (isMovementKey)
            {
                SetMovementKeyState(movementKey, false);
                if (_hasCanvasFocus || wasMovementKeyPressed)
                {
                    ConsumeKeyboardEvent(evt);
                }

                return;
            }

            if (DialogueGraphView.IsInlineInteractiveTarget(evt.target as VisualElement))
            {
                ResetMovementKeys();
            }
        }

        private void OnKeyboardPanTick()
        {
            if (_view.panel == null || !_hasCanvasFocus)
            {
                ResetMovementKeys();
                PauseKeyboardPanTick();
                return;
            }

            if (!HasMovementKeyPressed())
            {
                PauseKeyboardPanTick();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Max(0f, (float)(now - _lastPanTickTime));
            _lastPanTickTime = now;
            StepKeyboardPan(deltaTime);
        }

        private void SetCanvasFocusState(bool focused)
        {
            if (_hasCanvasFocus == focused)
            {
                if (!focused)
                {
                    ResetMovementKeys();
                    PauseKeyboardPanTick();
                }
                else
                {
                    UpdateKeyboardPanTickState();
                }

                return;
            }

            _hasCanvasFocus = focused;
            if (!focused)
            {
                ResetMovementKeys();
            }
            else
            {
                _lastPanTickTime = EditorApplication.timeSinceStartup;
            }

            UpdateKeyboardPanTickState();
            _view.SetCanvasFocusVisualState(focused);
        }

        private Vector2 CalculateKeyboardPanDelta(float deltaTimeSeconds)
        {
            var input = GetKeyboardPanInput();
            if (input == Vector2.zero)
            {
                return Vector2.zero;
            }

            var clampedDeltaTime = Mathf.Min(deltaTimeSeconds, MaxKeyboardPanDeltaTime);
            return input.normalized * (KeyboardPanSpeed * clampedDeltaTime);
        }

        private Vector2 GetKeyboardPanInput()
        {
            var input = Vector2.zero;
            if (_moveUpPressed)
            {
                input.y += 1f;
            }

            if (_moveDownPressed)
            {
                input.y -= 1f;
            }

            if (_moveLeftPressed)
            {
                input.x += 1f;
            }

            if (_moveRightPressed)
            {
                input.x -= 1f;
            }

            return input;
        }

        private void ResetMovementKeys()
        {
            _moveUpPressed = false;
            _moveDownPressed = false;
            _moveLeftPressed = false;
            _moveRightPressed = false;
            UpdateKeyboardPanTickState();
        }

        private bool HasMovementKeyPressed()
        {
            return _moveUpPressed || _moveDownPressed || _moveLeftPressed || _moveRightPressed;
        }

        private bool IsMovementKeyPressed(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.W => _moveUpPressed,
                KeyCode.S => _moveDownPressed,
                KeyCode.A => _moveLeftPressed,
                KeyCode.D => _moveRightPressed,
                _ => false
            };
        }

        private void UpdateKeyboardPanTickState()
        {
            if (_keyboardPanTick == null)
            {
                return;
            }

            if (_hasCanvasFocus && HasMovementKeyPressed() && _view.panel != null)
            {
                _keyboardPanTick.Resume();
                return;
            }

            PauseKeyboardPanTick();
        }

        private void PauseKeyboardPanTick()
        {
            _keyboardPanTick?.Pause();
        }

        private static bool HasKeyboardPanModifier(KeyDownEvent evt)
        {
            return evt.actionKey || evt.ctrlKey || evt.altKey || evt.shiftKey;
        }

        private static void ConsumeKeyboardEvent(EventBase evt)
        {
            evt.StopPropagation();
            evt.StopImmediatePropagation();
#pragma warning disable 618
            evt.PreventDefault();
#pragma warning restore 618
            evt.imguiEvent?.Use();
        }

        private static KeyCode GetMovementKey(KeyDownEvent evt)
        {
            var keyCode = GetMovementKey(evt.keyCode);
            if (keyCode != KeyCode.None)
            {
                return keyCode;
            }

            return char.ToLowerInvariant(evt.character) switch
            {
                'w' => KeyCode.W,
                'a' => KeyCode.A,
                's' => KeyCode.S,
                'd' => KeyCode.D,
                _ => KeyCode.None
            };
        }

        private static KeyCode GetMovementKey(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.W => KeyCode.W,
                KeyCode.A => KeyCode.A,
                KeyCode.S => KeyCode.S,
                KeyCode.D => KeyCode.D,
                _ => KeyCode.None
            };
        }

        private static bool IsAltModifierKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.LeftAlt || keyCode == KeyCode.RightAlt;
        }

        private static bool IsPaletteShortcutCandidateKey(KeyCode keyCode)
        {
            return keyCode is >= KeyCode.Alpha0 and <= KeyCode.Alpha9 ||
                   keyCode is >= KeyCode.Keypad0 and <= KeyCode.Keypad9 ||
                   keyCode is >= KeyCode.A and <= KeyCode.Z;
        }
    }
}
