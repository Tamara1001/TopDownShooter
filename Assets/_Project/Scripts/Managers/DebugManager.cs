#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugManager : MonoBehaviour
{
    [Header("UI del Panel")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TextMeshProUGUI variablesText;
    [SerializeField] private float watcherRefreshRate = 0.5f;

    private Coroutine _watcherCoroutine;
    private readonly StringBuilder _sb = new StringBuilder(512);

    private void Awake()
    {
        if (debugPanel != null) debugPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
           (Keyboard.current.f12Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame))
        {
            TogglePanel();
        }
    }

    private void TogglePanel()
    {
        if (debugPanel == null) return;
        bool isActive = !debugPanel.activeSelf;
        debugPanel.SetActive(isActive);

        if (isActive)
        {
            RefreshVariableDisplay();
            _watcherCoroutine = StartCoroutine(AutoRefreshRoutine());
        }
        else if (_watcherCoroutine != null)
        {
            StopCoroutine(_watcherCoroutine);
            _watcherCoroutine = null;
        }
    }

    private IEnumerator AutoRefreshRoutine()
    {
        var interval = new WaitForSeconds(watcherRefreshRate);
        while (debugPanel.activeSelf)
        {
            yield return interval;
            RefreshVariableDisplay();
        }
    }

    private void RefreshVariableDisplay()
    {
        if (variablesText == null) return;
        _sb.Clear();

        // 1. ESTADO GLOBAL
        _sb.AppendLine("<color=#FFD700><b>--- ESTADO GLOBAL ---</b></color>");
        if (GameManager.Instance != null)
        {
            _sb.AppendLine($"Estado FSM: {FormatValue(GameManager.Instance.CurrentState.ToString())}");
            float t = GameManager.Instance.SessionTime;
            _sb.AppendLine($"Tiempo Jugado: {FormatValue(string.Format("{0:00}:{1:00}", Mathf.FloorToInt(t / 60), Mathf.FloorToInt(t % 60)))}");
        }
        _sb.AppendLine();

        // 2. JUGADOR
        _sb.AppendLine("<color=#FFD700><b>--- JUGADOR ---</b></color>");
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            // Estado de Salud
            if (player.TryGetComponent<HealthComponent>(out var health))
            {
                _sb.AppendLine($"Salud: {FormatValue(health.CurrentHealth)} / {FormatValue(health.MaxHealth)}");
                _sb.AppendLine($"Muerto?: {FormatValue(health.IsDead)}");
            }
            else
            {
                _sb.AppendLine("<color=#FF6B6B>No se encontró HealthComponent en Player.</color>");
            }

            // Estado del Inventario
            _sb.AppendLine();
            if (player.TryGetComponent<TopDownShooter.Player.PlayerInventory>(out var inventory))
            {
                _sb.AppendLine($"Arma:        {FormatValue(inventory.CurrentWeapon?.DisplayName ?? "Ninguna")}");
                _sb.AppendLine($"Reliquia:    {FormatValue(inventory.CurrentRelic?.DisplayName ?? "Ninguna")}");
                _sb.AppendLine($"Consumible:  {FormatValue(inventory.CurrentConsumable?.DisplayName ?? "Ninguno")}");
            }
            else
            {
                _sb.AppendLine("<color=#FF6B6B>No se encontró PlayerInventory en Player.</color>");
            }

            // Escáner Físico de Colisiones (Modo Cápsula Profesional)
            _sb.AppendLine("\n<color=#FFD700><b>--- COLISIONES ACTIVAS ---</b></color>");

            Collider[] hits;
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                // Calculamos los centros de las dos esferas que forman la cápsula
                Vector3 center = player.transform.position + cc.center;
                float halfOffset = (cc.height / 2f) - cc.radius;

                Vector3 point1 = center + Vector3.up * halfOffset;  // Esfera superior (Cabeza)
                Vector3 point2 = center - Vector3.up * halfOffset;  // Esfera inferior (Pies)

                // Usamos el radio real del jugador + 0.1f de margen para detectar roces con el suelo
                hits = Physics.OverlapCapsule(point1, point2, cc.radius + 0.1f);
            }
            else
            {
                // Fallback de seguridad por si el jugador pierde su CharacterController
                hits = Physics.OverlapSphere(player.transform.position + Vector3.up * 0.5f, 1f);
            }

            // Procesar e imprimir las colisiones detectadas
            bool isTouchingSomething = false;
            foreach (Collider hit in hits)
            {
                // Ignoramos al jugador mismo para no ensuciar el log
                if (hit.gameObject != player)
                {
                    string isTrigger = hit.isTrigger ? "<color=#FFD93D>[Trigger]</color>" : "<color=#A8A8A8>[Solid]</color>";
                    _sb.AppendLine($"- {hit.gameObject.name} {isTrigger} (Layer: {LayerMask.LayerToName(hit.gameObject.layer)})");
                    isTouchingSomething = true;
                }
            }

            if (!isTouchingSomething)
            {
                _sb.AppendLine("<color=#A8A8A8>No está tocando nada físico.</color>");
            }
        }
        else
        {
            _sb.AppendLine("<color=#FF6B6B>No se encontró GameObject con Tag 'Player'.</color>");
        }

        _sb.Append($"\n<color=#555555>Último refresco: {System.DateTime.Now:HH:mm:ss.ff}</color>");
        variablesText.text = _sb.ToString();
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            bool b => b ? "<color=#6BCB77>true</color>" : "<color=#FF6B6B>false</color>",
            float f => $"<color=#FFD93D>{f:0.##}</color>",
            int i => $"<color=#FFD93D>{i}</color>",
            string s => $"<color=#C3A6FF>\"{s}\"</color>",
            null => "<color=#A8A8A8>null</color>",
            _ => $"<color=#A8A8A8>{value}</color>"
        };
    }
}
#endif