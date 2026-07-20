using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Reads the player's movement state and forwards it to the Animator.
    /// Keeping animation logic here prevents accidental physics interference.
    /// </summary>
    [RequireComponent(typeof(PlayerController3D))]
    public class PlayerAnimator : MonoBehaviour
    {
        private Animator _animator;
        private PlayerController3D _controller;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _controller = GetComponent<PlayerController3D>();

            if (_animator == null)
            {
                Debug.LogWarning("[PlayerAnimator] No Animator found on the player model.");
            }
        }

        private void Update()
        {
            if (_animator == null) return;

            _animator.SetBool("IsMoving", _controller.IsMoving);

            // Si en el futuro agregas animaciones de correr o saltar, las conectas así:
            // _animator.SetBool("IsSprinting", _controller.IsSprinting);
            // _animator.SetBool("IsGrounded", _controller.IsGrounded);
        }
    }
}