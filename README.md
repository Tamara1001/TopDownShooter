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
| **Saltar** | `Espacio` | `PlayerController3D` → `OnJump()` |
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

## 📁 Estructura del Proyecto

```
Assets/_Project/Scripts/
│
├── Combat/         → Interfaces (IWeapon, IDamageable, IWorldInteractable)
│                     y armas concretas (RangedWeapon, MeleeWeapon, etc.)
│
├── Dungeon/        → Generación procedural (DungeonGenerator, RoomController,
│                     RoomSocket, DoorController) y ScriptableObjects de configuración
│
├── Enemy/          → Comportamiento de enemigos (BossBrain, etc.)
│
├── Inventory/      → ScriptableObjects de ítems (WeaponDataSO, RelicDataSO, etc.)
│
├── Loot/           → Sistema de drop y recolección de loot
│
├── Managers/       → GameManager (FSM, Singleton), AudioManager, UIManager
│
├── Player/         → PlayerController3D, PlayerCombat, PlayerInventory,
│                     PlayerResourceComponent, PlayerStatsComponent, PlayerHUD
│
├── UI/             → HUD, pantallas de menú, BossHUD
│
└── World/          → Objetos interactuables del mundo
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
| **Alejo Nicolas Warner** | Desarrollo, diseño de sistemas y arquitectura |

---

*Proyecto desarrollado con fines académicos para la carrera de Producción de Simuladores y Videojuegos.*
