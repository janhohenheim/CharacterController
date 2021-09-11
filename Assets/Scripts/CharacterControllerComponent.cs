using Unity.Entities;
using Unity.Mathematics;

public class CharacterControllerComponent : IComponentData
{
    /// <summary>
    ///  The current direction that the character is moving to.
    /// </summary>
    public float3 CurrentDirection { get; set; }
    
    /// <summary>
    /// The current magnitude of the character movement.
    /// If <c>0.0</c>, then the character is not being directly moved by the controller,
    /// but residual forces may still be active
    /// </summary>
    public float3 CurrentMagnitude { get; set; }
    
    /// <summary>
    /// Is the character requesting to jump?
    /// </summary>
    public bool Jump { get; set; }
    
    /// <summary>
    /// Gravity force applied to the character.
    /// </summary>
    public float3 Gravity { get; set; }
    
    /// <summary>
    /// The maximum speed at which this character moves.
    /// </summary>
    public float MaxSpeed { get; set; }

    /// <summary>
    /// The current speed at which the player moves.
    /// </summary>
    public float Speed { get; set; }
    
    /// <summary>
    /// The jump strength which controls how high a jump is.
    /// </summary>
    public float JumpStrength { get; set; }
    
    /// <summary>
    /// The maximum height the character can step up, in world units.
    /// </summary>
    public float MaxStep { get; set; }
    
    /// <summary>
    /// Drag value applied to reduce the <see cref="JumpVelocity"/>
    /// </summary>
    public float Drag { get; set; }
    
    /// <summary>
    /// True if the character is on the ground.
    /// </summary>
    public bool IsGrounded { get; set; }

    /// <summary>
    /// The current jump velocity of the character.
    /// </summary>
    public float3 JumpVelocity { get; set; }
}
