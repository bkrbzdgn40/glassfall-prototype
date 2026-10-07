using UnityEngine;
using UnityEngine.InputSystem;

namespace Glassfall.Input
{
    public class UnityInputAdapter : MonoBehaviour
    {
        private Vector2 _moveDirection;
        private bool _resetQueued;
        private bool _splitQueued;

        private void Update()
        {
            Vector2 move = Vector2.zero;

            if (Keyboard.current == null)
                return;

            if (Keyboard.current.wKey.isPressed)
                move.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                move.y -= 1f;

            if (Keyboard.current.aKey.isPressed)
                move.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                move.x += 1f;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            _moveDirection = move;

            // Tek-frame input'u FixedUpdate kaçırmasın diye kuyruğa al.
            if (Keyboard.current.rKey.wasPressedThisFrame)
                _resetQueued = true;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                _splitQueued = true;
        }

        public PlayerCommand ConsumeCommand()
        {
            PlayerCommand command = new PlayerCommand
            {
                MoveDirection = _moveDirection,
                ResetPressed = _resetQueued,
                SplitPressed = _splitQueued
            };

            _resetQueued = false;
            _splitQueued = false;

            return command;
        }

        public void ClearQueuedCommands()
        {
            _resetQueued = false;
            _splitQueued = false;
        }
    }
}
