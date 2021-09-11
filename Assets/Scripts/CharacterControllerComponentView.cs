using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class CharacterControllerComponentView: MonoBehaviour, IConvertGameObjectToEntity
{
    public float3 gravity = new float3(0.0f, -9.81f, 0.0f);
    public float maxSpeed = 7.5f;
    public float speed = 5.0f;
    public float jumpStrength = 0.15f;
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