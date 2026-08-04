

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownShooter.Player;
using TopDownShooter.Combat;

public class PlayerHUD : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // CAMPOS DEL INSPECTOR
    // -------------------------------------------------------------------------

    [Header("Data Sources")]
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private PlayerResourceComponent playerResources;

    // -------------------------------------------------------------------------
    // SALUD — VISUALIZACIÓN DE CORAZONES
    // -------------------------------------------------------------------------
    //
    //  Reglas de diseño (igual que antes):
    //    • HP Máx = 100, dividido en 5 corazones.
    //    • Cada corazón completo = 20 HP.
    //    • Cada corazón tiene 5 estados (índice de sprite 0–4):
    //
    //        Índice de estado | HP en este corazón | Sugerencia de sprite
    //        -----------------|--------------------|---------------------
    //            0            |   0 HP  (vacío)    | heart_empty
    //            1            |   5 HP  (1/4)      | heart_quarter
    //            2            |  10 HP  (medio)    | heart_half
    //            3            |  15 HP  (3/4)      | heart_three_quarters
    //            4            |  20 HP  (lleno)    | heart_full
    //
    // -------------------------------------------------------------------------

    [Header("Health Visuals (Hearts)")]
    [Tooltip("El RectTransform padre que agrupa todas las imágenes de corazones. Usado para la animación de escala al curar.")]
    [SerializeField] private RectTransform heartsContainer;

    [Tooltip("Los componentes Image individuales para cada ranura de corazón, ordenados de izquierda a derecha (índice 0 = corazón más a la izquierda).")]
    [SerializeField] private Image[] corazonesUI;

    [Tooltip(
        "Los 5 sprites de estado del corazón en ORDEN ESTRICTO:\n" +
        "  [0] Vacío       (0 HP)\n" +
        "  [1] Cuarto      (5 HP)\n" +
        "  [2] Medio       (10 HP)\n" +
        "  [3] Tres cuartos (15 HP)\n" +
        "  [4] Lleno       (20 HP)")]
    [SerializeField] private Sprite[] estadosCorazon;

    // -------------------------------------------------------------------------
    // MANA — VISUALIZACIÓN DE ICONOS DE LLAMA
    // -------------------------------------------------------------------------
    //
    //  Mismas matemáticas fraccionarias que los corazones, pero para el Mana:
    //    • Mana Máx = 100, dividido en 5 iconos de llama.
    //    • Cada llama completa = 20 de Mana.
    //    • Cada llama tiene 5 estados (índice de sprite 0–4):
    //
    //        Índice de estado | Mana en este icono | Sugerencia de sprite
    //        -----------------|--------------------|---------------------
    //            0            |   0 Mana (vacío)   | flame_empty
    //            1            |   5 Mana (1/4)     | flame_quarter
    //            2            |  10 Mana (medio)   | flame_half
    //            3            |  15 Mana (3/4)     | flame_three_quarters
    //            4            |  20 Mana (lleno)   | flame_full
    //
    // -------------------------------------------------------------------------

    [Header("Mana Visuals (Flames)")]
    [Tooltip("El RectTransform padre que agrupa todas las imágenes de llamas de mana. Usado para la animación de parpadeo de error.")]
    [SerializeField] private RectTransform manaContainer;

    [Tooltip("Los componentes Image individuales para cada ranura de llama de mana, ordenados de izquierda a derecha.")]
    [SerializeField] private Image[] manaUI;

    [Tooltip(
        "Los 5 sprites de estado del mana en ORDEN ESTRICTO:\n" +
        "  [0] Vacío       (0 Mana)\n" +
        "  [1] Cuarto      (5 Mana)\n" +
        "  [2] Medio       (10 Mana)\n" +
        "  [3] Tres cuartos (15 Mana)\n" +
        "  [4] Lleno       (20 Mana)")]
    [SerializeField] private Sprite[] manaStates;

    // -------------------------------------------------------------------------
    // ENERGÍA — VISUALIZACIÓN DE ICONOS DE RAYO
    // -------------------------------------------------------------------------
    //
    //  Mismas matemáticas fraccionarias que los corazones y llamas, pero para la Energía:
    //    • Energía Máx = 100, dividida en 5 iconos de rayo.
    //    • Cada rayo completo = 20 de Energía.
    //    • Cada rayo tiene 5 estados (índice de sprite 0–4):
    //
    //        Índice de estado | Energía en este icono | Sugerencia de sprite
    //        -----------------|-----------------------|---------------------
    //            0            |   0 Energía (vacío)   | bolt_empty
    //            1            |   5 Energía (1/4)     | bolt_quarter
    //            2            |  10 Energía (medio)   | bolt_half
    //            3            |  15 Energía (3/4)     | bolt_three_quarters
    //            4            |  20 Energía (lleno)   | bolt_full
    //
    // -------------------------------------------------------------------------

    [Header("Energy Visuals (Lightning Bolts)")]
    [Tooltip("El RectTransform padre que agrupa todas las imágenes de rayos de energía. Usado para la animación de parpadeo de error.")]
    [SerializeField] private RectTransform energyContainer;

    [Tooltip("Los componentes Image individuales para cada ranura de rayo de energía, ordenados de izquierda a derecha.")]
    [SerializeField] private Image[] energyUI;

    [Tooltip(
        "Los 5 sprites de estado de energía en ORDEN ESTRICTO:\n" +
        "  [0] Vacío       (0 Energía)\n" +
        "  [1] Cuarto      (5 Energía)\n" +
        "  [2] Medio       (10 Energía)\n" +
        "  [3] Tres cuartos (15 Energía)\n" +
        "  [4] Lleno       (20 Energía)")]
    [SerializeField] private Sprite[] energyStates;

    // -------------------------------------------------------------------------
    // JUGO (JUICE) — CONFIGURACIÓN DE ANIMACIONES
    // -------------------------------------------------------------------------

    [Header("Juice Settings")]
    [Tooltip("Multiplicador de escala aplicado a heartsContainer cuando el jugador se cura.")]
    [SerializeField] private float _healPunchScale = 1.2f;

    [Tooltip("Cuánto tiempo (segundos) tarda la animación de escala de golpe en volver a la normalidad.")]
    [SerializeField] private float _punchDuration = 0.15f;

    [Tooltip("Cuánto tiempo (segundos) permanece visible el color de parpadeo al recibir daño o agotar recursos.")]
    [SerializeField] private float _flashDuration = 0.15f;

    [Tooltip("El color con el que parpadean los iconos cuando el jugador recibe daño o agota un recurso.")]
    [SerializeField] private Color _errorFlashColor = Color.red;

    // -------------------------------------------------------------------------
    // MONEDERO / UI DE MONEDAS
    // -------------------------------------------------------------------------

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI _coinText;

    [Tooltip("Multiplicador de escala aplicado al texto de monedas cuando se agregan monedas.")]
    [SerializeField] private float _pulseScale = 1.4f;

    [Tooltip("Cuánto tiempo (segundos) tarda la animación de pulso del texto de monedas en encogerse de nuevo.")]
    [SerializeField] private float _pulseDuration = 0.2f;

    // -------------------------------------------------------------------------
    // CONSTANTES MATEMÁTICAS DE ICONOS
    // -------------------------------------------------------------------------
    //
    //  Estas constantes son compartidas por los TRES tipos de recursos porque todos
    //  siguen exactamente las mismas reglas de iconos fraccionarios:
    //    MAX_VALUE          = 100  (puntos totales de recurso)
    //    ICONS_COUNT        =   5  (número de iconos en la fila)
    //    POINTS_PER_ICON    =  20  (100 / 5)
    //    POINTS_PER_QUARTER =   5  (20 / 4 cuartos)
    //
    // -------------------------------------------------------------------------

    /// <summary>
    /// Puntos de recurso totales que representa un icono completamente lleno.
    /// (Máx 100 / 5 iconos = 20 puntos por icono)
    /// </summary>
    private const int POINTS_PER_ICON = 20;

    /// <summary>
    /// Puntos de recurso que representa un segmento de cuarto de icono.
    /// (20 puntos por icono / 4 cuartos = 5 puntos por cuarto)
    /// </summary>
    private const int POINTS_PER_QUARTER = 5;

    // -------------------------------------------------------------------------
    // ESTADO DE EJECUCIÓN PRIVADO
    // -------------------------------------------------------------------------

    private PlayerWallet _wallet;
    private Coroutine    _pulseCoroutine;
    private Vector3      _originalCoinTextScale;

    // Realiza un seguimiento de la salud normalizada anterior para que podamos detectar
    // si el jugador se curó (el valor subió) o recibió daño (el valor bajó).
    private float _previousHealth = -1f;

    // Los manejadores de corrutinas nos permiten detener un parpadeo/pulso en curso antes de comenzar uno nuevo,
    // lo que evita fallos visuales cuando los eventos se disparan en rápida sucesión.
    private Coroutine _healthFlash;
    private Coroutine _manaFlash;
    private Coroutine _energyFlash;

    // -------------------------------------------------------------------------
    // CICLO DE VIDA DE UNITY
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Encontrar el PlayerWallet que vive en algún lugar de la escena.
        _wallet = FindObjectOfType<PlayerWallet>();

        // Guardar en caché el estado del texto de monedas para poder restaurarlo después de la animación de pulso.
        if (_coinText != null)
        {
            _originalCoinTextScale = _coinText.transform.localScale;
            _coinText.text = "0";
        }
    }

    private void OnEnable()
    {
        // ── Salud ──────────────────────────────────────────────────────────
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;

            // Dibuja el estado inicial de los corazones de inmediato para que el HUD sea correcto
            // desde el primer frame (antes de recibir cualquier daño).
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }

        // ── Resources (Mana / Energy) ────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   += UpdateManaBar;
            playerResources.OnEnergyChanged += UpdateEnergyBar;

            // Inicializar las filas de iconos con los valores actuales al iniciar.
            UpdateManaBar(playerResources.GetNormalizedMana());
            UpdateEnergyBar(playerResources.GetNormalizedEnergy());
        }

        // ── Eventos de parpadeo por agotamiento (disparados por sistemas de combate/movimiento) ────────
        PlayerCombat.OnManaDepleted         += HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted       += HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted += HandleEnergyDepleted;

        // ── Wallet ───────────────────────────────────────────────────────────
        if (_wallet != null)
        {
            _wallet.OnCoinsChanged += HandleCoinsChanged;
            if (_coinText != null) _coinText.text = _wallet.Coins.ToString();
        }
    }

    private void OnDisable()
    {
        // Cancelar siempre la suscripción en OnDisable para evitar fugas de memoria y
        // llamadas de retorno "fantasma" de objetos destruidos.
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;

        if (playerResources != null)
        {
            playerResources.OnManaChanged   -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }

        PlayerCombat.OnManaDepleted         -= HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted       -= HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted -= HandleEnergyDepleted;

        if (_wallet != null) _wallet.OnCoinsChanged -= HandleCoinsChanged;
    }

    // =========================================================================
    // CENTRALIZED ICON MATH HELPER  ← THE KEY NEW FUNCTION
    // =========================================================================

    /// <summary>
    /// Actualiza una fila de iconos de recursos fraccionarios para reflejar <paramref name="currentValue"/>.
    ///
    /// CÓMO FUNCIONA LAS MATEMÁTICAS (misma lógica para Salud, Maná y Energía):
    ///
    ///   Cada icono en el arreglo es responsable de una "ventana" de 20 puntos:
    ///     Icono 0 →  1 – 20 pts
    ///     Icono 1 → 21 – 40 pts
    ///     Icono 2 → 41 – 60 pts
    ///     Icono 3 → 61 – 80 pts
    ///     Icono 4 → 81 – 100 pts
    ///
    ///   Para cada icono calculamos cuántos puntos "se desbordan" en su ventana:
    ///     pointsInThisIcon = currentValue - (iconIndex * POINTS_PER_ICON)
    ///     → limitado a [0, 20] para que nunca sea negativo o exceda el límite.
    ///
    ///   Luego dividimos por POINTS_PER_QUARTER (5) usando división entera para
    ///   obtener un índice de estado de sprite en [0, 4]:
    ///     0 pts   → índice 0 → Sprite Vacío
    ///     1–5     → índice 1 → Sprite de Cuarto
    ///     6–10    → índice 2 → Sprite de Medio
    ///     11–15   → índice 3 → Sprite de Tres Cuartos
    ///     16–20   → índice 4 → Sprite Lleno
    ///
    ///   EJEMPLO: currentValue = 55 (de 100)
    ///     i=0 → 55 - (0*20) = 55 → limit → 20 → /5 = 4 → Lleno
    ///     i=1 → 55 - (1*20) = 35 → limit → 20 → /5 = 4 → Lleno
    ///     i=2 → 55 - (2*20) = 15 → limit → 15 → /5 = 3 → Tres Cuartos
    ///     i=3 → 55 - (3*20) = -5 → limit →  0 → /5 = 0 → Vacío
    ///     i=4 → 55 - (4*20) =-25 → limit →  0 → /5 = 0 → Vacío
    ///
    /// </summary>
    /// <param name="currentValue">El valor del recurso actual como un entero (por ejemplo, 55).</param>
    /// <param name="uiIcons">El arreglo de componentes Image a actualizar (uno por ranura de icono).</param>
    /// <param name="states">
    ///   Arreglo de exactamente 5 sprites en orden: [0]=Vacío, [1]=Cuarto, [2]=Medio,
    ///   [3]=Tres cuartos, [4]=Lleno.
    /// </param>
    private void UpdateFractionalIcons(int currentValue, Image[] uiIcons, Sprite[] states)
    {
        // ── Comprobaciones de seguridad ─────────────────────────────────────
        // Si falta alguna referencia requerida en el Inspector, salir silenciosamente
        // en lugar de lanzar una excepción NullReferenceException en tiempo de ejecución.
        if (uiIcons == null || uiIcons.Length == 0) return;
        if (states  == null || states.Length < 5)   return;

        // ── Bucle sobre cada ranura de icono ────────────────────────────────
        for (int i = 0; i < uiIcons.Length; i++)
        {
            // Omitir entradas nulas (en caso de que una ranura de arreglo se haya dejado vacía).
            if (uiIcons[i] == null) continue;

            // PASO A: ¿Cuántos puntos "se desbordan" en la ventana de 20 puntos de este icono?
            int pointsInThisIcon = currentValue - (i * POINTS_PER_ICON);

            // Limitar para que nunca excedamos 20 (lleno) o bajemos de 0 (vacío).
            pointsInThisIcon = Mathf.Clamp(pointsInThisIcon, 0, POINTS_PER_ICON);

            // PASO B: División entera → índice de estado del sprite [0, 4].
            //   0 pts  →  0  (Vacío)
            //   1-5    →  1  (Cuarto)
            //   6-10   →  2  (Medio)
            //   11-15  →  3  (Tres cuartos)
            //   16-20  →  4  (Lleno)
            int stateIndex = pointsInThisIcon / POINTS_PER_QUARTER;

            // Aplicar el sprite correcto al componente Image de esta ranura de icono.
            uiIcons[i].sprite = states[stateIndex];
        }
    }

    // =========================================================================
    // CORAZONES DE SALUD — LÓGICA DE ACTUALIZACIÓN
    // =========================================================================

    /// <summary>
    /// Llamado cada vez que HealthComponent dispara OnHealthChanged.
    /// Convierte el float de salud normalizado de nuevo a un valor HP entero,
    /// luego delega todo el cálculo de icono-sprite a UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Fracción de salud 0.0 (muerto) → 1.0 (lleno).</param>
    private void UpdateHealthBar(float normalized)
    {
        // Necesitamos el componente para leer CurrentHealth como un entero bruto.
        if (playerHealth == null) return;

        // Convertir float normalizado → HP entero.
        // Leemos CurrentHealth directamente (por ejemplo, 65) en lugar de usar el
        // float normalizado para evitar errores de redondeo de punto flotante en
        // los límites exactos de los cuartos de segmento.
        int currentHP = playerHealth.CurrentHealth;

        // Entregar todas las matemáticas al ayudante compartido.
        UpdateFractionalIcons(currentHP, corazonesUI, estadosCorazon);

        // ── JUGO (JUICE) — Animación al curar o recibir daño ──────────────────
        // _previousHealth es -1 en la primera llamada (inicialización),
        // por lo que omitimos las animaciones en ese frame para evitar un parpadeo falso de "curación".
        if (_previousHealth >= 0f)
        {
            if (normalized < _previousHealth)
            {
                // La salud bajó → el jugador recibió daño → parpadear todos los corazones en rojo.
                if (_healthFlash != null) StopCoroutine(_healthFlash);
                _healthFlash = StartCoroutine(FlashIconsRoutine(corazonesUI));
            }
            else if (normalized > _previousHealth && heartsContainer != null)
            {
                // La salud subió → el jugador se curó → escalar el contenedor con un golpe (punch-scale).
                StartCoroutine(PunchScaleRoutine(heartsContainer));
            }
        }

        // Guardar el valor normalizado para la siguiente comparación.
        _previousHealth = normalized;
    }

    // =========================================================================
    // LLAMAS DE MANÁ — LÓGICA DE ACTUALIZACIÓN
    // =========================================================================

    /// <summary>
    /// Llamado cada vez que PlayerResourceComponent dispara OnManaChanged.
    /// Convierte el float de maná normalizado a un entero y delega en
    /// UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Fracción de maná 0.0 (vacío) → 1.0 (lleno).</param>
    private void UpdateManaBar(float normalized)
    {
        if (playerResources == null) return;

        // Convertir float normalizado → puntos de Maná enteros.
        // PlayerResourceComponent.CurrentMana ya es una propiedad entera.
        int currentMana = playerResources.CurrentMana;

        // Entregar todas las matemáticas al ayudante compartido.
        UpdateFractionalIcons(currentMana, manaUI, manaStates);
    }

    // =========================================================================
    // RAYOS DE ENERGÍA — LÓGICA DE ACTUALIZACIÓN
    // =========================================================================

    /// <summary>
    /// Llamado cada vez que PlayerResourceComponent dispara OnEnergyChanged.
    /// Convierte el float de energía normalizado a un entero y delega en
    /// UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Fracción de energía 0.0 (vacío) → 1.0 (lleno).</param>
    private void UpdateEnergyBar(float normalized)
    {
        if (playerResources == null) return;

        // Convertir float normalizado → puntos de Energía enteros.
        int currentEnergy = playerResources.CurrentEnergy;

        // Entregar todas las matemáticas al ayudante compartido.
        UpdateFractionalIcons(currentEnergy, energyUI, energyStates);
    }

    // =========================================================================
    // MANEJADORES DE DESTELLO POR AGOTAMIENTO
    // =========================================================================
    //  Estos son llamados por eventos estáticos cuando un sistema INTENTA gastar un recurso
    //  pero no hay suficiente. Disparan la animación de parpadeo de error "jugo" (juice).

    private void HandleManaDepleted()
    {
        // Guardia: si el arreglo no se ha configurado en el Inspector, no hacer nada.
        if (manaUI == null || manaUI.Length == 0) return;

        // Detener cualquier parpadeo ya en curso antes de comenzar uno nuevo.
        if (_manaFlash != null) StopCoroutine(_manaFlash);
        _manaFlash = StartCoroutine(FlashIconsRoutine(manaUI));
    }

    private void HandleEnergyDepleted()
    {
        // Guardia: si el arreglo no se ha configurado en el Inspector, no hacer nada.
        if (energyUI == null || energyUI.Length == 0) return;

        if (_energyFlash != null) StopCoroutine(_energyFlash);
        _energyFlash = StartCoroutine(FlashIconsRoutine(energyUI));
    }

    // =========================================================================
    // MONEDERO / MONEDAS
    // =========================================================================

    private void HandleCoinsChanged(int newAmount)
    {
        if (_coinText == null) return;
        _coinText.text = newAmount.ToString();

        // Detener cualquier pulso ya en ejecución para que no haya dos rutinas compitiendo.
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseText());
    }

    // =========================================================================
    // CORRUTINAS — ANIMACIONES DE JUGO (JUICE)
    // =========================================================================

    /// <summary>
    /// Cambia instantáneamente TODOS los iconos del arreglo dado al color de parpadeo de error,
    /// luego los restaura a blanco después de <see cref="_flashDuration"/> segundos.
    ///
    /// Este es el mismo concepto que el antiguo FlashBarRoutine / FlashHeartsRoutine,
    /// pero ahora funciona con cualquier arreglo de iconos, por lo que maneja corazones, llamas y
    /// rayos sin duplicar el código de la corrutina.
    /// </summary>
    /// <param name="icons">El arreglo Image de iconos a parpadear.</param>
    private IEnumerator FlashIconsRoutine(Image[] icons)
    {
        // Parpadear todos los iconos en rojo.
        foreach (var img in icons)
            if (img != null) img.color = _errorFlashColor;

        yield return new WaitForSeconds(_flashDuration);

        // Restaurar todos los iconos a blanco (color predeterminado de Image de Unity).
        foreach (var img in icons)
            if (img != null) img.color = Color.white;
    }

    /// <summary>
    /// Escala instantáneamente el texto de monedas hasta <see cref="_pulseScale"/>, luego
    /// realiza un lerp suave de regreso a su escala original durante <see cref="_pulseDuration"/> segundos.
    /// </summary>
    private IEnumerator PulseText()
    {
        if (_coinText == null) yield break;

        Vector3 bigScale = _originalCoinTextScale * _pulseScale;
        _coinText.transform.localScale = bigScale;

        float elapsed = 0f;
        while (elapsed < _pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _pulseDuration);
            _coinText.transform.localScale = Vector3.Lerp(bigScale, _originalCoinTextScale, t);
            yield return null;
        }

        _coinText.transform.localScale = _originalCoinTextScale;
        _pulseCoroutine = null;
    }

    /// <summary>
    /// Escala instantáneamente un RectTransform hasta <see cref="_healPunchScale"/>,
    /// luego realiza un lerp suave de regreso a (1, 1, 1) durante <see cref="_punchDuration"/> segundos.
    /// Se usa para la animación de curación en el contenedor de corazones.
    /// </summary>
    private IEnumerator PunchScaleRoutine(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 bigScale = new Vector3(_healPunchScale, _healPunchScale, _healPunchScale);
        rt.localScale = bigScale;

        float elapsed = 0f;
        while (elapsed < _punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _punchDuration);
            rt.localScale = Vector3.Lerp(bigScale, Vector3.one, t);
            yield return null;
        }

        rt.localScale = Vector3.one;
    }
}