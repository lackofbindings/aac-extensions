using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetContainer", menuName = "Lackofbindings/AAC/AssetContainer", order = 281)]
[PreferBinarySerialization]
public class AACAssetContainer : ScriptableObject
{
    [SerializeField]
    [HideInInspector]
    private AnimatorController _dummyAnimator;

    /// <summary>
    /// Updates the dummy animator sub-asset to match the given animator, creating a new animator sub-asset if it doesn't yet exist.
    /// This allows the result animator to be accessible at consistent GUID so external references wont be lost every generation.
    /// </summary>
    /// <param name="animator">The animator to copy from, usually the result of an AAC generation</param>
    public void updateMainAnimator(AnimatorController animator)
    {
        bool found = false;
        string ourPath = AssetDatabase.GetAssetPath(this);
        var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(ourPath);
        foreach (var subAsset in allSubAssets)
        {
            if (object.ReferenceEquals(subAsset, _dummyAnimator))
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            _dummyAnimator = new AnimatorController();
            AssetDatabase.AddObjectToAsset(_dummyAnimator, this);
        }
        EditorUtility.CopySerialized(animator, _dummyAnimator);
        _dummyAnimator.name = this.name + "Animator";
        AssetDatabase.ImportAsset(ourPath);
    }

    /// <summary>
    /// Extends Reset to also clear sub-assets.
    /// </summary>
    public void Reset()
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
        AssetDatabase.ImportAsset(ourPath);
    }

}
