using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Utils;

[UpdateAfter(typeof(ExportPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
public class CharacterControllerSystem : JobComponentSystem
{
    private const float Epsilon = 0.001f;
    
    private BuildPhysicsWorld _buildPhysicsWorld;
    private ExportPhysicsWorld _exportPhysicsWorld;
    private EndFramePhysicsSystem _endFramePhysicsSystem;

    private EntityQuery _characterControllerGroup;

    protected override void OnCreate()
    {
        _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        _exportPhysicsWorld = World.GetOrCreateSystem<ExportPhysicsWorld>();
        _endFramePhysicsSystem = World.GetOrCreateSystem<EndFramePhysicsSystem>();

        _characterControllerGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(CharacterControllerComponent),
                typeof(Translation),
                typeof(Rotation),
                typeof(PhysicsCollider),
            }
        });
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (_characterControllerGroup.CalculateChunkCount() == 0)
        {
            return inputDeps;
        }

        var entityTypeHandle = GetEntityTypeHandle();
        var colliderData = GetComponentDataFromEntity<PhysicsCollider>(isReadOnly: true);
        
        var characterControllerTypeHandle = GetComponentTypeHandle<CharacterControllerComponent>();
        var translationTypeHandle = GetComponentTypeHandle<Translation>();
        var rotationTypeHandle = GetComponentTypeHandle<Rotation>();

        var controllerJob = new CharacterControllerJob
        {
            DeltaTime = Time.DeltaTime,
            PhysicsWorld = _buildPhysicsWorld.PhysicsWorld,
            EntityHandles = entityTypeHandle,
            ColliderData = colliderData,

            CharacterControllerHandles = characterControllerTypeHandle,
            TranslationHandles = translationTypeHandle,
            RotationHandles = rotationTypeHandle
        };

        var dependency = JobHandle.CombineDependencies(inputDeps, _exportPhysicsWorld.GetOutputDependency());
        var controllerJobHandle = controllerJob.Schedule(_characterControllerGroup, dependency);
        
        _endFramePhysicsSystem.AddInputDependency(controllerJobHandle);

        return controllerJobHandle;
    }

    // [BurstCompile]
    private struct CharacterControllerJob : IJobChunk
    {
        public float DeltaTime;

        [ReadOnly] public PhysicsWorld PhysicsWorld;
        [ReadOnly] public EntityTypeHandle EntityHandles;
        [ReadOnly] public ComponentDataFromEntity<PhysicsCollider> ColliderData;

        public ComponentTypeHandle<CharacterControllerComponent> CharacterControllerHandles;
        public ComponentTypeHandle<Translation> TranslationHandles;
        public ComponentTypeHandle<Rotation> RotationHandles;


        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var collisionWorld = PhysicsWorld.CollisionWorld;
            var chunkEntityData = chunk.GetNativeArray(EntityHandles);
            var chunkCharacterControllerData = chunk.GetNativeArray(CharacterControllerHandles);
            var chunkTranslationData = chunk.GetNativeArray(TranslationHandles);
            var chunkRotationData = chunk.GetNativeArray(RotationHandles);

            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = chunkEntityData[i];
                var controller = chunkCharacterControllerData[i];
                var position = chunkTranslationData[i];
                var rotation = chunkRotationData[i];
                var collider = ColliderData[entity];

                HandleChunk(ref entity, ref controller, ref position, ref rotation, ref collider, ref collisionWorld);
                chunkTranslationData[i] = position;
                chunkCharacterControllerData[i] = controller;
            }
        }

        private void HandleChunk(ref Entity entity, ref CharacterControllerComponent controller, ref Translation position, ref Rotation rotation, ref PhysicsCollider collider, ref CollisionWorld collisionWorld)
        {
            var epsilon = new float3(0.0f, Epsilon, 0.0f) * -math.normalize(controller.Gravity);
            var currPos = position.Value + epsilon;
            var currRot = rotation.Value;

            var verticalVelocity = new float3();
            var jumpVelocity = new float3();
            var gravityVelocity = controller.IsGrounded ? 0.0f : controller.Gravity * DeltaTime;
            
            HandleVerticalMovement(ref verticalVelocity, ref jumpVelocity, ref gravityVelocity, ref entity, ref currPos, ref currRot, ref controller, ref collider, ref collisionWorld);
            currPos += verticalVelocity;
            position.Value = currPos - epsilon;
        }

        private void HandleVerticalMovement(ref float3 totalVelocity, ref float3 jumpVelocity, ref float3 gravityVelocity, ref Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, ref PhysicsCollider collider, ref CollisionWorld collisionWorld)
        {
            totalVelocity = jumpVelocity + gravityVelocity;

            var verticalCollisions =
                PhysicsUtils.ColliderCastAll(collider, currPos, currPos + totalVelocity, ref collisionWorld, entity, Allocator.Temp);
            PhysicsUtils.TrimByFilter(ref verticalCollisions, ColliderData, PhysicsCollisionFilters.DynamicWithPhysical);

            if (verticalCollisions.Length != 0)
            {
                var transform = new RigidTransform
                {
                    pos = currPos + totalVelocity,
                    rot = currRot,
                };

                if (PhysicsUtils.ColliderDistance(out var verticalPenetration, collider, 1.0f, transform,
                    ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, ColliderData,
                    Allocator.Temp))
                {
                    if (verticalPenetration.Distance < 0.0f)
                    {
                        totalVelocity += verticalPenetration.SurfaceNormal * -verticalPenetration.Distance;

                        if (PhysicsUtils.ColliderCast(out var adjustedHit, collider, currPos, currPos + totalVelocity,
                            ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, ColliderData,
                            Allocator.Temp))
                        {
                            totalVelocity *= adjustedHit.Fraction;
                        }

                        controller.JumpVelocity = new float3();
                    }
                }
            }

            totalVelocity = MathUtils.ZeroOut(totalVelocity, 0.01f);
        }
    }
}