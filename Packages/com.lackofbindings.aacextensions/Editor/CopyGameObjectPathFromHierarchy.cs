using UnityEditor;
using UnityEngine;

// Originally By RubenHeeren https://discussions.unity.com/t/please-include-a-copy-path-when-right-clicking-a-game-object/638839/6

namespace LackofbindingsAAC
{
    public static class CopyGameObjectPathFromHierarchy
    {
        [MenuItem("GameObject/Copy Path", false, 11)]
        static void CopyPath()
        {
            GameObject currentGameObject = Selection.activeGameObject;

            if (currentGameObject == null)
                return;

            string path = currentGameObject.name;

            while (currentGameObject.transform.parent != null)
            {
                currentGameObject = currentGameObject.transform.parent.gameObject;

                path = $"{currentGameObject.name}/{path}";
            }

            EditorGUIUtility.systemCopyBuffer = path;
        }

        /// <summary>
        /// Only allow path copying if 1 object is selected.
        /// </summary>
        [MenuItem("GameObject/Copy Path", true)]
        static bool CopyPathValidation() => Selection.gameObjects.Length == 1;
    }
}