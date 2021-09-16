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
            if (Camera.main is null)
            {
                Debug.LogError("No main camera found");
                return;
            }
            
            ProcessCameraInput(ref camera);
            var transform = Camera.main.transform;
            var currPos = transform.position;
            var targetPos = new Vector3(position.Value.x, position.Value.y + 1.0f, position.Value.z);

            targetPos += transform.forward * -camera.Zoom;
            var posLerp = Mathf.Clamp(Time.DeltaTime * 8.0f, 0.0f, 1.0f);

            transform.rotation = new Quaternion();
            transform.Rotate(new Vector3(camera.Pitch, camera.Yaw, 0.0f));
            transform.position = Vector3.Lerp(currPos, targetPos, posLerp);
            camera.Forward = transform.forward;
            camera.Right = transform.right;
        });
    }

    /// <summary>
    /// Handles all camera related input.
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private void ProcessCameraInput(ref CameraFollowComponent camera)
    {
        ProcessCameraZoom(ref camera);
        ProcessCameraYawPitch(ref camera);
    }

    /// <summary>
    /// Handles input for zooming the camera in and out.
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private void ProcessCameraZoom(ref CameraFollowComponent camera)
    {
        var scroll = _playerInputActions.Player.Zoom.ReadValue<float>();

        if (!MathUtils.IsZero(scroll))
        {
            camera.Zoom -= scroll;
        }
    }

    /// <summary>
    /// Handles input for manipulating the camera yaw (rotating around).
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    private void ProcessCameraYawPitch(ref CameraFollowComponent camera)
    {
        var mouse = _playerInputActions.Player.Look.ReadValue<Vector2>();
        //Debug.Log($"x: {mouse.x}");
        //Debug.Log($"y: {mouse.y}");
        //Debug.Log($"pitch before: {camera.Pitch}");
        //Debug.Log($"yaw before: {camera.Yaw}");
        camera.Yaw += mouse.x;
        camera.Pitch -= mouse.y;
        Debug.Log($"pitch: {camera.Pitch}");
        Debug.Log($"yaw: {camera.Yaw}");
    }
}