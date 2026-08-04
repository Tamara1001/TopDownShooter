
/// <summary>
/// Contrato universal para cualquier entidad que pueda recibir daño.
/// Implemente esta interfaz en cualquier MonoBehaviour que deba
/// participar en el sistema de daño.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Aplica la cantidad especificada de daño a esta entidad.
    /// Las implementaciones son responsables de su propia gestión
    /// de vida, limitación y lógica de muerte.
    /// </summary>
    /// <param name="amount">
    /// La cantidad bruta de daño a aplicar. Debe ser un entero positivo.
    /// Los valores negativos (curación) se excluyen intencionadamente de
    /// este contrato para mantener clara la responsabilidad de la interfaz.
    /// </param>
    void TakeDamage(int amount);
}
