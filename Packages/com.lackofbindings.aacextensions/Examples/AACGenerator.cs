#if UNITY_EDITOR
using System;
using AnimatorAsCode.V1;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using AnimatorAsCode.V1.VRC;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using UnityEditor;
using AnimatorAsCode.V1.VRCDestructiveWorkflow;
using LackofbindingsAAC;

namespace LackofbindingsAAC.Examples
{
    /// <summary>
    /// This is a script that generates an animator using my admittedly janky semi-destructive workflow.
    /// In order to use this, you attach it to an empty in your scene, and supply it with a sacrificial AnimationController.
    /// Every time scripts compile, it will clear out the AnimationController and then generate new state machines and params based on the below code.
    /// You can then put that AnimationController into whatever you want, the GUID will remain constant through generations since it re-uses the same asset.
    /// The idea is to put it into a VRCFury Full Controller component to be merged into an avatar/prop. 
    /// </summary>
    [ExecuteInEditMode]
    public class AACGenerator : MonoBehaviour, IEditorOnly
    {
        public RuntimeAnimatorController controller;
        public string AssetKey;
        public Transform rootTransform;
        public VRCExpressionParameters[] mainPresets;
        public AnimatorController oldController;
        public AnimationClip previewAnimation;
        private AACUtils _utils;
        public VRCExpressionParameters mainParamsList;

