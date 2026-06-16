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
            // Opcional: Como vi que tenés el script UI_ScreenFader armado, 
            // podés descomentar esta línea para que la pantalla se vaya 
            // a negro suavemente mientras transcurren los 3 segundos.
            // if (UI_ScreenFader.Instance != null) UI_ScreenFader.Instance.FadeTo(1f, 3f);

            // Esperar 3 segundos reales
            yield return new WaitForSeconds(3f);

            // Ahora sí, llamar al Game Over (esto congelará el tiempo y mostrará la UI)
            GameManager.Instance.ChangeState(GameManager.GameState.GameOver);
        }
    }
}