using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Utils;

/// <summary>
/// Basic system which follows the entity with the <see cref="CameraFollowComponent"/>.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(ExportPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
public sealed class CameraFollowSystem : ComponentSystem
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
        Entities.ForEach((
            Entity entity,
            ref Translation position,
            ref Rotation rotation,
            ref CameraFollowComponent camera) =>
        {
            ProcessCameraInput(ref camera);

            var transform = Camera.main.transform;
            var currPos = transform.position;
            var targetPos = new Vector3(position.Value.x, position.Value.y + 1.0f, position.Value.z);

            targetPos += (transform.forward * -camera.Zoom);
            var posLerp = Mathf.Clamp(Time.DeltaTime * 8.0f, 0.0f, 1.0f);

            Camera.main.transform.rotation = new Quaternion();
            Camera.main.transform.Rotate(new Vector3(camera.Pitch, camera.Yaw, 0.0f));
            Camera.main.transform.position = Vector3.Lerp(currPos, targetPos, posLerp);

            camera.Forward = transform.forward;
            camera.Right = transform.right;
        });
    }

    /// <summary>
    /// Handles all camera related input.
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private bool ProcessCameraInput(ref CameraFollowComponent camera)
    {
        return ProcessCameraZoom(ref camera) ||
               ProcessCameraYawPitch(ref camera);
    }

    /// <summary>
    /// Handles input for zooming the camera in and out.
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private bool ProcessCameraZoom(ref CameraFollowComponent camera)
    {
        var scroll = _playerInputActions.Player.Zoom.ReadValue<float>();

        if (!MathUtils.IsZero(scroll))
        {
            camera.Zoom -= scroll;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles input for manipulating the camera yaw (rotating around).
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private bool ProcessCameraYawPitch(ref CameraFollowComponent camera)
    {
        if (!_playerInputActions.Player.Yaw.triggered)
        {
            return false;
        }

        var mouse = _playerInputActions.Player.Look.ReadValue<Vector2>();
        camera.Yaw += mouse.x;
        camera.Pitch -= mouse.y;

        return true;
    }
}