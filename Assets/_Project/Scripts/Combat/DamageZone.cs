
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una zona de peligro de daño continuo. Aplica daño periódico a cualquier
/// entidad que implemente <see cref="IDamageable"/> mientras permanezca
/// dentro de este colisionador trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DamageZone : MonoBehaviour
{
    // ----------------------------------------------------------
    // INSPECTOR FIELDS
    // Todos los campos son privados — [SerializeField] da acceso al Inspector
    // sin exponer setters públicos a otros scripts.
    // ----------------------------------------------------------

    [Header("Damage Settings")]

    [Tooltip("Daño aplicado a la entidad en cada tick de daño.")]
    [SerializeField] private int damageAmount = 10;

    [Tooltip("Segundos entre cada aplicación de daño. Valores más bajos = ticks de daño más rápidos. Por ejemplo, 0.5 = daño cada medio segundo.")]
    [SerializeField] private float damageTickRate = 1f;

    // ----------------------------------------------------------
    // PRIVATE STATE
    // ----------------------------------------------------------

    /// <summary>
    /// Realiza un seguimiento de los temporizadores de enfriamiento por colisionador para que cada entidad en la
    /// zona sea dañada de forma independiente en su propio intervalo de tick.
    ///
    /// KEY   = el Collider que se superpone actualmente con este trigger.
    /// VALUE = el Time.time en el cual ese colisionador es elegible por primera vez
    ///         para recibir un tick de daño.
    ///
    /// El uso de un Dictionary aquí en lugar de un único float permite que
    /// múltiples entidades estén en la zona simultáneamente con
    /// temporizadores de enfriamiento independientes que no interfieren entre sí.
    /// </summary>
    private readonly Dictionary<Collider, float> nextDamageTimeMap
        = new Dictionary<Collider, float>();

    // ----------------------------------------------------------
    // UNITY LIFECYCLE — TRIGGER EVENTS
    //
    // NOTA DE RENDIMIENTO:
    //   IDamageable se obtiene UNA VEZ en OnTriggerEnter y se almacena en caché
    //   en el Dictionary. OnTriggerStay lee de la caché,
    //   lo que significa que ocurren cero llamadas a GetComponent durante el bucle de actualización.
    // ----------------------------------------------------------

    /// <summary>
    /// Llamado por Unity cuando un Collider entra en este volumen trigger.
    /// Registra el colisionador en el mapa de tiempo de daño para que sea
    /// elegible para el ticking en OnTriggerStay.
    /// </summary>
    /// <param name="other">El Collider que entró en el trigger.</param>
    private void OnTriggerEnter(Collider other)
    {
        // Solo realizar seguimiento de los colisionadores que pertenecen a una entidad dañable.
        // GetComponent se llama aquí (al entrar) — NO en Stay.
        if (other.TryGetComponent<IDamageable>(out _))
        {
            // Registrar con un tiempo inicial de próximo daño de AHORA para que
            // el primer tick se dispare inmediatamente en el primer frame de Stay.
            if (!nextDamageTimeMap.ContainsKey(other))
            {
                nextDamageTimeMap[other] = Time.time;
            }
        }
    }

    /// <summary>
    /// Llamado por Unity en cada frame de FixedUpdate mientras un Collider permanezca
    /// dentro de este volumen trigger. Aplica daño en el intervalo de tick configurado
    /// utilizando un enfriamiento por colisionador almacenado en el mapa.
    /// </summary>
    /// <param name="other">El Collider superpuesto actualmente.</param>
    private void OnTriggerStay(Collider other)
    {
        // Salida temprana: si este colisionador nunca fue registrado (es decir,
        // no implementa IDamageable), no hacer nada — sin llamar a GetComponent.
        if (!nextDamageTimeMap.TryGetValue(other, out float nextDamageTime))
            return;

        // Verificar si el enfriamiento para esta entidad específica ha transcurrido.
        if (Time.time < nextDamageTime) return;

        // El enfriamiento ha transcurrido — intentar obtener la interfaz y aplicar daño.
        // TryGetComponent es seguro aquí; evita un antipatrón de verificación de nulos.
        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(damageAmount);

            // Programar el SIGUIENTE tick para este colisionador.
            nextDamageTimeMap[other] = Time.time + damageTickRate;
        }
    }

    /// <summary>
    /// Llamado por Unity cuando un Collider sale de este volumen trigger.
    /// Limpia la entrada del mapa para que no acumulemos referencias obsoletas.
    /// </summary>
    /// <param name="other">El Collider que salió del trigger.</param>
    private void OnTriggerExit(Collider other)
    {
        // Intentar siempre la eliminación — Dictionary.Remove es una operación nula si la
        // clave no existe, por lo que esto es seguro para colisionadores no dañables.
        nextDamageTimeMap.Remove(other);
    }

    // ----------------------------------------------------------
    // EDITOR HELPERS
    // ----------------------------------------------------------

#if UNITY_EDITOR
    /// <summary>
    /// Dibuja un cubo de alambre amarillo en la vista de Scene para visualizar
    /// el alcance de la zona de peligro. Requiere un BoxCollider para ser legible.
    /// Regresa de forma segura a una alternativa si no hay BoxCollider presente.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f); // naranja, semi-transparente

        // Intentar reflejar la forma de BoxCollider para una vista previa precisa.
        if (TryGetComponent<BoxCollider>(out BoxCollider box))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else
        {
            // Alternativa de esfera genérica para colisionadores que no sean de tipo caja.
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
#endif
}
