
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TopDownShooter.EditorUtils
{
    /// <summary>
    /// Wizard de editor para reemplazar GameObjects en masa manteniendo su 
    /// transformación (posición, rotación, escala) y su jerarquía de padre.
    /// </summary>
    public class PrefabReplacer : ScriptableWizard
    {
        [Tooltip("El nuevo prefab que reemplazará a los objetos seleccionados.")]
        public GameObject newPrefab;

        [MenuItem("Tools/Replace Selected Prefabs")]
        private static void CreateWizard()
        {
            // Abre la ventana del Wizard. Si ya está abierta, la enfoca.
            ScriptableWizard.DisplayWizard<PrefabReplacer>("Replace Selected Prefabs", "Replace");
        }

        private void OnWizardCreate()
        {
            // Validaciones iniciales
            if (newPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a New Prefab in the wizard window before replacing.", "OK");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "No GameObjects are currently selected in the Hierarchy. Select the objects you want to replace first.", "OK");
                return;
            }

            // Agrupar todas las operaciones de Undo en un solo bloque 
            // para que el usuario pueda revertir todos los reemplazos con un solo Ctrl+Z.
            Undo.SetCurrentGroupName("Replace Selected Prefabs");
            int undoGroup = Undo.GetCurrentGroup();

            int replacedCount = 0;

            foreach (GameObject oldObject in selectedObjects)
            {
                // Instanciar el nuevo prefab asegurando que conserve el enlace (Prefab Connection)
                // y asignándole exactamente el mismo padre en la jerarquía.
                GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, oldObject.transform.parent);

                if (newObject == null)
                {
                    Debug.LogWarning($"[PrefabReplacer] Failed to instantiate prefab for {oldObject.name}. Skipping.");
                    continue;
                }

                // Copiar exactamente la transformación local del objeto antiguo
                newObject.transform.localPosition = oldObject.transform.localPosition;
                newObject.transform.localRotation = oldObject.transform.localRotation;
                newObject.transform.localScale    = oldObject.transform.localScale;

                // Mantener el mismo índice entre sus hermanos para que no se mueva al final de la lista
                newObject.transform.SetSiblingIndex(oldObject.transform.GetSiblingIndex());

                // Registrar el nuevo objeto en el sistema de Undo ANTES de destruir el viejo
                Undo.RegisterCreatedObjectUndo(newObject, "Replace Selected Prefabs");

                // Destruir el objeto antiguo registrándolo en el sistema de Undo
                Undo.DestroyObjectImmediate(oldObject);
                
                replacedCount++;
            }

            // Colapsar todo el bloque de Undo
            Undo.CollapseUndoOperations(undoGroup);

            // Seleccionar los nuevos objetos instanciados para conveniencia del usuario
            // (Opcional, pero muy útil para continuar trabajando)
            // No lo hacemos automáticamente aquí porque DestroyObjectImmediate deselecciona asíncronamente, 
            // pero el flujo principal ya está completo.

            Debug.Log($"[PrefabReplacer] Successfully replaced {replacedCount} objects with '{newPrefab.name}'. Press Ctrl+Z to undo if needed.");
        }
    }
}
#endif
