using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Script dedicado exclusivamente a leer el estado del jugador
    /// y enviarlo al Animator. Cero riesgo de romper las físicas.
    /// </summary>
    [RequireComponent(typeof(PlayerController3D))]
    public class PlayerAnimator : MonoBehaviour
    {
        private Animator _animator;
        private PlayerController3D _controller;

        private void Awake()
        {
            // Busca el Animator en el modelo 3D (el hijo)
            _animator = GetComponentInChildren<Animator>();

            // Busca el controlador de movimiento en este mismo objeto
            _controller = GetComponent<PlayerController3D>();

            if (_animator == null)
            {
                Debug.LogWarning("[PlayerAnimator] No se encontró el Animator en el modelo del jugador.");
            }
        }

        private void Update()
        {
            if (_animator == null) return;

            // Le pasa automáticamente la variable del script al parámetro del Animator
            _animator.SetBool("IsMoving", _controller.IsMoving);

            // Si en el futuro agregas animaciones de correr o saltar, las conectas así:
            // _animator.SetBool("IsSprinting", _controller.IsSprinting);
            // _animator.SetBool("IsGrounded", _controller.IsGrounded);
        }
    }
}