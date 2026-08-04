using System.Collections;
using UnityEngine;

namespace TopDownShooter.Player
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        private HealthComponent _health;
        private Animator _animator;
        private PlayerController3D _controller; // Agregamos referencia al controlador

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _animator = GetComponentInChildren<Animator>();
            _controller = GetComponent<PlayerController3D>();
        }

        private void OnEnable()
        {
            _health.OnDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandlePlayerDied;
        }

        private void HandlePlayerDied()
        {
            Debug.Log("[PlayerDeathHandler] Jugador muerto. Iniciando secuencia...");

            // 1. Disparar la animación visual
            if (_animator != null)
            {
                _animator.SetTrigger("Death");
            }

            // 2. Apagar el control físico inmediatamente para que no se mueva muerto
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            // 3. Iniciar la cuenta regresiva antes de llamar al GameManager
            StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            // Esperar 3 segundos reales antes de llamar al Game Over
            yield return new WaitForSeconds(3f);

            GameManager.Instance.ChangeState(GameManager.GameState.GameOver);
        }
    }
}