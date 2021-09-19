using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Components
{
    /// <summary>
    /// Used to add <see cref="CameraFollowComponent"/> via the Editor.
    /// </summary>
    [Serializable]
    public sealed class CameraFollowComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public float yaw = 0.0f;
        public float pitch = 0.0f;
        public float zoom = 8.0f;

        public float minPitch = 0.0f;
        public float maxPitch = 70.0f;
        public float minZoom = 5.0f;
        public float maxZoom = 10.0f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (!enabled)
            {
                return;
            }

            dstManager.AddComponentData(entity, new CameraFollowComponent()
            {
                MinPitch = minPitch,
                MaxPitch = maxPitch,
                MinZoom = minZoom,
                MaxZoom = maxZoom,
                Yaw = yaw,
                Pitch = pitch,
                Zoom = zoom
            });
        }
    }
}