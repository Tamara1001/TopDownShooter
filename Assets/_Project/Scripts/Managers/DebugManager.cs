#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugManager : MonoBehaviour
{
    [Header("Debug Panel UI")]
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

        // 1. GLOBAL STATE
        _sb.AppendLine("<color=#FFD700><b>--- GLOBAL STATE ---</b></color>");
        if (GameManager.Instance != null)
        {
            _sb.AppendLine($"FSM State: {FormatValue(GameManager.Instance.CurrentState.ToString())}");
            float t = GameManager.Instance.SessionTime;
            _sb.AppendLine($"Session Time: {FormatValue(string.Format("{0:00}:{1:00}", Mathf.FloorToInt(t / 60), Mathf.FloorToInt(t % 60)))}");
        }
        _sb.AppendLine();

        // 2. PLAYER
        _sb.AppendLine("<color=#FFD700><b>--- PLAYER ---</b></color>");
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            if (player.TryGetComponent<HealthComponent>(out var health))
            {
                _sb.AppendLine($"Health: {FormatValue(health.CurrentHealth)} / {FormatValue(health.MaxHealth)}");
                _sb.AppendLine($"Dead?:  {FormatValue(health.IsDead)}");
            }
            else
            {
                _sb.AppendLine("<color=#FF6B6B>HealthComponent not found on Player.</color>");
            }

            _sb.AppendLine();
            if (player.TryGetComponent<TopDownShooter.Player.PlayerInventory>(out var inventory))
            {
                _sb.AppendLine($"Weapon:     {FormatValue(inventory.CurrentWeapon?.DisplayName ?? "None")}");
                _sb.AppendLine($"Relic:      {FormatValue(inventory.CurrentRelic?.DisplayName ?? "None")}");
                _sb.AppendLine($"Consumable: {FormatValue(inventory.CurrentConsumable?.DisplayName ?? "None")}");
            }
            else
            {
                _sb.AppendLine("<color=#FF6B6B>PlayerInventory not found on Player.</color>");
            }

            _sb.AppendLine("\n<color=#FFD700><b>--- ACTIVE COLLISIONS ---</b></color>");

            Collider[] hits;
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                // Calculamos los centros de las dos esferas de la capsula para OverlapCapsule.
                // El radio se expande 0.1f para detectar contactos rasantes con el suelo.
                Vector3 center = player.transform.position + cc.center;
                float halfOffset = (cc.height / 2f) - cc.radius;

                Vector3 point1 = center + Vector3.up * halfOffset;
                Vector3 point2 = center - Vector3.up * halfOffset;

                hits = Physics.OverlapCapsule(point1, point2, cc.radius + 0.1f);
            }
            else
            {
                hits = Physics.OverlapSphere(player.transform.position + Vector3.up * 0.5f, 1f);
            }

            bool isTouchingSomething = false;
            foreach (Collider hit in hits)
            {
                if (hit.gameObject != player)
                {
                    string isTrigger = hit.isTrigger ? "<color=#FFD93D>[Trigger]</color>" : "<color=#A8A8A8>[Solid]</color>";
                    _sb.AppendLine($"- {hit.gameObject.name} {isTrigger} (Layer: {LayerMask.LayerToName(hit.gameObject.layer)})");
                    isTouchingSomething = true;
                }
            }

            if (!isTouchingSomething)
                _sb.AppendLine("<color=#A8A8A8>No physical contacts.</color>");
        }
        else
        {
            _sb.AppendLine("<color=#FF6B6B>No GameObject found with Tag 'Player'.</color>");
        }

        _sb.Append($"\n<color=#555555>Last refresh: {System.DateTime.Now:HH:mm:ss.ff}</color>");
        variablesText.text = _sb.ToString();
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            bool b   => b ? "<color=#6BCB77>true</color>" : "<color=#FF6B6B>false</color>",
            float f  => $"<color=#FFD93D>{f:0.##}</color>",
            int i    => $"<color=#FFD93D>{i}</color>",
            string s => $"<color=#C3A6FF>\"{s}\"</color>",
            null     => "<color=#A8A8A8>null</color>",
            _        => $"<color=#A8A8A8>{value}</color>"
        };
    }
}
#endif
