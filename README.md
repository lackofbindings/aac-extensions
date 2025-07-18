# Lackofbindings' AAC Extensions

Personal extensions and utilities for Animator As Code.

**Most of these will probably make no sense outside of my specific workflow**, however perhaps they may be helpful as inspiration for your own code.

Take a look at [the example script](Packages/com.lackofbindings.aacextensions/Examples/AACGenerator.cs) for an idea on how to use the utils library.

----

> Hai if you're reading this, turn back now, lest my code make your skeleton cringe out of your body lol.

##  [AACUtils.cs](Packages/com.lackofbindings.aacextensions/Editor/AACUtils.cs) - Basic Usage

1. Add a reference to my Assembly Definition to yours (see [the example](Packages/com.lackofbindings.aacextensions/Examples/)). If you don't already have an Assembly Definition for your code, you should probably make one.
2. Add `using LackofbindingsAAC;` to your script.
3. Somewhere in your script after instantiating AAC and making your first layer, you should create an instance of my utils class and pass in a reference to your AacFlBase, you animation controller, and your main AacFlLayer.  `_utils = new AACUtils(aac, (AnimatorController)controller, layer);`. 
   * These references are all for functions that rely on an existing animator or animator layer, (creating params, animator layers, etc.), but where which exact layer doesn't matter. *(For instance, creating a parameter requires a reference to an animator layer, even though it applies to the entire animator).*
   * Confusingly, AAC uses the word "layer" to refer to both animator layers, as well as animators themselves.
4. You can now use that instance to call any of my utility functions from inside your generator.

## [AACExtensions.cs](Packages/com.lackofbindings.aacextensions/Editor/AACExtensions/AACExtensions.cs)

This package also includes an extension class for AAC that includes a few QoL functions. 

* `AacFlBase.NewBlendTree(string name)` - To create a new blend tree while providing a name like you can with clips.
* `AacFlEditClip.AnimatesScaleWIthOneFrame(Transform transform, float scale)` - To quickly animate uniform scale.
* `AacFlBase.ClearOutController(AnimatorController controller)` - To quickly clear all layers, params, and acc-created sub-assets from a controller. *(Only relevant to my janky workflow)*.