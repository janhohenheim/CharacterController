using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Components
{
    [Serializable]
    public sealed class CharacterControllerComponentView: MonoBehaviour, IConvertGameObjectToEntity
    {
        public float3 gravity = new float3(0.0f, -9.81f, 0.0f);
        public float maxSpeed = 7.5f;
        public float speed = 5.0f;
        public float jumpStrength = 0.25f;
        public float maxStep = 0.35f;
        public float drag = 0.2f;
    
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (!enabled)
            {
                return;
            }

            dstManager.AddComponentData(entity, new CharacterControllerComponent
            {
                Gravity = gravity,
                MaxSpeed = maxSpeed,
                Speed = speed,
                JumpStrength = jumpStrength,
                MaxStep = maxStep,
                Drag = drag,
            });
        }
    }
}