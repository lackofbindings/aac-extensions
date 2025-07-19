using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LackofbindingsAAC
{
    [CreateAssetMenu(fileName = "AssetContainer", menuName = "Lackofbindings/AAC/AssetContainer", order = 281)]
    [PreferBinarySerialization]
    public class AACAssetContainer : ScriptableObject
    {
        private string prefix = "AACAssetContainer";
        private string suffix = "Proxy";

        /// <inheritDoc cref="updateAnimator(string name, AnimatorController animator)" />
        public AnimatorController UpdateAnimator(AnimatorController animator)
        {
            return UpdateAnimator("Main", animator);
        }

        /// <summary>
        /// Updates a named proxy animator sub-asset to match the given animator, creating a new animator sub-asset if it doesn't yet exist.
        /// This allows the result animator to be accessible at consistent GUID so external references wont be lost every generation.
        /// Only overwrites the animator sub-asset with the same name.
        /// </summary>
        /// <param name="animator">The animator to copy from, usually the result of an AAC generation. Input null to delete to proxy.</param>
        /// <returns>The proxy animator, or null if deleted.</returns>
        public AnimatorController UpdateAnimator(string name, AnimatorController animator)
        {
            string ourPath = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
            string savedName = $"{prefix}_{name}_{suffix}";
            AnimatorController proxyAnimator = null;
            foreach (var subAsset in allSubAssets)
            {
                if (subAsset is AnimatorController && ((AnimatorController)subAsset).name == savedName)
                {
                    if (animator == null)
                    {
                        AssetDatabase.RemoveObjectFromAsset(subAsset);
                        Object.DestroyImmediate(subAsset);
                        return null;
                    }
                    else
                    {
                        proxyAnimator = (AnimatorController)subAsset;
                        break;
                    }
                }
            }
            if (proxyAnimator == null)
            {
                proxyAnimator = new AnimatorController();
                AssetDatabase.AddObjectToAsset(proxyAnimator, this);
            }
            EditorUtility.CopySerialized(animator, proxyAnimator);
            proxyAnimator.name = savedName;
            AssetDatabase.ImportAsset(ourPath);
            return proxyAnimator;
        }

        /// <summary>
        /// Extends Reset to also clear sub-assets.
        /// </summary>
        public void Reset()
        {
            string ourPath = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
            for (int i = 0; i < allSubAssets.Length; i++)
            {
                if (allSubAssets[i] is not AACAssetContainer)
                {
                    AssetDatabase.RemoveObjectFromAsset(allSubAssets[i]);
                    Object.DestroyImmediate(allSubAssets[i]);
                }
            }
            AssetDatabase.ImportAsset(ourPath);
        }

    }
}