        private void OnEnable()
        {
            if (controller == null) return;
            if (rootTransform == null) rootTransform = this.transform;
            if (AssetKey == null || AssetKey.Length == 0) this.AssetKey = UnityEditor.GUID.Generate().ToString();

            // This configuration is only for demonstration purposes. It does not create persistent assets in the project.
            var aac = AacV1.Create(new AacConfiguration
            {
                AnimatorRoot = rootTransform,
                AssetContainer = controller,
                AssetKey = AssetKey,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                SystemName = this.GetType().Name,
                DefaultValueRoot = rootTransform,
                DefaultsProvider = new AacDefaultsProvider(false)
            });

            // Clear out animation controller before starting
            aac.ClearOutController((AnimatorController)controller);

            // Collect references to various avatar parts
            Transform bodyTransform = rootTransform.Find("Body");
            SkinnedMeshRenderer bodySkinnedMeshRenderer = bodyTransform.GetComponent<SkinnedMeshRenderer>();
            Transform faceTransform = rootTransform.Find("Face");
            SkinnedMeshRenderer faceSkinnedMeshRenderer = faceTransform.GetComponent<SkinnedMeshRenderer>();
            Transform hudTransform = rootTransform.Find("Hud");
            SkinnedMeshRenderer hudSkinnedMeshRenderer = hudTransform.GetComponent<SkinnedMeshRenderer>();
            VRCPhysBone earsPhysBone = rootTransform.Find("Physbones/Ears").GetComponent<VRCPhysBone>();

            // Reuse an existing animator controller, and edit its contents
            var layer = aac.CreateMainArbitraryControllerLayer((AnimatorController)controller);

            _utils = new AACUtils(aac, (AnimatorController)controller, layer);

            const string paramPrefixBase = "Example/C";

            var directBlendWeight = layer.FloatParameter($"{paramPrefixBase}/DirectBlendWeight");
            layer.OverrideValue(directBlendWeight, 1f);

            var rootBlendTree = aac.NewBlendTree("Root").Direct();
            layer.NewState("Direct BlendTree").WithWriteDefaultsSetTo(true).WithAnimation(rootBlendTree);

            string[] colors = new string[] { "R", "G", "B", "A" };

            // Set up 4 sub blend trees, one for each material layer on Body
            for (var l = 1; l <= 4; l++)
            {
                var LTree = aac.NewBlendTree($"Body L{l}").Direct();
                rootBlendTree.WithAnimation(LTree, directBlendWeight);

                string paramPrefix = $"{paramPrefixBase}/L{l}";
                string propertyPrefix = "material._RGBAColorMask";

                LTree.WithAnimation(_utils.NewHSVBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Color"), directBlendWeight);
                LTree.WithAnimation(_utils.NewMetallicBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Metallic"), directBlendWeight);
                LTree.WithAnimation(_utils.NewSmoothnessBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Smoothness"), directBlendWeight);
                if (l != 4) LTree.WithAnimation(_utils.NewPatternBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}Map_ST_{colors[l - 1]}"), directBlendWeight);

            }

            // Set up a hue rotation blend tree for the glows
            rootBlendTree.WithAnimation(_utils.NewHueBlendTree(layer.FloatParameter($"{paramPrefixBase}/Glow/Hue"), new[] { bodySkinnedMeshRenderer, faceSkinnedMeshRenderer, hudSkinnedMeshRenderer }, $"material._EmissionColor", 1.0f, true), directBlendWeight);
            rootBlendTree.WithAnimation(_utils.NewHueBlendTree(layer.FloatParameter($"{paramPrefixBase}/Glow/Hue"), new[] { bodySkinnedMeshRenderer }, $"material._EmissionColor1", 1.0f, true), directBlendWeight);

            // Set up toggle for ears
            rootBlendTree.WithAnimation(_utils.NewBlendTreeFromClips(layer.FloatParameter($"{paramPrefixBase}/Ears"), new[] {
                aac.NewClip("Ears Off").Animating(clip => {
                    clip.Animates(earsPhysBone.gameObject).WithOneFrame(0);
                    clip.AnimatesScaleWithOneFrame(earsPhysBone.rootTransform, 0);
                }),
                aac.NewClip("Ears On").Animating(clip => {
                    clip.Animates(earsPhysBone.gameObject).WithOneFrame(1);
                    clip.AnimatesScaleWithOneFrame(earsPhysBone.rootTransform, 1);
                }),
            }), directBlendWeight);

            // Set up toggle for AL Theme Colors
            rootBlendTree.WithAnimation(_utils.NewBlendTreeFromClips(layer.FloatParameter($"{paramPrefixBase}/ALThemeColors"), new[] {
                aac.NewClip("AL Theme Colors Off").Animating(clip => {
                    
                    // var defaultALColorR = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorR");
                    // var defaultALColorG = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorG");
                    // var defaultALColorB = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorB");
                    
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorR").WithOneFrame(defaultALColorR);
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorG").WithOneFrame(defaultALColorG);
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorB").WithOneFrame(defaultALColorB);

                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeR").WithOneFrame(0);
                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeG").WithOneFrame(0);
                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeB").WithOneFrame(0);
                }),
                aac.NewClip("AL Theme Colors On").Animating(clip => {
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorR").WithOneFrame(Color.white);
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorG").WithOneFrame(Color.white);
                    // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorB").WithOneFrame(Color.white);

                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeR").WithOneFrame(1);
                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeG").WithOneFrame(4);
                    clip.Animates(bodySkinnedMeshRenderer, "material._alThemeB").WithOneFrame(2);
                }),
            }), directBlendWeight);

            // Set up main presets
            var presetsLayer = _utils.NewPresetsLayer("Main", paramPrefixBase, mainPresets);

        }

        [ContextMenu("Converts presets from RGB to HSV")]
        public void ConvertPresetsRGBToHSVAction()
        {
            AACUtils.ConvertPresetsRGBToHSV(mainPresets);
        }

        [ContextMenu("Copy Presets to Files")]
        public void CopyPresetsToFilesAction()
        {
            AACUtils.CopyPresetsToFiles(oldController, mainPresets);
        }

        [ContextMenu("Sync Animator Params To Main List")]
        public void SyncAnimatorParamsToListAction()
        {
            AACUtils.SyncAnimatorParamsToList((AnimatorController)controller, mainParamsList);
        }

        [ContextMenu("Generate Animator")]
        public void GenerateAction()
        {
            OnEnable();
        }
    }
}

#endif