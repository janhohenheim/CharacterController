using System;
using Unity.Entities;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Used to add <see cref="CameraFollowComponent"/> via the Editor.
    /// </summary>
    [Serializable]
    public sealed class CameraFollowComponentView : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float Yaw = 0.0f;
        public float Pitch = 0.0f;
        public float Zoom = 8.0f;

        public float MinPitch = 0.0f;
        public float MaxPitch = 70.0f;
        public float MinZoom = 5.0f;
        public float MaxZoom = 10.0f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (!enabled)
            {
                return;
            }

            dstManager.AddComponentData(entity, new CameraFollowComponent()
            {
                MinPitch = MinPitch,
                MaxPitch = MaxPitch,
                MinZoom = MinZoom,
                MaxZoom = MaxZoom,
                Yaw = Yaw,
                Pitch = Pitch,
                Zoom = Zoom
            });
        }
    }
}