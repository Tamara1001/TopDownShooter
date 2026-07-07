// =============================================================================
//  DoorController.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Handles the visual fading and collision toggle of door prefabs.
//  Used by RoomController to lock players in during combat encounters
//  and release them when cleared.
//
//  ARCHITECTURE
//  ─────────────
//  • Requires Collider and Renderer.
//  • Uses a Coroutine for smooth alpha fading.
//  • Caches the original material color and relies on "_BaseColor"
//    to ensure URP compatibility.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Controls the visual fading and collision toggle of a door.
    /// Assumes a URP-compatible material with a "_BaseColor" property.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Renderer))]
    public sealed class DoorController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Duration of the door's fade-in/fade-out animation.")]
        [SerializeField] private float _fadeDuration = 0.3f;

        private Collider _collider;
        private Renderer _renderer;
        private Material _material;
        private Color _originalColor;
        private Coroutine _fadeCoroutine;

        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _renderer = GetComponent<Renderer>();
            
            // Assume the material uses a transparent/fade URP shader.
            _material = _renderer.material;
            
            if (_material.HasProperty(BaseColorID))
            {
                _originalColor = _material.GetColor(BaseColorID);
            }
            else
            {
                // Fallback for non-URP shaders.
                _originalColor = _material.color;
            }
            
            // Door starts open (invisible and non-blocking).
            _collider.enabled = false;
            SetAlpha(0f);
        }

        /// <summary>
        /// Enables collision and fades the door in.
        /// </summary>
        public void CloseDoor()
        {
            _collider.enabled = true;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAlphaRoutine(1f));
        }

        /// <summary>
        /// Disables collision and fades the door out.
        /// </summary>
        public void OpenDoor()
        {
            _collider.enabled = false;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAlphaRoutine(0f));
        }

        private IEnumerator FadeAlphaRoutine(float targetAlpha)
        {
            float startAlpha = _material.HasProperty(BaseColorID) 
                ? _material.GetColor(BaseColorID).a 
                : _material.color.a;

            float time = 0f;
            while (time < _fadeDuration)
            {
                time += Time.deltaTime;
                float t = time / _fadeDuration;
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                SetAlpha(currentAlpha);
                yield return null;
            }

            SetAlpha(targetAlpha);
            _fadeCoroutine = null;
        }

        private void SetAlpha(float alpha)
        {
            Color color = _originalColor;
            color.a = alpha;
            
            if (_material.HasProperty(BaseColorID))
            {
                _material.SetColor(BaseColorID, color);
            }
            else
            {
                _material.color = color;
            }
        }
    }
}
