using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Utils;

namespace Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup)), UpdateAfter(typeof(ExportPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class CharacterControllerSystem : SystemBase
    {
        private const float Epsilon = 0.001f;
    
        private BuildPhysicsWorld _buildPhysicsWorld;

        protected override void OnCreate()
        {
            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        }
    
        protected override void OnUpdate()
        {
            var collisionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
            var deltaTime = Time.DeltaTime;
            var job = Entities
                .WithReadOnly(collisionWorld)
                .ForEach((
                    Entity entity,
                    ref CharacterControllerComponent controller,
                    ref Translation translation,
                    ref Rotation rotation, 
                    in PhysicsCollider collider
                ) =>
                {
                    HandleChunk(deltaTime, in entity, ref controller, ref translation, ref rotation, in collider, in collisionWorld);
                }).ScheduleParallel(JobHandle.CombineDependencies(Dependency, _buildPhysicsWorld.GetOutputDependency()));
            job.Complete();
        }

        private static void HandleChunk(float deltaTime, in Entity entity, ref CharacterControllerComponent controller, ref Translation position, ref Rotation rotation, in PhysicsCollider collider, in CollisionWorld collisionWorld)
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
                                     deltaTime;
            HandleHorizontalMovement(ref horizontalVelocity, in entity, ref currPos, ref currRot, ref controller,
                in collider, in collisionWorld);
            currPos += horizontalVelocity;
        
        
            var gravityVelocity = controller.IsGrounded ? 0.0f : controller.Gravity * deltaTime;
        
            HandleVerticalMovement(ref verticalVelocity, ref jumpVelocity, ref gravityVelocity, in entity, ref currPos, ref currRot, ref controller, in collider, in collisionWorld);
            ApplyDrag(deltaTime, ref jumpVelocity, ref controller);
        
            currPos += verticalVelocity;

            DetermineIfGrounded(entity, ref currPos, ref epsilon, ref controller, in collider, in collisionWorld);
        
            position.Value = currPos - epsilon;
        }

        private static void HandleHorizontalMovement(ref float3 horizontalVelocity, in Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
        {
            if (MathUtils.IsZero(horizontalVelocity))
            {
                return;
            }

            var targetPos = currPos + horizontalVelocity;

            var horizontalCollisions = PhysicsUtils.RaycastAll(currPos, targetPos, in collisionWorld, entity,
                PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp);

            if (horizontalCollisions.Length == 0)
            {
                return;
            }
            
            var step = new float3(0.0f, controller.MaxStep, 0.0f);
            // TODO: Swap from and to
            PhysicsUtils.ColliderCast(out var nearestStepHit, collider, targetPos + step, targetPos,
                in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null,
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
                    in collisionWorld, entity, Allocator.Temp);
                // PhysicsUtils.TrimByFilter(ref horizontalCollisions, null, PhysicsCollisionFilters.DynamicWithPhysical);

                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var horizontalDistanceHit in horizontalDistances)
                {
                    if (horizontalDistanceHit.Distance < 0.0f)
                    {
                        horizontalVelocity += horizontalDistanceHit.SurfaceNormal * -horizontalDistanceHit.Distance;
                    }
                }
            }
        }

        // TODO: What about epsilon?
        private static unsafe void DetermineIfGrounded(Entity entity, ref float3 currPos, ref float3 epsilon, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
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
        
            controller.IsGrounded = PhysicsUtils.Raycast(out var _, samplePos, samplePos + offset, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                                    ||  PhysicsUtils.Raycast(out var _, posLeft, posLeft + offset, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                                    ||  PhysicsUtils.Raycast(out var _, posRight, posRight + offset, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                                    ||  PhysicsUtils.Raycast(out var _, posForward, posForward + offset, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp)
                                    ||  PhysicsUtils.Raycast(out var _, posBackward, posBackward + offset, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, Allocator.Temp);
            
        }

        private static void ApplyDrag(float deltaTime, ref float3 jumpVelocity, ref CharacterControllerComponent controller)
        {
            var currSpeed = math.length(jumpVelocity);
            var dragDelta = controller.Drag * deltaTime;

            currSpeed = math.max(currSpeed - dragDelta, 0.0f);
        
            if (MathUtils.IsZero(currSpeed))
            {
                jumpVelocity = new float3();
            }
            else
            {
                jumpVelocity = math.normalize(jumpVelocity) * currSpeed;
                jumpVelocity = MathUtils.SetToZero(jumpVelocity, 0.001f);
            }

            controller.JumpVelocity = jumpVelocity;
        }

        private static void HandleVerticalMovement(ref float3 totalVelocity, ref float3 jumpVelocity, ref float3 gravityVelocity, in Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
        {
            totalVelocity = jumpVelocity + gravityVelocity;

            var verticalCollisions =
                PhysicsUtils.ColliderCastAll(collider, currPos, currPos + totalVelocity, in collisionWorld, entity, Allocator.Temp);
            // PhysicsUtils.TrimByFilter(ref verticalCollisions, colliderData, PhysicsCollisionFilters.DynamicWithPhysical);

            if (verticalCollisions.Length != 0)
            {
                var transform = new RigidTransform
                {
                    pos = currPos + totalVelocity,
                    rot = currRot,
                };

                if (PhysicsUtils.ColliderDistance(out var verticalPenetration, collider, 1.0f, transform,
                    in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null,
                    Allocator.Temp))
                {
                    if (verticalPenetration.Distance < 0.0f)
                    {
                        totalVelocity += verticalPenetration.SurfaceNormal * -verticalPenetration.Distance;

                        if (PhysicsUtils.ColliderCast(out var adjustedHit, collider, currPos, currPos + totalVelocity,
                            in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null,
                            Allocator.Temp))
                        {
                            totalVelocity *= adjustedHit.Fraction;
                        }

                        controller.JumpVelocity = new float3();
                    }
                }
            }

            totalVelocity = MathUtils.SetToZero(totalVelocity, 0.01f);
            
        }
    }
}