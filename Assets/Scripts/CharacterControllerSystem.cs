using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

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

            CharacterControllerType = characterControllerTypeHandle,
            TranslationHandles = translationTypeHandle,
            RotationHandles = rotationTypeHandle
        };

        var dependency = JobHandle.CombineDependencies(inputDeps, _exportPhysicsWorld.GetOutputDependency());
        var controllerJobHandle = controllerJob.Schedule(_characterControllerGroup, dependency);
        
        _endFramePhysicsSystem.AddInputDependency(controllerJobHandle);

        return controllerJobHandle;
    }

    private struct CharacterControllerJob : IJobChunk
    {
        public float DeltaTime;

        [ReadOnly] public PhysicsWorld PhysicsWorld;
        [ReadOnly] public EntityTypeHandle EntityHandles;
        [ReadOnly] public ComponentDataFromEntity<PhysicsCollider> ColliderData;

        public ComponentTypeHandle<CharacterControllerComponent> CharacterControllerType;
        public ComponentTypeHandle<Translation> TranslationHandles;
        public ComponentTypeHandle<Rotation> RotationHandles;


        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}