using Components;
using Unity.Entities;
using UnityEngine;

public sealed class PlayerControllerComponentView : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        if (!enabled)
        {
            return;
        }

        dstManager.AddComponentData(entity, new PlayerControllerComponent());
    }
}