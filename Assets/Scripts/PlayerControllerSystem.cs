using Unity.Entities;
using UnityEngine;
using Utils;

public class PlayerControllerSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.WithAll<PlayerControllerComponent>().ForEach((
            Entity entity,
            ref CameraFollowComponent camera,
            ref CharacterControllerComponent controller) =>
        {
            ProcessMovement(ref controller, ref camera);
        });
    }

    private static void ProcessMovement(ref CharacterControllerComponent controller, ref CameraFollowComponent camera)
    {
        var movementX = (Input.GetAxis("Move Right") > 0.0f ? 1.0f : 0.0f) +
                        (Input.GetAxis("Move Left") > 0.0f ? -1.0f : 0.0f);
        
        var movementZ = (Input.GetAxis("Move Forward") > 0.0f ? 1.0f : 0.0f) +
                        (Input.GetAxis("Move Backward") > 0.0f ? -1.0f : 0.0f);

        var forward = new Vector3(camera.Forward.x, 0.0f, camera.Forward.z).normalized;
        var right = new Vector3(camera.Right.x, 0.0f, camera.Right.z).normalized;

        if (!MathUtils.IsZero(movementX) || !MathUtils.IsZero(movementZ))
        {
            controller.CurrentDirection = (forward * movementZ + right * movementX).normalized;
            controller.CurrentMagnitude = Input.GetKey(KeyCode.LeftShift) ? 1.5f : 1.0f;
        }
        else
        {
            controller.CurrentMagnitude = 0.0f;
        }

        controller.Jump = Input.GetAxis("Jump") > 0.0f;
    }
}