using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetContainer", menuName = "ScriptableObjects/AACAssetContainer", order = 1)]
[PreferBinarySerialization]
public class AACAssetContainer : ScriptableObject
{
    /// <summary>
    /// Clears out all sub-assets and re-assigns the ScriptableObject as the main asset.
    /// Call this before starting work (re)generating an animator.
    /// </summary>
    public void ResetContainer()
    {
        string ourPath = AssetDatabase.GetAssetPath(this);
        var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
        foreach (var subAsset in allSubAssets)
        {
            if (subAsset is not AACAssetContainer)
            {
                AssetDatabase.RemoveObjectFromAsset(subAsset);
            }
        }

        AssetDatabase.SetMainObject(this, ourPath);
        AssetDatabase.ImportAsset(ourPath);
    }

    /// <summary>
    /// Finds the first animator sub-asset and sets it as the main asset.
    /// This allows dragging the asset into slots designed for animators. GUID should be consistent.
    /// </summary>
    public void ToAnimator()
    {
        string ourPath = AssetDatabase.GetAssetPath(this);
        var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
        foreach (var subAsset in allSubAssets)
        {
            if (subAsset is AnimatorController || subAsset is RuntimeAnimatorController)
            {
                AssetDatabase.SetMainObject(subAsset, ourPath);
                AssetDatabase.ImportAsset(ourPath);
                break;
            }
        }

    }
}
