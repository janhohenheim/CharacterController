using Unity.Entities;
using UnityEngine;

namespace Components
{
    public sealed class PlayerControllerComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
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
}