
using TopDownShooter.Inventory;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Interfaz opcional implementada por los MonoBehaviours de armas que deseen
    /// recibir sus estadísticas en tiempo de ejecución de un <see cref="TopDownShooter.Inventory.WeaponDataSO"/>
    /// al ser instanciados.
    ///
    /// <para>
    /// <b>Patrón de uso en <see cref="PlayerCombat"/>:</b>
    /// <code>
    /// if (instance is IWeaponConfigurable configurable)
    ///     configurable.Configure(weaponData);
    /// </code>
    /// </para>
    ///
    /// <para>
    /// Las armas que tienen estadísticas completamente hardcodeadas pueden omitir esta interfaz por completo —
    /// <see cref="PlayerCombat"/> realiza el cast de forma defensiva y lo omite si es nulo.
    /// </para>
    /// </summary>
    public interface IWeaponConfigurable
    {
        /// <summary>
        /// Llamado una vez por <see cref="PlayerCombat"/> inmediatamente después de que el
        /// MonoBehaviour de lógica del arma se instancie como hijo de Player.
        ///
        /// <para>
        /// Las implementaciones deben leer solo las propiedades que les interesen de
        /// <paramref name="stats"/> y almacenarlas localmente. La referencia al SO en sí
        /// NO debe almacenarse a largo plazo para mantener clara la propiedad de los datos.
        /// </para>
        /// </summary>
        /// <param name="stats">
        /// El <see cref="WeaponDataSO"/> del objeto que se acaba de recoger.
        /// Contiene la cadencia de fuego, el daño base y cualquier otro campo específico del arma.
        /// </param>
        void Configure(WeaponDataSO stats);
    }
}
