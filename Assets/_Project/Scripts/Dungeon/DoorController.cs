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
//  • Aggressively discovers Colliders and Renderers in all children.
//  • Uses a Coroutine for smooth alpha fading across all cached materials.
//  • Caches the original material color and relies on "_BaseColor"
//    to ensure URP compatibility.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Controls the visual fading and collision toggle of a door.
    /// Discovers all child renderers and colliders. Assumes URP-compatible
    /// materials with a "_BaseColor" property.
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED TYPES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Caches the initial material state per renderer so multi-part doors
        /// with different base colours fade proportionally.
        /// </summary>
        private class RendererCache
        {
            public Material Material;
            public Color OriginalColor;
            public bool UsesBaseColorID;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Settings")]
        [Tooltip("Duration of the door's fade-in/fade-out animation.")]
        [SerializeField] private float _fadeDuration = 0.3f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private Collider[] _colliders;
        private List<RendererCache> _rendererCaches = new List<RendererCache>();
        private Coroutine _fadeCoroutine;

        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Discover all physical barriers in the door hierarchy.
            _colliders = GetComponentsInChildren<Collider>();
            
            // Discover all visuals and cache their original material parameters.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material mat = r.material; // Will instantiate a material instance per renderer
                bool usesBase = mat.HasProperty(BaseColorID);
                
                _rendererCaches.Add(new RendererCache
                {
                    Material = mat,
                    OriginalColor = usesBase ? mat.GetColor(BaseColorID) : mat.color,
                    UsesBaseColorID = usesBase
                });
            }
            
            // Force initialization to Open (invisible, non-blocking)
            SetCollidersState(false);
            SetAlpha(0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enables collision and fades the door in.
        /// </summary>
        public void CloseDoor()
        {
            SetCollidersState(true);
            
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAlphaRoutine(1f));
        }

        /// <summary>
        /// Disables collision and fades the door out.
        /// </summary>
        public void OpenDoor()
        {
            SetCollidersState(false);
            
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAlphaRoutine(0f));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FADING LOGIC
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator FadeAlphaRoutine(float targetAlpha)
        {
            // We use the first renderer's alpha as the starting point.
            // If there are no renderers, we just wait out the duration.
            float startAlpha = 0f;
            if (_rendererCaches.Count > 0)
            {
                RendererCache firstCache = _rendererCaches[0];
                startAlpha = firstCache.UsesBaseColorID 
                    ? firstCache.Material.GetColor(BaseColorID).a 
                    : firstCache.Material.color.a;
            }

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
            for (int i = 0; i < _rendererCaches.Count; i++)
            {
                RendererCache cache = _rendererCaches[i];
                Color color = cache.OriginalColor;
                color.a = alpha;
                
                if (cache.UsesBaseColorID)
                {
                    cache.Material.SetColor(BaseColorID, color);
                }
                else
                {
                    cache.Material.color = color;
                }
            }
        }
        
        // ─────────────────────────────────────────────────────────────────────
        //  COLLISION UTILITY
        // ─────────────────────────────────────────────────────────────────────

        private void SetCollidersState(bool state)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = state;
                }
            }
        }
    }
}
