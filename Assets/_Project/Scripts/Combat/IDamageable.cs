
/// <summary>
/// Universal contract for any entity that can receive damage.
/// Implement this interface on any MonoBehaviour that should
/// participate in the damage system.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Applies the specified amount of damage to this entity.
    /// Implementations are responsible for their own health
    /// management, clamping, and death logic.
    /// </summary>
    /// <param name="amount">
    /// The raw damage amount to apply. Must be a positive integer.
    /// Negative values (healing) are intentionally excluded from
    /// this contract to keep the interface's responsibility clear.
    /// </param>
    void TakeDamage(int amount);
}
