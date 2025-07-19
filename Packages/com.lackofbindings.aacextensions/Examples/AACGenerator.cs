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
    /// This is a script that generates an animator using my admittedly strange workflow.
    /// In order to use this, you attach it to an empty in your scene, and supply it with an ACCAssetContainer (ScriptableAsset also included in this library).
    /// Every time scripts compile, it will clear out the asset container and then generate a new animator (and other sub assets) into it based on the below code.
    /// AACAssetContainer provides updateMainAnimator() that will copy the provided animator to its internal animator, as to keep the GUID constant between generations.
    /// You can then put that AnimationController into whatever you want.
    /// The idea is to put it into a VRCFury Full Controller component to be merged into an avatar/prop. 
    /// </summary>
    [ExecuteInEditMode]
    public class AACGenerator : MonoBehaviour, IEditorOnly
    {
        private AacFlController _controller;
        public AACAssetContainer assetContainer;
        public string AssetKey;
        public Transform rootTransform;
        public VRCExpressionParameters[] mainPresets;
        public AnimatorController oldController;
        private AACUtils _utils;
        public VRCExpressionParameters mainParamsList;

        private void OnEnable()
        {
            if (assetContainer == null) return;
            if (rootTransform == null) rootTransform = this.transform;
            if (AssetKey == null || AssetKey.Length == 0) this.AssetKey = UnityEditor.GUID.Generate().ToString();

            var aac = AacV1.Create(new AacConfiguration
            {
                AnimatorRoot = rootTransform,
                AssetContainer = assetContainer,
                AssetKey = AssetKey,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                SystemName = this.GetType().Name,
                DefaultValueRoot = rootTransform,
                DefaultsProvider = new AacDefaultsProvider(false)
            });

            // Clear out asset container before starting
            aac.ClearPreviousAssetsAll();

            // Collect references to various avatar parts
            SkinnedMeshRenderer bodySkinnedMeshRenderer = rootTransform.Find("Body")?.GetComponent<SkinnedMeshRenderer>();
            VRCPhysBone tailPhysBone = rootTransform.Find("Dynamics/PhysBones/Tail")?.GetComponent<VRCPhysBone>();

            _controller = aac.NewAnimatorController("Example FX");
            var layer = _controller.NewLayer();

            _utils = new AACUtils(aac, _controller, layer);

            const string paramPrefixBase = "Example/AAC";

            var directBlendWeight = layer.FloatParameter($"{paramPrefixBase}/DirectBlendWeight");
            layer.OverrideValue(directBlendWeight, 1f);

            var rootBlendTree = aac.NewBlendTree("Root").Direct();
            layer.NewState("Direct BlendTree").WithWriteDefaultsSetTo(true).WithAnimation(rootBlendTree);

            string[] colors = new string[] { "R", "G", "B", "A" };

            // Set up 4 sub blend trees, one for each material layer on Body
            // for (var l = 1; l <= 4; l++)
            // {
            //     var LTree = aac.NewBlendTree($"Body L{l}").Direct();
            //     rootBlendTree.WithAnimation(LTree, directBlendWeight);

            //     string paramPrefix = $"{paramPrefixBase}/L{l}";
            //     string propertyPrefix = "material._RGBAColorMask";

            //     LTree.WithAnimation(_utils.NewHSVBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Color"), directBlendWeight);
            //     LTree.WithAnimation(_utils.NewMetallicBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Metallic"), directBlendWeight);
            //     LTree.WithAnimation(_utils.NewSmoothnessBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}{colors[l - 1]}Smoothness"), directBlendWeight);
            //     if (l != 4) LTree.WithAnimation(_utils.NewPatternBlendTree(paramPrefix, bodySkinnedMeshRenderer, $"{propertyPrefix}Map_ST_{colors[l - 1]}"), directBlendWeight);

            // }
            {
                var tree = aac.NewBlendTree($"Body Material").Direct();
                rootBlendTree.WithAnimation(tree, directBlendWeight);

                string paramPrefix = $"{paramPrefixBase}/Body";
                string propertyPrefix = "material._Color";

                tree.WithAnimation(_utils.NewHSVBlendTree(paramPrefix, bodySkinnedMeshRenderer, propertyPrefix), directBlendWeight);
            }

            // Set up a hue rotation blend tree for the glows
            // rootBlendTree.WithAnimation(_utils.NewHueBlendTree(layer.FloatParameter($"{paramPrefixBase}/Glow/Hue"), new[] { bodySkinnedMeshRenderer, faceSkinnedMeshRenderer, hudSkinnedMeshRenderer }, $"material._EmissionColor", 1.0f, true), directBlendWeight);
            // rootBlendTree.WithAnimation(_utils.NewHueBlendTree(layer.FloatParameter($"{paramPrefixBase}/Glow/Hue"), new[] { bodySkinnedMeshRenderer }, $"material._EmissionColor1", 1.0f, true), directBlendWeight);

            // Set up toggle for tail
            rootBlendTree.WithAnimation(_utils.NewBlendTreeFromClips(layer.FloatParameter($"{paramPrefixBase}/Tail"), new[] {
                aac.NewClip("Tail Off").Animating(clip => {
                    clip.Animates(tailPhysBone.gameObject).WithOneFrame(0);
                    clip.AnimatesScaleWithOneFrame(tailPhysBone.rootTransform, 0);
                }),
                aac.NewClip("Tail On").Animating(clip => {
                    clip.Animates(tailPhysBone.gameObject).WithOneFrame(1);
                    clip.AnimatesScaleWithOneFrame(tailPhysBone.rootTransform, 1);
                }),
            }), directBlendWeight);

            // Set up toggle for AL Theme Colors
            // rootBlendTree.WithAnimation(_utils.NewBlendTreeFromClips(layer.FloatParameter($"{paramPrefixBase}/ALThemeColors"), new[] {
            //     aac.NewClip("AL Theme Colors Off").Animating(clip => {

            //         // var defaultALColorR = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorR");
            //         // var defaultALColorG = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorG");
            //         // var defaultALColorB = bodySkinnedMeshRenderer.sharedMaterials[0].GetColor("_alColorB");

            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorR").WithOneFrame(defaultALColorR);
            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorG").WithOneFrame(defaultALColorG);
            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorB").WithOneFrame(defaultALColorB);

            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeR").WithOneFrame(0);
            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeG").WithOneFrame(0);
            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeB").WithOneFrame(0);
            //     }),
            //     aac.NewClip("AL Theme Colors On").Animating(clip => {
            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorR").WithOneFrame(Color.white);
            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorG").WithOneFrame(Color.white);
            //         // clip.AnimatesColor(bodySkinnedMeshRenderer, "material._alColorB").WithOneFrame(Color.white);

            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeR").WithOneFrame(1);
            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeG").WithOneFrame(4);
            //         clip.Animates(bodySkinnedMeshRenderer, "material._alThemeB").WithOneFrame(2);
            //     }),
            // }), directBlendWeight);

            // Set up main presets
            var presetsLayer = _utils.NewPresetsLayer("Main", paramPrefixBase, mainPresets);
            

            assetContainer.UpdateAnimator("FX", _controller.AnimatorController);
        }

        [ContextMenu("Convert presets from RGB to HSV")]
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
            AACUtils.SyncAnimatorParamsToList(_controller.AnimatorController, mainParamsList);
        }

        [ContextMenu("Generate Animator")]
        public void GenerateAction()
        {
            OnEnable();
        }
    }
}

#endif