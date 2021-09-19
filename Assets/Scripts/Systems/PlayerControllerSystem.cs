using Components;
using Unity.Entities;
using UnityEngine;
using Utils;

namespace Systems
{
    public class PlayerControllerSystem : ComponentSystem
    {
        private PlayerInputActions _playerInputActions;
        protected override void OnCreate()
        {
            base.OnCreate();
            _playerInputActions = new PlayerInputActions();
            _playerInputActions.Enable();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAll<PlayerControllerComponent>()
                .ForEach((
                    Entity entity,
                    ref CameraFollowComponent camera,
                    ref CharacterControllerComponent controller) =>
                {
                    ProcessMovement(ref controller, ref camera);
                });
        }

        private void ProcessMovement(ref CharacterControllerComponent controller, ref CameraFollowComponent camera)
        {
            var movement= _playerInputActions.Player.Move.ReadValue<Vector2>();

            var forward = new Vector3(camera.Forward.x, 0.0f, camera.Forward.z).normalized;
            var right = new Vector3(camera.Right.x, 0.0f, camera.Right.z).normalized;

            if (!MathUtils.IsZero(movement.x) || !MathUtils.IsZero(movement.y))
            {
                controller.CurrentDirection = (forward * movement.y + right * movement.x).normalized;
                controller.CurrentMagnitude =  _playerInputActions.Player.Run.triggered ? 1.5f : 1.0f;
            }
            else
            {
                controller.CurrentMagnitude = 0.0f;
            }

            controller.Jump = _playerInputActions.Player.Jump.triggered;
        }
    }
}