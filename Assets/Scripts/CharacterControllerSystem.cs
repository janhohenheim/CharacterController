using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Utils;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(ExportPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
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

    // TODO: [BurstCompile]
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

            var jumpVelocity = controller.JumpVelocity;

            if (controller.IsGrounded && controller.Jump && MathUtils.IsZero(math.lengthsq(controller.JumpVelocity)))
            {
                jumpVelocity += controller.JumpStrength * -math.normalize(controller.Gravity);
            }

            var horizontalVelocity = controller.CurrentDirection * controller.CurrentMagnitude * controller.Speed *
                                     DeltaTime;
            HandleHorizontalMovement(ref horizontalVelocity, ref entity, ref currPos, ref currRot, ref controller,
                ref collider, ref collisionWorld);
            currPos += horizontalVelocity;
            
            
            var gravityVelocity = controller.IsGrounded ? 0.0f : controller.Gravity * DeltaTime;
            
            HandleVerticalMovement(ref verticalVelocity, ref jumpVelocity, ref gravityVelocity, ref entity, ref currPos, ref currRot, ref controller, ref collider, ref collisionWorld);
            ApplyDrag(ref jumpVelocity, ref controller);
            
            currPos += verticalVelocity;

            DetermineIfGrounded(entity, ref currPos, ref epsilon, ref controller, ref collider, ref collisionWorld);
            
            position.Value = currPos - epsilon;
        }

        private void HandleHorizontalMovement(ref float3 horizontalVelocity, ref Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, ref PhysicsCollider collider, ref CollisionWorld collisionWorld)
        {
            if (MathUtils.IsZero(horizontalVelocity))
            {
                return;
            }

            var targetPos = currPos + horizontalVelocity;

            var horizontalCollisions = PhysicsUtils.RaycastAll(currPos, targetPos, ref collisionWorld, entity,
                PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp);

            if (horizontalCollisions.Length != 0)
            {
                var step = new float3(0.0f, controller.MaxStep, 0.0f);
                // TODO: Swap from and to
                PhysicsUtils.ColliderCast(out var nearestStepHit, collider, targetPos + step, targetPos,
                    ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, ColliderData,
                    Allocator.Temp);
                if (!MathUtils.IsZero(nearestStepHit.Fraction))
                {
                    // step up
                    // TODO: + aabb height / 2
                    targetPos += step * (1.0f - nearestStepHit.Fraction);
                    horizontalVelocity = targetPos - currPos;
                }
                else
                {
                    // slide
                    var transform = new RigidTransform
                    {
                        pos = currPos + horizontalVelocity,
                        rot = currRot
                    };
                    // TODO: Can this be a nearest hit?
                    var horizontalDistances = PhysicsUtils.ColliderDistanceAll(collider, 1.0f, transform,
                        ref collisionWorld, entity, Allocator.Temp);
                    PhysicsUtils.TrimByFilter(ref horizontalCollisions, ColliderData, PhysicsCollisionFilters.DynamicWithPhysical);

                    // TODO: Maybe use index for burst
                    foreach (var horizontalDistanceHit in horizontalDistances)
                    {
                        if (horizontalDistanceHit.Distance < 0.0f)
                        {
                            horizontalVelocity += horizontalDistanceHit.SurfaceNormal * -horizontalDistanceHit.Distance;
                        }
                    }
                }
            }
        }

        // TODO: What about epsilon?
        private static unsafe void DetermineIfGrounded(Entity entity, ref float3 currPos, ref float3 epsilon, ref CharacterControllerComponent controller, ref PhysicsCollider collider, ref CollisionWorld collisionWorld)
        {
            var aabb = collider.ColliderPtr->CalculateAabb();
            const float mod = 0.15f;

            var samplePos = currPos + new float3(0.0f, aabb.Min.y, 0.0f);
            var gravity = math.normalize(controller.Gravity);
            var offset = gravity * 0.1f;

            var posLeft = samplePos - new float3(aabb.Extents.x * mod, 0.0f, 0.0f);
            var posRight = samplePos + new float3(aabb.Extents.x * mod, 0.0f, 0.0f);
            var posForward = samplePos + new float3(0.0f, 0.0f, aabb.Extents.x);
            var posBackward = samplePos - new float3(0.0f, 0.0f, aabb.Extents.x);
            
            controller.IsGrounded = PhysicsUtils.Raycast(out var _, samplePos, samplePos + offset, ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                ||  PhysicsUtils.Raycast(out var _, posLeft, posLeft + offset, ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                ||  PhysicsUtils.Raycast(out var _, posRight, posRight + offset, ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                ||  PhysicsUtils.Raycast(out var _, posForward, posForward + offset, ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                ||  PhysicsUtils.Raycast(out var _, posBackward, posBackward + offset, ref collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp);
                
        }

        private void ApplyDrag(ref float3 jumpVelocity, ref CharacterControllerComponent controller)
        {
            var currSpeed = math.length(jumpVelocity);
            var dragDelta = controller.Drag * DeltaTime;

            currSpeed = math.max(currSpeed - dragDelta, 0.0f);
            
            if (MathUtils.IsZero(currSpeed))
            {
                jumpVelocity = new float3();
            }
            else
            {
                jumpVelocity = math.normalize(jumpVelocity) * currSpeed;
                jumpVelocity = MathUtils.ZeroOut(jumpVelocity, 0.001f);
            }

            controller.JumpVelocity = jumpVelocity;
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