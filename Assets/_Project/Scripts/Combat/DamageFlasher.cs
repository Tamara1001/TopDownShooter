using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageFlasher : MonoBehaviour
{
    [SerializeField] private Renderer[] _renderers;
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField] private float _flashDuration = 0.15f;

    private Dictionary<Material, Color> _originalColors = new Dictionary<Material, Color>();
    private HealthComponent _health;
    private int _baseColorID;
    private float _previousHealth = -1f;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _baseColorID = Shader.PropertyToID("_BaseColor");

        foreach (var rend in _renderers)
        {
            foreach (var mat in rend.materials)
            {
                if (mat.HasProperty(_baseColorID))
                {
                    _originalColors[mat] = mat.GetColor(_baseColorID);
                }
            }
        }
    }

    private void OnEnable() => _health.OnHealthChanged += CheckDamage;
    private void OnDisable() => _health.OnHealthChanged -= CheckDamage;

    private void CheckDamage(float normalized)
    {
        if (_previousHealth >= 0 && normalized < _previousHealth)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());
        }
        _previousHealth = normalized;
    }

    private IEnumerator FlashRoutine()
    {
        foreach (var mat in _originalColors.Keys)
        {
            mat.SetColor(_baseColorID, _flashColor);
        }

        yield return new WaitForSeconds(_flashDuration);

        foreach (var entry in _originalColors)
        {
            entry.Key.SetColor(_baseColorID, entry.Value);
        }
    }
}