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
        private string suffixAnimator = "Animator";

        /// <summary>
        /// Updates a proxy animator sub-asset to match the given animator, creating a new animator sub-asset if it doesn't yet exist.
        /// This allows the result animator to be accessible at consistent GUID so external references wont be lost every generation.
        /// </summary>
        /// <param name="animator">The animator to copy from, usually the result of an AAC generation</param>
        public void updateAnimator(AnimatorController animator)
        {
            string ourPath = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
            AnimatorController proxyAnimator = null;
            foreach (var subAsset in allSubAssets)
            {
                if (subAsset is AnimatorController && object.ReferenceEquals(subAsset, proxyAnimator))
                {
                    proxyAnimator = (AnimatorController)subAsset;
                    break;
                }
            }
            if (proxyAnimator == null)
            {
                proxyAnimator = new AnimatorController();
                AssetDatabase.AddObjectToAsset(proxyAnimator, this);
            }
            EditorUtility.CopySerialized(animator, proxyAnimator);
            proxyAnimator.name = $"{prefix}_{suffixAnimator}";
            AssetDatabase.ImportAsset(ourPath);
        }

        /// <summary>
        /// Updates a named proxy animator sub-asset to match the given animator, creating a new animator sub-asset if it doesn't yet exist.
        /// This allows the result animator to be accessible at consistent GUID so external references wont be lost every generation.
        /// Only overwrites the animator sub-asset with the same given name.
        /// </summary>
        /// <inheritDoc cref="updateAnimator(AnimatorController animator)" />
        public void updateAnimator(string name, AnimatorController animator)
        {
            string ourPath = AssetDatabase.GetAssetPath(this);
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
            string savedName = $"{prefix}_{name}_{suffixAnimator}";
            AnimatorController proxyAnimator = null;
            foreach (var subAsset in allSubAssets)
            {
                if (subAsset is AnimatorController && ((AnimatorController)subAsset).name == savedName)
                {
                    if (animator == null)
                    {
                        AssetDatabase.RemoveObjectFromAsset(subAsset);
                        Object.DestroyImmediate(subAsset);
                        return;
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
