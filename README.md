# 🎮 Top-Down Shooter

> **Proyecto Final Universitario** — Prototipo desarrollado en Unity (C#)
> Carrera: **Producción de Simuladores y Videojuegos**

---

**Autores:** Tamara D'Angelo & Alejo Nicolas Warner  
**Motor:** Unity (New Input System · CharacterController · NavMesh)  
**Lenguaje:** C# (.NET Standard 2.1)

---

## 📖 Descripción General

*Top-Down Shooter* es un prototipo de videojuego de acción con perspectiva cenital (top-down) centrado en la exploración de mazmorras generadas proceduralmente. Cada partida construye un mapa único mediante un algoritmo de colocación por cuadrículas, garantizando que ninguna sesión de juego sea igual a la anterior.

El juego pone al jugador en la piel de **Lunaria**, una maga que debe abrirse paso a través de oleadas de enemigos, recoger armas y reliquias, encontrar la **llave** que desbloquea la sala del jefe y derrotarlo para ganar.

### Mecánicas Principales

| Característica | Descripción |
|---|---|
| 🗺️ **Generación Procedural** | Mazmorras construidas en tiempo de ejecución mediante un algoritmo de cuadrícula con conexión por *sockets* cardinales (Norte/Sur/Este/Oeste). |
| 🚪 **Bloqueo de Puertas** | Al entrar a una sala de combate, todas las puertas se cierran. Solo se abren cuando el último enemigo es derrotado. |
| 🗝️ **Sistema de Llave** | La sala del jefe (*Boss Room*) está protegida por una puerta especial que exige al jugador recolectar una llave ubicada en una sala de rama. |
| 💀 **Boss Room** | Sala terminal del camino principal. Contiene un enemigo jefe con barra de vida dedicada en el HUD. |
| 🏆 **Condición de Victoria** | Al derrotar al jefe, se activa una *Victory Door* en su sala. El jugador debe interactuar con ella para ganar la partida. |
| ☠️ **Condición de Derrota** | Al perder toda la salud, el jugador muere y se transiciona al estado *Game Over*. El `GameManager` gestiona ambos desenlaces. |

---

## 🎮 Controles

El juego utiliza el **New Input System** de Unity con el modo *Send Messages*. Los controles están definidos en el asset `CharacterActions.inputactions`.

### Teclado y Ratón

| Acción | Input | Componente Responsable |
|---|---|---|
| **Mover** | `W A S D` | `PlayerController3D` → `OnMove()` |
| **Apuntar** | `Mouse` (posición absoluta en pantalla) | `PlayerController3D` → `OnLook()` |
| **Disparar / Atacar** | `Clic Izquierdo` | `PlayerCombat` → `OnAttack()` |
| **Correr** | `Shift` (mantener) | `PlayerController3D` → `OnSprint()` |
| **Dash** | `Espacio` *(acción "Dash")* | `PlayerController3D` → `OnDash()` |
| **Interactuar / Recoger** | `E` | `PlayerInventory` → `OnInteract()` |
| **Usar Consumible** | `Q` | `PlayerInventory` → `OnConsume()` |

> **Nota sobre el Dash:** El dash consume **Energía** del jugador. Si la barra de energía está vacía, el dash es rechazado silenciosamente y se dispara el evento `OnEnergyDepleted` para notificar al HUD.  
> **Nota sobre el Ataque:** Si el arma equipada requiere **Maná**, cada ataque consume recursos. Sin recursos suficientes, el disparo es abortado y se dispara `OnManaDepleted`.  
> **Nota sobre los Ítems Quest:** Los ítems de misión (como la **Llave**) no pueden usarse con `Q`; solo funcionan al interactuar (`E`) con el objeto de mundo correspondiente.

---

## 🏗️ Arquitectura Técnica Destacada

El proyecto fue desarrollado aplicando principios SOLID y patrones de diseño reconocidos de la industria.

### 1. Singleton — `GameManager`

El `GameManager` es el cerebro central del juego. Implementa el patrón **Singleton** con `DontDestroyOnLoad`, garantizando una única instancia persistente entre escenas.

```
GameManager (Singleton)
│
├── FSM de Estados: MainMenu → Playing → Pause → GameOver / Victory
├── Controla Time.timeScale (única clase autorizada)
├── Registra al jugador en tiempo de ejecución (PlayerTransform)
└── Emite eventos estáticos: OnStateChanged, OnPlayerRegistered
```

**Estados de la FSM:**

| Estado | Descripción |
|---|---|
| `MainMenu` | Estado inicial al arrancar la aplicación. |
| `Playing` | Partida activa; el tiempo avanza normalmente. |
| `Pause` | Tiempo congelado (`timeScale = 0`). |
| `GameOver` | El jugador murió. Tiempo congelado. |
| `Victory` | El jefe fue derrotado y se usó la puerta de victoria. |

---

### 2. Generación Procedural — `DungeonGenerator`

El generador construye la mazmorra en `Start()` usando una **cuadrícula de 20×20 unidades** por sala. El algoritmo garantiza la siguiente estructura narrativa:

```
[Start] ──► [Combat] ──► [Combat] ──► ... ──► [Boss]
                │
                └──► [Key Room]   (rama lateral)
                └──► [Treasure]   (rama lateral)
```

**Flujo del algoritmo:**
1. Coloca la sala de inicio en el origen `(0,0)`.
2. Itera `MainPathLength - 1` pasos; la última sala siempre es la *Boss Room*.
3. Por cada paso, selecciona un socket libre del *frontier* y coloca una sala de combate.
4. Después del camino principal, genera ramas con salas de *Llave* y *Tesoro*.
5. Coloca la *Victory Door* en el socket libre de la Boss Room.
6. Hornea el **NavMesh** en tiempo de ejecución para la IA de los enemigos.

Los *sockets* de la Boss Room son intencionalmente **excluidos del frontier**, garantizando que sea siempre un callejón sin salida.

---

### 3. Sistema de Interacción Desacoplado — `IWorldInteractable`

Cualquier objeto del mundo que responda a la tecla `E` implementa la interfaz `IWorldInteractable`. Esto mantiene `PlayerInventory` completamente agnóstico respecto a los tipos concretos (puertas, interruptores, NPCs).

```csharp
// Contrato mínimo para cualquier objeto interactuable del mundo
public interface IWorldInteractable
{
    void Interact(PlayerInventory inventory);
}
```

**Prioridad en `OnInteract()`:**
1. `TryWorldInteract()` — Busca un `IWorldInteractable` en radio (puertas, switches).
2. `TryPickupNearestItem()` — Fallback para recoger ítems del suelo.

---

### 4. Patrón Strategy — Sistema de Armas

`PlayerCombat` actúa como **Context** del patrón *Strategy*: delega toda la lógica de ataque a la interfaz `IWeapon`, sin conocer la implementación concreta.

```
PlayerCombat (Context)
    └── IWeapon (Strategy Interface)
            ├── RangedWeapon        (disparo simple)
            ├── SpreadRangedWeapon  (disparo en abanico)
            └── MeleeWeapon         (cuerpo a cuerpo)
```

El cambio de arma ocurre dinámicamente al recoger un ítem: `PlayerInventory` emite `OnWeaponChanged → WeaponDataSO`, `PlayerCombat` destruye el hijo viejo e instancia el nuevo prefab de lógica, y opcionalmente llama `IWeaponConfigurable.Configure()` para inyectar las estadísticas desde el ScriptableObject.

---

### 5. ScriptableObjects — Datos Desacoplados del Código

Toda la configuración de ítems y mazmorras vive en **ScriptableObjects**, separando datos de comportamiento:

| SO | Responsabilidad |
|---|---|
| `WeaponDataSO` | Estadísticas del arma: daño, cadencia, tipo de recurso (Maná/Energía), costo, prefab de lógica. |
| `RelicDataSO` | Modificadores pasivos que afectan a `PlayerStatsComponent` (velocidad, etc.). |
| `ConsumableDataSO` | Curación, duración del efecto, multiplicador de velocidad. Soporta ítems tipo quest. |
| `DungeonConfigSO` | Pool de salas disponibles, longitud del camino principal, límite de ramas. |
| `RoomDataSO` | Prefab de sala, tipo (`RoomType`), peso de selección aleatoria ponderada. |

---

### 6. Inventario de 3 Slots — `PlayerInventory`

El jugador dispone de exactamente tres slots fijos:

```
┌─────────┐  ┌──────────┐  ┌────────────┐
│  ARMA   │  │ RELIQUIA │  │ CONSUMIBLE │
│WeaponSO │  │ RelicSO  │  │ConsumableSO│
└─────────┘  └──────────┘  └────────────┘
```

**Swap atómico:** Al recoger un ítem cuando el slot está ocupado, el ítem anterior se *dropea* en el mundo (instancia el `DropPrefab` frente al jugador) y el nuevo ocupa el slot. Los eventos `OnWeaponChanged / OnRelicChanged / OnConsumableChanged` notifican al HUD y demás sistemas sin acoplamiento directo.

---

### 7. Sistema de Recursos — `PlayerResourceComponent`

El jugador gestiona dos recursos independientes que actúan como costos de habilidad:

| Recurso | Uso |
|---|---|
| ⚡ **Energía** | Consumida por el Dash (`PlayerController3D`). |
| 🔵 **Maná** | Consumida por armas de tipo `WeaponResourceType.Mana`. |

Si un recurso es insuficiente, la acción es rechazada y se dispara un evento estático (`OnEnergyDepleted` / `OnManaDepleted`) para que el HUD pueda mostrar feedback visual sin acoplamientos.

---

### 8. Sistema DungeonMaster — `DungeonMasterDirector`

El **DungeonMaster** es una capa de variabilidad roguelite que se activa automáticamente cada vez que el jugador entra en una sala de combate. Implementa el patrón **Singleton** con `DontDestroyOnLoad` para mantener su estado entre salas sin necesidad de búsquedas en escena.

**Flujo de ejecución por sala:**

```
RoomController.OnRoomActivated()
    └── DungeonMasterDirector.TriggerRoomRoll(room, player)
            ├── 1. Tira D20 → Random.Range(1, 21)
            ├── 2. Emite OnDiceRolled(int) → HUD anima el dado
            ├── 3. Clasifica el resultado en un tier
            └── 4. Selecciona y aplica un DungeonModifierSO del pool

RoomController.OnRoomCleared()
    └── DungeonMasterDirector.ClearActiveModifier()
            ├── RevertModifier() sobre jugador y enemigos
            └── Emite OnModifierCleared → HUD oculta el banner
```

**Tabla de tiers del D20:**

| Resultado | Tier | Efecto |
|---|---|---|
| **1** | Fallo Crítico | Penalización severa al jugador (`criticalFailures`). |
| **2–6** | Mal Tiro | Penalización leve o ventaja para enemigos (`badRolls`). |
| **7–14** | Normal | Sin modificador activo. No-op silencioso. |
| **15–19** | Buen Tiro | Ventaja moderada para el jugador (`goodRolls`). |
| **20** | Éxito Crítico | Ventaja poderosa o efecto espectacular (`criticalSuccesses`). |

Cada pool es una `List<DungeonModifierSO>` configurada en el Inspector. `DungeonModifierSO` es una clase base abstracta con dos métodos: `ApplyModifier(player, enemies)` y `RevertModifier(player, enemies)`. Las implementaciones concretas — `PlayerStatsModifier` y `EnemyStatsModifier` — modifican estadísticas a través de `PlayerStatsComponent` y `EnemyStatsSO` respectivamente, y las restauran exactamente al revertir. El Director cachea la referencia al modificador activo (`_activeModifier`) para garantizar que el revert siempre opere sobre el mismo objeto sin depender del estado del pool en tiempo de ejecución.

**Eventos estáticos** (cualquier sistema puede suscribirse sin acoplamiento):

| Evento | Parámetro | Uso |
|---|---|---|
| `OnDiceRolled` | `int` (1–20) | `DungeonMasterHUD` anima el dado antes de mostrar el efecto. |
| `OnModifierApplied` | `string` (nombre del efecto o `Empty`) | `DungeonMasterHUD` muestra el banner con el nombre del modificador. |
| `OnModifierCleared` | — | `DungeonMasterHUD` oculta el banner al limpiar la sala. |

---

### 9. Efectos Visuales — `VFX` y Sistemas de Armas

#### 9.1 Rastro de Proyectiles — `WeaponDataSO` + `Projectile.SetColor()`

Cada `WeaponDataSO` expone un campo público `projectileTrailColor` (`Color`). Al equipar un arma, `PlayerCombat` llama a `IWeaponConfigurable.Configure(stats)` en el prefab de lógica recién instanciado. Dentro de `RangedWeapon.Configure()`, el color es cacheado localmente en `_projectileColor` sin mantener una referencia viva al SO:

```csharp
// RangedWeapon.Configure()  — llamado una sola vez al equipar
_damage           = stats.BaseDamage;
_baseCooldown     = stats.AttackCooldown;
_projectileColor  = stats.ProjectileTrailColor;  // Color cacheado, SO liberado
```

En cada ciclo de disparo, el delegado `actionOnGet` del pool (`OnGetProjectile`) invoca `projectile.SetColor(_projectileColor)`. Este método aplica el color al `SpriteRenderer` y construye un `Gradient` programático para el `TrailRenderer`, creando una estela que va del color opaco al frente hasta totalmente transparente al final:

```csharp
// Projectile.SetColor() — construye el degradado en cada disparo
gradient.SetKeys(
    colorKeys:  { (color, 0f), (color, 1f) },
    alphaKeys:  { (color.a, 0f), (0f, 1f) }   // opaco → transparente
);
_trailRenderer.colorGradient = gradient;
```

Ambos renderers (`_spriteRenderer`, `_trailRenderer`) se resuelven de forma perezosa mediante `GetComponentInChildren` en la primera llamada y se cachean para las siguientes, evitando asignaciones por frame. Si el prefab no tiene alguno de los componentes, el método es un no-op parcial seguro.

#### 9.2 Resplandor de Ítems — `ItemGlowEffect`

Todo ítem recolectable en el suelo puede llevar el componente `ItemGlowEffect`. En `Awake()`, busca un `Light` existente en la jerarquía; si no lo encuentra, crea un `GameObject` hijo (`GlowLight`) y le agrega el componente en código, elevándolo `0.5f` unidades sobre el suelo para evitar intersección con la geometría.

El pulso de intensidad usa una función seno con offset de fase aleatorio por instancia:

```csharp
// ItemGlowEffect.PulseIntensity() — ejecutado en Update()
float sinValue   = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f + _phaseOffset);
float normalized = (sinValue + 1f) * 0.5f;   // [-1,1] → [0,1]
_light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, normalized);
```

El offset de fase (`_phaseOffset = Random.Range(0, 2π)`) garantiza que varios ítems en la misma sala pulsen de forma asíncrona, eliminando el artefacto de "parpadeo grupal". Todos los parámetros estáticos (color, rango, sombras) se configuran una sola vez en `ConfigureLight()` llamado desde `Awake`, no en `Update`.

#### 9.3 Feedback de Emisión con `MaterialPropertyBlock` — `LockedBossDoor` y `VictoryDoor`

Los objetos de mundo con feedback visual dinámico usan `MaterialPropertyBlock` para modificar la propiedad `_EmissionColor` del shader en tiempo de ejecución, evitando la instanciación de nuevos `Material` (que generaría fugas de memoria y contaminaría el asset database):

```csharp
// LockedBossDoor.SetEmission() — llamado por OnPlayerApproach / FlashDenyColor
_renderers[i].GetPropertyBlock(_propBlock);
_propBlock.SetColor(EmissionColorID, color);
_renderers[i].SetPropertyBlock(_propBlock);
```

El ID de propiedad `EmissionColorID` se obtiene con `Shader.PropertyToID("_EmissionColor")` como campo estático, evitando búsquedas por nombre (string hash) en cada frame. `LockedBossDoor` implementa además un ciclo de parpadeo de denegación vía corrutina (`FlashDenyColor`) que hace una rampa ascendente hacia `_denyFlashColor` (rojo HDR) y luego desciende a negro, todo en `_flashDuration` segundos.

---

### 10. Herramienta de Editor — `PrefabReplacer`

`PrefabReplacer` es un `ScriptableWizard` accesible desde **Tools → Replace Selected Prefabs** que permite sustituir en masa cualquier cantidad de GameObjects seleccionados en la jerarquía por un nuevo prefab, conservando exactamente su transformación local y su posición en el orden de hermanos.

**Flujo de operación:**

```
1. Editor abre el Wizard (DisplayWizard<PrefabReplacer>)
2. Usuario asigna newPrefab y hace clic en "Replace"
3. OnWizardCreate() itera Selection.gameObjects:
   a. PrefabUtility.InstantiatePrefab(newPrefab, oldObject.transform.parent)
      → mantiene Prefab Connection y mismo padre en la jerarquía
   b. Copia localPosition, localRotation, localScale del objeto viejo
   c. SetSiblingIndex(oldObject.transform.GetSiblingIndex())
      → preserva el orden en la lista de hermanos
   d. Undo.RegisterCreatedObjectUndo(newObject)
   e. Undo.DestroyObjectImmediate(oldObject)
4. Undo.CollapseUndoOperations(undoGroup)
   → todo el batch queda reversible con un solo Ctrl+Z
```

Toda la operación se agrupa en un único bloque de `Undo` mediante `Undo.SetCurrentGroupName()` + `Undo.CollapseUndoOperations()`. Esto garantiza que un batch de 50 reemplazos pueda revertirse con un solo `Ctrl+Z`, sin saturar el historial de Unity. El uso de `PrefabUtility.InstantiatePrefab` en lugar de `Instantiate` preserva el vínculo con el prefab original en el asset database, lo que permite modificar el prefab más adelante y que las instancias reflejen los cambios.

---

### 11. Expansiones del Generador de Mazmorras

#### 11.1 Soporte para Salas Multi-Celda — `RoomDataSO.Footprint`

El generador original asumía una huella de exactamente `1×1` celda por sala. La versión actual introduce el campo `List<Vector2Int> Footprint` en `RoomDataSO`, que define el conjunto de celdas locales que ocupa la sala. Una sala de `1×1` tiene `Footprint = [(0,0)]`; una sala de `2×1` tendría `[(0,0), (1,0)]`.

El algoritmo de validación `TryFitRoom()` itera **todos** los sockets de la dirección opuesta disponibles en el prefab candidato. Por cada socket, calcula un `roomOrigin` independiente y verifica que **ninguna** celda de la huella colisione con `_occupiedCells` (un `HashSet<Vector2Int>` con lookup O(1)):

```csharp
// TryFitRoom() — itera todas las alineaciones posibles
foreach (RoomSocket candidateSocket in matchingSockets)
{
    Vector2Int candidateOrigin = frontierSocket.TargetGridPos - candidateSocket.LocalGridPosition;
    bool fits = data.Footprint.All(local => !_occupiedCells.Contains(candidateOrigin + local));
    if (fits) { roomOrigin = candidateOrigin; return true; }
}
```

En salas `1×1`, el comportamiento es idéntico al generador original (exactamente un socket y una alineación posible). En salas multi-celda, el mismo método prueba múltiples alineaciones antes de descartar la sala, maximizando las posibilidades de colocación sin retroceso complejo.

#### 11.2 Puertas Tipadas por Sala de Destino — `ConnectSockets()`

El generador expone cuatro prefabs de puerta distintos en el Inspector: `_doorPrefab` (genérico), `_doorPrefabBoss`, `_doorPrefabTreasure` y `_doorPrefabKey`. La selección se realiza en `ConnectSockets()` basándose exclusivamente en el `RoomType` del prefab de sala de **destino**:

```csharp
// ConnectSockets() — el tipo de la sala nueva determina el prefab de puerta
RoomType targetType = newRoom.Type;
GameObject selectedDoorPrefab = targetType switch
{
    RoomType.Boss     => _doorPrefabBoss,
    RoomType.Treasure => _doorPrefabTreasure,
    RoomType.Key      => _doorPrefabKey,
    _                 => _doorPrefab
};
```

Este diseño garantiza que el prefab de sala sea la **única fuente de verdad** sobre qué puerta visual debe aparecer en su entrada. El algoritmo de generación no necesita conocer las reglas visuales; simplemente conecta sockets y el tipo hace el resto.

#### 11.3 Garantía de Callejón Sin Salida para la Boss Room

Los sockets de la sala Boss son **intencionalmente retenidos** del frontier al terminar el camino principal. La llamada `RegisterOpenSockets(newRoom, roomOrigin)` se omite cuando `isFinalRoom == true`:

```csharp
// DungeonGenerator.Generate() — paso 8 del loop de camino principal
if (!isFinalRoom)
    RegisterOpenSockets(newRoom, roomOrigin);
// ↑ Los sockets de la Boss Room nunca entran al frontier →
//   GenerateBranches() no puede adjuntar salas a ella
```

Esto garantiza estructuralmente que la Boss Room sea siempre un callejón sin salida terminal, sin ninguna sala de rama adjunta, independientemente de la longitud del camino o la cantidad de ramas configuradas.

---

## 📁 Estructura del Proyecto

```
Assets/_Project/Scripts/
│
├── Combat/         → Interfaces (IWeapon, IDamageable, IWorldInteractable)
│                     y armas concretas (RangedWeapon, MeleeWeapon, SpreadRangedWeapon)
│
├── Dungeon/        → Generación procedural (DungeonGenerator, RoomController,
│                     RoomSocket, DoorController) y ScriptableObjects de configuración
│                     (DungeonConfigSO, RoomDataSO). Soporte para salas multi-celda
│                     y puertas tipadas por destino.
│
├── DungeonMaster/  → Sistema D20 de variabilidad roguelite (DungeonMasterDirector,
│                     DungeonModifierSO, Modifiers/, UI/DungeonMasterHUD)
│
├── Editor/         → Herramientas de editor Unity (PrefabReplacer — Wizard de
│                     reemplazo masivo con soporte Undo atómico)
│
├── Enemy/          → Comportamiento de enemigos (BossBrain, EnemyBrain, FSM de
│                     estados: Idle/Chase/Attack/Dead, BossPhase2, BossTransition)
│
├── Inventory/      → ScriptableObjects de ítems (WeaponDataSO, RelicDataSO,
│                     ConsumableDataSO, ItemDataSO) y lógica de recogida (ItemPickup)
│
├── Loot/           → Sistema de drop y recolección de loot (LootDropper,
│                     BouncyLoot, CoinCollectible, HealthCollectible, AutoPickupTrigger)
│
├── Managers/       → GameManager (FSM, Singleton), AudioManager, UIManager,
│                     TutorialManager, DebugManager, UI/BossHUD
│
├── Player/         → PlayerController3D, PlayerCombat, PlayerInventory,
│                     PlayerResourceComponent, PlayerStatsComponent, PlayerHUD,
│                     PlayerWallet, PlayerAnimator, PlayerDamageFeedback
│
├── UI/             → MainMenuController
│
├── VFX/            → Efectos visuales de juego (ItemGlowEffect — pulso senoidal
│                     de luz puntual; MeleeSlashEffect — corrutina de escala/alpha)
│
└── World/          → Objetos interactuables del mundo (LockedBossDoor —
                      MaterialPropertyBlock + corrutina de parpadeo, VictoryDoor)
```

---

## ⚙️ Requisitos para Ejecutar

- **Unity:** 2022.3 LTS o superior
- **Paquetes requeridos:**
  - `com.unity.inputsystem` (New Input System)
  - `com.unity.ai.navigation` (NavMesh para IA de enemigos)
- **Plataforma:** PC (Windows / macOS / Linux)

---

## 🚀 Cómo Iniciar el Juego

1. Abrir el proyecto en Unity Hub.
2. Cargar la escena principal desde `Assets/_Project/Scenes/`.
3. Presionar **Play** en el editor, o generar una build desde *File → Build Settings*.
4. En el menú principal, presionar **Jugar** para iniciar una nueva partida con una mazmorra generada proceduralmente.

---

## 👩‍💻 Autores

| Autor | Rol |
|---|---|
| **Tamara D'Angelo** | Desarrollo, diseño de sistemas y arquitectura |
| **Alejo Nicolas Warner** | Desarrollo, artista técnico |

Agradecimientos especiales a: Lucio Riccobono (Arte 2D), Santiago Leal Britos (Audio) y Mateo Molina (Early Game Design)
Ellos ayudaron en una version preliminar de este proyecto que fue realizada en Construct 3.

---

*Proyecto desarrollado con fines académicos para la carrera de Producción de Simuladores y Videojuegos.*
