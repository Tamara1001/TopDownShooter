using UnityEngine;

/// <summary>
/// Estado de combate agresivo de la Fase 2. El jefe se reposiciona a un punto de anclaje de la sala
/// y desata una ráfaga rápida de bullet-hell usando el índice de arma 1.
/// </summary>
public class BossPhase2State : EnemyStateBase
{
    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURACIÓN
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Qué tan cerca (en unidades del mundo) debe estar el jefe de su anclaje antes
    /// de que deje de reposicionarse y comience a atacar.
    /// </summary>
    private const float ArrivalThreshold = 1.5f;

    // ─────────────────────────────────────────────────────────────────────
    //  ESTADO PRIVADO
    // ─────────────────────────────────────────────────────────────────────

    private BossBrain _bossBrain;
    private float _lastAttackTime = float.NegativeInfinity;
    private bool _hasArrived;

    // ─────────────────────────────────────────────────────────────────────
    //  CICLO DE VIDA FSM
    // ─────────────────────────────────────────────────────────────────────

    public override void Enter()
    {
        // Guardar el down-cast en caché una vez — seguro porque BossPhase2State solo
        // se registra y se llama desde un BossBrain.
        _bossBrain = Brain as BossBrain;
        if (_bossBrain == null)
        {
            Debug.LogError("[BossPhase2State] Brain is not a BossBrain! This state requires BossBrain.", Brain);
            return;
        }

        _hasArrived = false;
        Brain.Agent.isStopped = false;
        Brain.Agent.SetDestination(_bossBrain.Phase2AnchorPoint);

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", true);

        Debug.Log($"[BossPhase2State] '{Brain.name}': Repositioning to anchor {_bossBrain.Phase2AnchorPoint}.");
    }

    public override void Tick()
    {
        if (_bossBrain == null) return;

        if (!_hasArrived)
        {
            TickRepositioning();
        }
        else
        {
            TickBulletHell();
        }
    }

    public override void Exit()
    {
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  AYUDANTES DE PATRONES DE FASE
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Se mueve hacia el punto de anclaje y cambia al modo de ataque una vez que llega.
    /// </summary>
    private void TickRepositioning()
    {
        // remainingDistance solo es válido una vez que se ha calculado una ruta.
        if (!Brain.Agent.pathPending &&
            Brain.Agent.remainingDistance <= ArrivalThreshold)
        {
            _hasArrived = true;
            Brain.Agent.isStopped = true;
            Brain.Agent.ResetPath();

            if (Brain.Anim != null)
                Brain.Anim.SetBool("IsMoving", false);

            Debug.Log($"[BossPhase2State] '{Brain.name}': Anchor reached — starting bullet hell.");
        }
    }

    /// <summary>
    /// Mira al jugador y dispara el índice de arma 1 con un enfriamiento rápido.
    /// </summary>
    private void TickBulletHell()
    {
        // Siempre mirar al jugador mientras ataca.
        FacePlayer();

        if (Time.time >= _lastAttackTime + _bossBrain.GetBossWeaponCooldown(1))
        {
            if (Brain.Anim != null)
                Brain.Anim.SetTrigger("Attack");

            // Índice 1 = arma a distancia de la Fase 2 en el arsenal del jefe.
            _bossBrain.ExecuteBossWeapon(1);

            _lastAttackTime = Time.time;
        }
    }

    /// <summary>
    /// Ajusta instantáneamente al jefe para mirar al jugador en el eje Y.
    /// </summary>
    private void FacePlayer()
    {
        if (Brain.PlayerTransform == null) return;

        Vector3 direction = Brain.PlayerTransform.position - Brain.transform.position;
        direction.y = 0f;
        if (direction == Vector3.zero) return;

        Brain.transform.rotation = Quaternion.Slerp(
            Brain.transform.rotation,
            Quaternion.LookRotation(direction),
            Time.deltaTime * 12f);
    }
}
