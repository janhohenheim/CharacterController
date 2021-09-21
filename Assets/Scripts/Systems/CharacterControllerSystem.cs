using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Utils;
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

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
            
        
            var gravityVelocity = controller.Gravity * deltaTime;
            var verticalVelocity = controller.JumpVelocity + gravityVelocity;
            var horizontalVelocity = controller.CurrentDirection * controller.CurrentMagnitude * controller.Speed * deltaTime;

            if (controller.IsGrounded)
            {
                if (controller.Jump)
                {
                    verticalVelocity = controller.JumpStrength * -math.normalize(controller.Gravity);
                }
                else
                {
                    var gravityDir = math.normalize(gravityVelocity);
                    var verticalDir = math.normalize(verticalVelocity);

                    if (MathUtils.FloatEquals(math.dot(gravityDir, verticalDir), 1.0f))
                    {
                        verticalVelocity = new float3();
                    }
                }
            }
            
            controller.Jump = false;
        
            HandleHorizontalMovement(ref horizontalVelocity, in entity, ref currPos, ref currRot, ref controller,
                in collider, in collisionWorld);
            currPos += horizontalVelocity;
            
            HandleVerticalMovement(ref verticalVelocity, deltaTime, in entity, ref currPos, ref currRot, ref controller, in collider, in collisionWorld);
        
            currPos += verticalVelocity;

            
            CorrectForCollision(entity, ref currPos, ref currRot, ref controller, in collisionWorld);
            DetermineIfGrounded(entity, ref currPos, ref controller, in collider, in collisionWorld);
        
            position.Value = currPos - epsilon;
        }

        private static void HandleHorizontalMovement(ref float3 horizontalVelocity, in Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
        {
                if (MathUtils.IsZero(horizontalVelocity))
                {
                    return;
                }

                var targetPos = currPos + horizontalVelocity;

                var horizontalCollisions = PhysicsUtils.ColliderCastAll(collider, currPos, targetPos, in collisionWorld, entity, Allocator.Temp);
                //PhysicsUtils.TrimByFilter(ref horizontalCollisions, ColliderData, PhysicsCollisionFilters.DynamicWithPhysical);

                if (horizontalCollisions.Length > 0)
                {
                    // We either have to step or slide as something is in our way.
                    var step = new float3(0.0f, controller.MaxStep, 0.0f);
                    PhysicsUtils.ColliderCast(out var nearestStepHit, collider, targetPos + step, targetPos, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null, Allocator.Temp);

                    if (!MathUtils.IsZero(nearestStepHit.Fraction))
                    {
                        // We can step up.
                        targetPos += step * (1.0f - nearestStepHit.Fraction);
                        horizontalVelocity = targetPos - currPos;
                    }
                    else
                    {
                        // We can not step up, so slide.
                        var horizontalDistances = PhysicsUtils.ColliderDistanceAll(collider, 1.0f, new RigidTransform { pos = currPos + horizontalVelocity, rot = currRot }, in collisionWorld, entity, Allocator.Temp);
                        //PhysicsUtils.TrimByFilter(ref horizontalDistances, ColliderData, PhysicsCollisionFilters.DynamicWithPhysical);

                        foreach (var hit in horizontalDistances)
                        {
                            if (hit.Distance >= 0.0f)
                            {
                                continue;
                            }

                            horizontalVelocity += hit.SurfaceNormal * -hit.Distance;
                        }

                        horizontalDistances.Dispose();
                    }
                }

                horizontalCollisions.Dispose();
        }
        
        /// <summary>
        /// Performs a collision correction at the specified position.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="currPos"></param>
        /// <param name="currRot"></param>
        /// <param name="controller"></param>
        /// <param name="collisionWorld"></param>
        private static void CorrectForCollision(Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, in CollisionWorld collisionWorld)
        {
            var transform = new RigidTransform
            {
                pos = currPos,
                rot = currRot
            };

            // Use a subset sphere within our collider to test against.
            // We do not use the collider itself as some intersection (such as on ramps) is ok.

            var offset = -math.normalize(controller.Gravity) * 0.1f;
            var sampleCollider = new PhysicsCollider
            {
                Value = SphereCollider.Create(new SphereGeometry
                {
                    Center = currPos + offset,
                    Radius = 0.1f
                })
            };

            if (!PhysicsUtils.ColliderDistance(out var smallestHit, sampleCollider, 0.1f, transform,
                in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null,
                Allocator.Temp))
            {
                return;
            }
            if (smallestHit.Distance < 0.0f)
            {
                currPos += math.abs(smallestHit.Distance) * smallestHit.SurfaceNormal;
            }
        }

        private static unsafe void DetermineIfGrounded(Entity entity, ref float3 currPos, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
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
        
        
        private static void HandleVerticalMovement(ref float3 verticalVelocity, float deltaTime, in Entity entity, ref float3 currPos, ref quaternion currRot, ref CharacterControllerComponent controller, in PhysicsCollider collider, in CollisionWorld collisionWorld)
        {
            controller.JumpVelocity = verticalVelocity;

            if (MathUtils.IsZero(verticalVelocity))
            {
                return;
            }

            verticalVelocity *= deltaTime;

            var verticalCollisions = PhysicsUtils.ColliderCastAll(collider, currPos, currPos + verticalVelocity, in collisionWorld, entity, Allocator.Temp);
            //PhysicsUtils.TrimByFilter(ref verticalCollisions, ColliderData, PhysicsCollisionFilters.DynamicWithPhysical);

            if (verticalCollisions.Length > 0)
            {
                var transform = new RigidTransform
                {
                    pos = currPos + verticalVelocity,
                    rot = currRot
                };

                if (PhysicsUtils.ColliderDistance(out var verticalPenetration, collider, 1.0f, transform, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null, Allocator.Temp))
                {
                    if (verticalPenetration.Distance < -0.01f)
                    {
                        verticalVelocity += verticalPenetration.SurfaceNormal * verticalPenetration.Distance;

                        if (PhysicsUtils.ColliderCast(out var adjustedHit, collider, currPos, currPos + verticalVelocity, in collisionWorld, entity, PhysicsCollisionFilters.DynamicWithPhysical, null, null, Allocator.Temp))
                        {
                            verticalVelocity *= adjustedHit.Fraction;
                        }
                    }
                }
            }

            verticalVelocity = MathUtils.SetToZero(verticalVelocity, 0.0001f);
            verticalCollisions.Dispose();
            
        }
    }
}