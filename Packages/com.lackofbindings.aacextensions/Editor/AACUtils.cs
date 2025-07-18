#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using UnityEngine;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace LackofbindingsAAC
{
    public class AACUtils : IEditorOnly
    {
        private readonly AacFlBase _aac;
        private readonly AacFlController _controller;
        private readonly AacFlLayer _anyLayer;
        public static string[] RGBA = new string[] { "R", "G", "B", "A" };
        public static string[] HSV = new string[] { "H", "S", "V" };

        public AACUtils(AacFlBase aac, AacFlController controller, AacFlLayer baseLayer)
        {
            _aac = aac;
            _controller = controller;
            _anyLayer = baseLayer;
        }

        public static void ConvertPresetsRGBToHSV(VRCExpressionParameters[] presets)
        {
            foreach (var preset in presets)
            {

                foreach (var param in preset.parameters)
                {
                    if (param.name.EndsWith("/R"))
                    {
                        // Collect each set of RGB params as floats
                        // Found first part (R) color component, now look for matching G and B
                        float r = 0, g = 0, b = 0;

                        r = param.defaultValue;

                        string prefix = param.name.Split("/R")[0];
                        foreach (var paramIn in preset.parameters)
                        {
                            if (paramIn.name == prefix + "/G")
                            {
                                g = paramIn.defaultValue;
                            }
                            else if (paramIn.name == prefix + "/B")
                            {
                                b = paramIn.defaultValue;
                            }
                        }

                        // Convert to HSV
                        float h, s, v;
                        Color.RGBToHSV(new Color(r, g, b), out h, out s, out v);

                        // Save back as HSV params
                        foreach (var paramOut in preset.parameters)
                        {
                            if (paramOut.name == prefix + "/R")
                            {
                                paramOut.name = Regex.Replace(paramOut.name, @"/R$", "/H");
                                paramOut.defaultValue = h;
                            }
                            else if (paramOut.name == prefix + "/G")
                            {
                                paramOut.name = Regex.Replace(paramOut.name, @"/G$", "/S");
                                paramOut.defaultValue = s;
                            }
                            else if (paramOut.name == prefix + "/B")
                            {
                                paramOut.name = Regex.Replace(paramOut.name, @"/B$", "/V");
                                paramOut.defaultValue = v;
                            }
                        }
                    }
                }
                UnityEditor.EditorUtility.SetDirty(preset);
            }
        }

        public static void CopyPresetsToFiles(AnimatorController oldController, VRCExpressionParameters[] newPresets)
        {
            // var oldPresets = animationController
            AnimatorControllerLayer[] allLayer = oldController.layers;

            foreach (var layer in allLayer)
            {
                if (layer.name != "Presets") continue;

                foreach (var s in layer.stateMachine.states)
                {
                    var state = s.state;
                    if (state.name == "Waiting") continue;

                    VRCAvatarParameterDriver paramDriver = state.behaviours.OfType<VRCAvatarParameterDriver>().First();
                    var driverParameters = paramDriver.parameters;

                    foreach (var newPreset in newPresets)
                    {
                        if (newPreset.name.ToLower().Contains(state.name.ToLower()))
                        {
                            newPreset.parameters = new VRCExpressionParameters.Parameter[driverParameters.Count];
                            for (int i = 0; i < driverParameters.Count; i++)
                            {
                                newPreset.parameters[i] = new VRCExpressionParameters.Parameter();
                                newPreset.parameters[i].name = driverParameters[i].name;
                                newPreset.parameters[i].valueType = VRCExpressionParameters.ValueType.Float;
                                newPreset.parameters[i].defaultValue = driverParameters[i].value;
                            }
                        }
                        UnityEditor.EditorUtility.SetDirty(newPreset);
                    }
                }
            }
        }

        public static void SyncAnimatorParamsToList(AnimatorController controller, VRCExpressionParameters paramsList)
        {
            var separatorLParam = new VRCExpressionParameters.Parameter();
            separatorLParam.name = "-------------";
            paramsList.parameters = paramsList.parameters.Append(separatorLParam).ToArray();

            foreach (var aParam in controller.parameters)
            {
                bool found = false;
                foreach (var lParam in paramsList.parameters)
                {
                    if (lParam.name == aParam.name)
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    var newLParam = new VRCExpressionParameters.Parameter();
                    newLParam.name = aParam.name;
                    switch (aParam.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            newLParam.valueType = VRCExpressionParameters.ValueType.Float;
                            newLParam.defaultValue = aParam.defaultFloat;
                            break;
                        case AnimatorControllerParameterType.Bool:
                            newLParam.valueType = VRCExpressionParameters.ValueType.Bool;
                            newLParam.defaultValue = aParam.defaultBool ? 1 : 0;
                            break;
                        case AnimatorControllerParameterType.Int:
                            newLParam.valueType = VRCExpressionParameters.ValueType.Int;
                            newLParam.defaultValue = aParam.defaultInt;
                            break;
                    }
                    paramsList.parameters = paramsList.parameters.Append(newLParam).ToArray();
                }
            }
            UnityEditor.EditorUtility.SetDirty(paramsList);
        }

        public static string PrefixToClipName(string paramPrefix)
        {
            string[] parts = paramPrefix.Split("/");
            if (parts.Length > 2)
            {
                parts = parts.Skip(2).ToArray();
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Returns a new 1D blend tree that will drive the given color property with individual R,G,B floats.
        /// Creates the required params based on the given param prefix.
        /// </summary>
        public AacFlBlendTree1D NewRGBBlendTree(string paramPrefix, Component anyComponent, string propertyName)
        {
            string clipPrefix = PrefixToClipName(paramPrefix);
            var RTree = _aac.NewBlendTree($"{clipPrefix} Color R?").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/R"));

            for (var r = 0; r < 2; r++)
            {
                var GTree = _aac.NewBlendTree($"{clipPrefix} Color R{r}G?").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/G"));
                RTree.WithAnimation(GTree, r);

                for (var g = 0; g < 2; g++)
                {
                    var BTree = _aac.NewBlendTree($"{clipPrefix} Color R{r}G{g}B?").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/B"));
                    GTree.WithAnimation(BTree, g);

                    for (var b = 0; b < 2; b++)
                    {
                        BTree.WithAnimation(_aac.NewClip($"{clipPrefix} Color R{r}G{g}B{b}").Animating(clip =>
                            clip.AnimatesColor(anyComponent, propertyName).WithOneFrame(new Color(r, g, b, 1))
                        ), b);
                    }
                }
            }

            return RTree;
        }

        /// <inheritdoc cref="NewHSVBlendTree(string, Component[], string)"/>
        public AacFlBlendTree1D NewHSVBlendTree(string paramPrefix, Component anyComponent, string propertyName)
        {
            return NewHSVBlendTree(paramPrefix, new[] { anyComponent }, propertyName);
        }

        /// <summary>
        /// Returns a new 1D blend tree that will drive the given color property with individual H,S,V floats.
        /// Creates the required params based on the given param prefix.
        /// </summary>
        public AacFlBlendTree1D NewHSVBlendTree(string paramPrefix, Component[] anyComponents, string propertyName)
        {
            string clipPrefix = PrefixToClipName(paramPrefix);
            var STree = _aac.NewBlendTree($"{clipPrefix} Color S?").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/S"));

            for (var s = 0; s < 2; s++)
            {
                var VTree = _aac.NewBlendTree($"{clipPrefix} Color S{s}V?").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/V"));
                STree.WithAnimation(VTree, s);

                for (var v = 0; v < 2; v++)
                {
                    switch (s * 10 + v)
                    {
                        case 00:
                        case 10:
                            VTree.WithAnimation(_aac.NewClip($"{clipPrefix} Color H?S{s}V{v}").Animating(clip =>
                                clip.AnimatesColor(anyComponents, propertyName).WithOneFrame(new Color(0, 0, 0, 1))
                            ), v);
                            break;
                        case 01:
                            VTree.WithAnimation(_aac.NewClip($"{clipPrefix} Color H?S{s}V{v}").Animating(clip =>
                                clip.AnimatesColor(anyComponents, propertyName).WithOneFrame(new Color(1, 1, 1, 1))
                            ), v);
                            break;
                        case 11:
                            VTree.WithAnimation(NewHueBlendTree(_anyLayer.FloatParameter($"{paramPrefix}/H"), anyComponents, propertyName, 1.0f, false, false), v);
                            break;
                    }
                }
            }

            return STree;
        }

        public AacFlBlendTree1D NewPatternBlendTree(string paramPrefix, Component anyComponent, string propertyPrefix)
        {
            string clipPrefix = PrefixToClipName(paramPrefix);

            var patternOffsets = new[,] {
                    { 0.0f, 0.0f, 0.0f, 0.0f }, // Off
                    { 0.5f, 0.5f, 0.0f, 0.0f }, // Pattern 1
                    { 0.5f, 0.5f, 0.5f, 0.0f }, // Pattern 2
                    { 0.5f, 0.5f, 0.0f, 0.5f }, // Pattern 3
                    { 0.5f, 0.5f, 0.5f, 0.5f }, // Pattern 4
                };

            var patternAxis = new[] { "x", "y", "z", "w" };

            var patternClips = new AacFlClip[5];

            // Make a new clip setting the 4 fields of the property, one clip per possible pattern.
            for (int c = 0; c < patternOffsets.GetLength(0); c++)
            {
                patternClips[c] = _aac.NewClip($"Pattern {c}").Animating(clip =>
                {
                    for (var a = 0; a < patternOffsets.GetLength(1); a++)
                    {
                        clip.Animates(anyComponent, $"{propertyPrefix}.{patternAxis[a]}").WithOneFrame(patternOffsets[c, a]);
                    }
                });
            }


            var pTree = _aac.NewBlendTree($"{clipPrefix} Pattern Blend").Simple1D(_anyLayer.FloatParameter($"{paramPrefix}/Pattern"));

            // Extra threshold to make sure zero gets covered
            pTree.WithAnimation(patternClips[0], 0);

            // Add each clip 2 times, creating a constant region between t + 0.001 and t + 1
            for (int c = 0; c < patternClips.Length; c++)
            {
                pTree.WithAnimation(patternClips[c], (1.0f / (patternClips.Length) * c) + 0.001f);
                pTree.WithAnimation(patternClips[c], (1.0f / (patternClips.Length) * (c + 1)));
            }

            return pTree;
        }

        public AacFlBlendTree1D NewMetallicBlendTree(string paramPrefix, Component anyComponent, string propertyName, bool invert = false)
        {
            return NewToggleBlendTree($"{paramPrefix}/Metallic", anyComponent, propertyName, invert ? -1 : 0, invert ? 0 : 1);
        }

        public AacFlBlendTree1D NewToggleBlendTree(string paramPrefix, Component anyComponent, string propertyName, float valueMin = 0, float valueMax = 1)
        {
            return NewToggleBlendTree(paramPrefix, new[] { anyComponent }, propertyName, valueMin, valueMax);
        }

        public AacFlBlendTree1D NewToggleBlendTree(string paramPrefix, Component[] anyComponents, string propertyName, float valueMin = 0, float valueMax = 1)
        {
            return NewToggleBlendTree(_anyLayer.FloatParameter(paramPrefix), anyComponents, propertyName, valueMin, valueMax);
        }

        public AacFlBlendTree1D NewToggleBlendTree(AacFlFloatParameter param, Component[] anyComponents, string propertyName, float valueMin = 0, float valueMax = 1)
        {
            string clipPrefix = PrefixToClipName(param.Name);
            var tree = _aac.NewBlendTree($"{clipPrefix}").Simple1D(param);

            float[] values = new[] { valueMin, valueMax };

            for (var i = 0; i < 2; i++)
            {
                tree.WithAnimation(_aac.NewClip($"{clipPrefix} {i}").Animating(clip =>
                    clip.Animates(anyComponents, propertyName).WithOneFrame(values[i])
                ), i);
            }

            return tree;
        }

        /// <summary>
        /// Creates a new 1D blend tree with the provided clips evenly distributed from zero to one along the provided float.
        /// </summary>
        public AacFlBlendTree1D NewBlendTreeFromClips(AacFlFloatParameter floatParam, AacFlClip[] clips)
        {
            var tree = _aac.NewBlendTree($"{clips[0].Clip.name} Blend").Simple1D(floatParam);

            for (int i = 0; i < clips.Length; i++)
            {
                tree.WithAnimation(clips[i], 1.0f / (clips.Length - 1) * i);
            }

            return tree;
        }

        public AacFlBlendTree1D NewSmoothnessBlendTree(string paramPrefix, Component anyComponent, string propertyName)
        {
            string clipPrefix = PrefixToClipName(paramPrefix);
            var parameter = _anyLayer.FloatParameter($"{paramPrefix}/Smoothness");
            var tree = _aac.NewBlendTree($"{clipPrefix} Smoothness").Simple1D(parameter);

            float[] thresholds = new[] { 0f, 0.5f, 1f };
            float[] values = new[] { -1f, 0f, 1f };

            for (var i = 0; i < 3; i++)
            {
                tree.WithAnimation(_aac.NewClip($"{clipPrefix} Smoothness{i}").Animating(clip =>
                    clip.Animates(anyComponent, propertyName).WithOneFrame(values[i])
                ), thresholds[i]);
            }

            _anyLayer.OverrideValue(parameter, 0.5f);

            return tree;
        }

        // Single component overload of below
        public AacFlBlendTree1D NewHueBlendTree(AacFlFloatParameter param, Component anyComponent, string propertyName, float brightness = 1.0f, bool addBlackAndWhite = false, bool isHDR = false)
        {
            return NewHueBlendTree(param, new[] { anyComponent }, propertyName, brightness, addBlackAndWhite, isHDR);
        }

        /// <summary>
        /// Creates a new hue rotation blend tree that animates a color or HDR color swatch.
        /// </summary>
        /// <param name="param">The Parameter to control the hue rotation</param>
        /// <param name="anyComponents">The component to animate</param>
        /// <param name="propertyName">The material property to animate</param>
        /// <param name="brightness">Multiplier for HDR colors, the color is automatically considered HDR if this value is above one</param>
        /// <param name="addBlackAndWhite">Adds an extra keyframe for pure white and pure black at 0 and 1, respectively</param>
        /// <param name="isHDR">Use to force HDR mode on (only needed if HDR multiplier needs to be less than 1.0)</param>
        /// <returns></returns>
        public AacFlBlendTree1D NewHueBlendTree(AacFlFloatParameter param, Component[] anyComponents, string propertyName, float brightness = 1.0f, bool addBlackAndWhite = false, bool isHDR = false)
        {
            string clipPrefix = PrefixToClipName(param.Name);
            var tree = _aac.NewBlendTree(clipPrefix).Simple1D(param);

            if (brightness > 1.0f) isHDR = true;

            Color hdr = new Color(brightness, brightness, brightness, 1);

            // var hueRotation = new[] {
            //     1.0f, 1.0f, 1.0f, // white
            //     1.0f, 0.0f, 0.0f,
            //     1.0f, 1.0f, 0.0f,
            //     0.0f, 1.0f, 0.0f,
            //     0.0f, 1.0f, 1.0f,
            //     0.1f, 0.0f, 1.0f,
            //     0.1f, 0.0f, 0.0f,
            //     0.0f, 0.0f, 0.0f, // black
            // };

            // int numColors = hueRotation.Length / 3;
            // for (var i = 0; i < numColors; i++)
            // {
            //     tree.WithAnimation(aac.NewClip($"{clipPrefix} Hue H{i}S1V1").Animating(clip => 
            //         clip.AnimatesHDRColor(anyComponents, propertyName).WithOneFrame(new Color(
            //             hueRotation[i * 3 + 0], 
            //             hueRotation[i * 3 + 1], 
            //             hueRotation[i * 3 + 2]
            //         ) * hdr)
            //     ), MapRange(i, 0, numColors - 1, 0, 1));
            // }

            if (addBlackAndWhite)
            {
                // Add White
                tree.WithAnimation(_aac.NewClip($"{clipPrefix} Hue White").Animating(clip =>
                {
                    if (isHDR) clip.AnimatesHDRColor(anyComponents, propertyName).WithOneFrame(Color.white * hdr);
                    else clip.AnimatesColor(anyComponents, propertyName).WithOneFrame(Color.white);
                }), 0);

                // Add Black
                tree.WithAnimation(_aac.NewClip($"{clipPrefix} Hue Black").Animating(clip =>
                {
                    if (isHDR) clip.AnimatesHDRColor(anyComponents, propertyName).WithOneFrame(Color.black);
                    else clip.AnimatesColor(anyComponents, propertyName).WithOneFrame(Color.black);
                }), 1);
            }

            // Add Rainbow
            int numHueSamples = 6;
            for (var i = 0; i <= numHueSamples; i++)
            {
                float mul = (float)i / numHueSamples;
                tree.WithAnimation(_aac.NewClip($"{clipPrefix} Hue H{360 * mul}S1V1").Animating(clip =>
                {
                    if (isHDR) clip.AnimatesHDRColor(anyComponents, propertyName).WithOneFrame(Color.HSVToRGB(mul, 1, 1) * hdr);
                    else clip.AnimatesColor(anyComponents, propertyName).WithOneFrame(Color.HSVToRGB(mul, 1, 1));
                }), addBlackAndWhite ? MapRange(i, 0, numHueSamples, 0.1f, 0.9f) : mul);
            }

            return tree;
        }

        /// <summary>
        /// Creates and adds a new layer that drives parameters to one or more sets of preset values.
        /// Presets are defined by VRCExpressionParameters objects using the defaultValue fields.
        /// Will create a new int parameter with suffix "/Preset" to select a preset, expected to be driven from menu buttons.
        /// </summary>
        /// <param name="addRandom">
        /// If true, will add an extra state that randomizes all parameters. Activates when preset param is 255.
        /// Parameter list will be based on the first preset. 
        /// Extra logic is added to round non-floats to zero or one.
        /// </param>
        public AacFlLayer NewPresetsLayer(string layerNamePrefix, string paramPrefix, VRCExpressionParameters[] presets, bool addRandom = true, int addCustom = 1)
        {
            var presetsLayer = _controller.NewLayer($"{layerNamePrefix} Presets");
            var presetWaitingState = presetsLayer.NewState("Waiting", 0, 0);
            presetWaitingState.TransitionsFromEntry();
            var presetSelectorParameter = presetsLayer.IntParameter($"{paramPrefix}/Preset");

            for (int presetIdx = 0; presetIdx < presets.Length; presetIdx++)
            {
                var preset = presets[presetIdx];
                var presetState = presetsLayer.NewState(preset.name, 1, presetIdx);

                presetWaitingState.TransitionsTo(presetState).When(presetSelectorParameter.IsEqualTo(presetIdx + 1));
                presetState.Exits().When(presetSelectorParameter.IsNotEqualTo(presetIdx + 1));

                presetState.Driving(driver =>
                {
                    foreach (VRCExpressionParameters.Parameter parameter in preset.parameters)
                    {
                        driver.Sets(presetsLayer.FloatParameter(parameter.name), parameter.defaultValue);
                    }
                    driver.Locally();
                });
            }

            if (addCustom > 0)
            {
                var presetTemplate = presets[0];

                for (int slotNum = 1; slotNum <= addCustom; slotNum++)
                {
                    string slotParamPrefix = $"{paramPrefix}/SaveSlot/S{slotNum}";
                    var slotParamSave = presetsLayer.BoolParameter($"{slotParamPrefix}/Save");
                    var slotParamLoad = presetsLayer.BoolParameter($"{slotParamPrefix}/Load");
                    var slotParamOpen = presetsLayer.BoolParameter($"{slotParamPrefix}/Open");

                    AacFlState slotStateLoad = presetsLayer.NewState($"Load Custom Slot {slotNum}", 1, presets.Length + slotNum);
                    AacFlState slotStateSave = presetsLayer.NewState($"Save Custom Slot {slotNum}", 2, presets.Length + slotNum);

                    presetWaitingState.TransitionsTo(slotStateLoad).When(slotParamLoad.IsTrue());
                    slotStateLoad.Exits().When(slotParamLoad.IsFalse());
                    presetWaitingState.TransitionsTo(slotStateSave).When(slotParamSave.IsTrue());
                    slotStateSave.Exits().When(slotParamSave.IsFalse());

                    slotStateLoad.Driving(driver =>
                    {
                        foreach (VRCExpressionParameters.Parameter parameter in presetTemplate.parameters)
                        {
                            string savedPath = parameter.name.Replace(paramPrefix, slotParamPrefix);
                            driver.Copies(presetsLayer.FloatParameter(savedPath), presetsLayer.FloatParameter(parameter.name));
                        }
                        driver.Sets(slotParamOpen, false);
                        driver.Locally();
                    });

                    slotStateSave.Driving(driver =>
                    {
                        foreach (VRCExpressionParameters.Parameter parameter in presetTemplate.parameters)
                        {
                            string savedPath = parameter.name.Replace(paramPrefix, slotParamPrefix);
                            driver.Copies(presetsLayer.FloatParameter(parameter.name), presetsLayer.FloatParameter(savedPath));
                        }
                        driver.Sets(slotParamOpen, false);
                        driver.Locally();
                    });
                }
            }

            if (addRandom)
            {
                var randomState = presetsLayer.NewState("Randomize", 1, presets.Length + addCustom + 2);

                presetWaitingState.TransitionsTo(randomState).When(presetSelectorParameter.IsEqualTo(255));
                randomState.Exits().When(presetSelectorParameter.IsNotEqualTo(255));

                randomState.Driving(driver =>
                {
                    foreach (VRCExpressionParameters.Parameter parameter in presets[0].parameters)
                    {
                        switch (parameter.valueType)
                        {
                            case VRCExpressionParameters.ValueType.Float:
                                driver.Randomizes(presetsLayer.FloatParameter(parameter.name), 0, 1);
                                break;
                            case VRCExpressionParameters.ValueType.Int:
                                driver.Randomizes(presetsLayer.FloatParameter(parameter.name), 0, 255);
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                // copy bool out to separate float parameter (thats not in params list) because default casting results in value almost always true
                                driver.Randomizes(presetsLayer.FloatParameter($"{parameter.name}_Random"), 0, 1);
                                driver.Copies(presetsLayer.FloatParameter($"{parameter.name}_Random"), presetsLayer.FloatParameter(parameter.name));
                                break;
                        }
                    }
                    driver.Sets(presetSelectorParameter, 0);
                    driver.Locally();
                });

                // Add extra logic to round bools to zero or one
                // Need to do this from separate copy (made above), otherwise default casting casts everything thats not zero to  true.
                // The param being in the param list enforces the casting, so if we do it on a copy thats not in the list we can do it with our own logic.
                var boolParameters = presets[0].parameters.Where(p => p.valueType == VRCExpressionParameters.ValueType.Bool).ToList();
                for (int p = 0; p < boolParameters.Count; p++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var boolParameter = presetsLayer.FloatParameter(boolParameters[p].name);
                        var boolParameterCopy = presetsLayer.FloatParameter($"{boolParameters[p].name}_Random");

                        var roundingState = presetsLayer.NewState($"{PrefixToClipName(boolParameter.Name)} Round to {i}", 2 + i, presets.Length + addCustom + 2 + p);
                        if (i == 0)
                        {
                            roundingState.TransitionsFromAny().When(boolParameterCopy.IsLessThan(0.5f)).And(boolParameterCopy.IsGreaterThan(0));
                        }
                        else
                        {
                            roundingState.TransitionsFromAny().When(boolParameterCopy.IsGreaterThan(0.5f)).And(boolParameterCopy.IsLessThan(1));
                        }
                        roundingState.Driving(driver =>
                        {
                            driver.Sets(boolParameter, i);
                            driver.Sets(boolParameterCopy, i);
                            driver.Locally();
                        });
                        if (i == 0)
                        {
                            roundingState.Exits().When(boolParameter.IsLessThan(0.5f));
                        }
                        else
                        {
                            roundingState.Exits().When(boolParameter.IsGreaterThan(0.5f));
                        }
                    }
                }

            }

            return presetsLayer;
        }

        /// <summary>
        /// Creates a new layer that sets the given parameters to the given values once either when the avatar is first worn or when it is reset.
        /// </summary>
        /// <param name="paramPrefix">Prefix for the FirstRun param that will be added, usually the base prefix</param>
        /// <param name="defaults">VRC parameters list with the parameter types and value to set. BEWARE: Use the types as they appear in the animator, not the synced versions</param>
        /// <returns></returns>
        public AacFlLayer NewSetDefaultsLayer(string paramPrefix, VRCExpressionParameters defaults)
        {
            var presetsLayer = _controller.NewLayer("Set Defaults"); 

            var waitingState = presetsLayer.NewState("Waiting", 0, 0);
            waitingState.TransitionsFromEntry();

            var firstRun = presetsLayer.BoolParameter($"{paramPrefix}/FirstRun");
            var trackingType = presetsLayer.IntParameter("TrackingType");
            var isLocal = presetsLayer.BoolParameter("IsLocal");

            var setDefaultsState = presetsLayer.NewState("Set Defaults", 1, 0);

            waitingState.TransitionsTo(setDefaultsState).When(firstRun.IsTrue()).And(isLocal.IsTrue()).And(trackingType.IsGreaterThan(2));
            setDefaultsState.Exits().When(firstRun.IsFalse());

            setDefaultsState.Driving(driver =>
            {
                foreach (VRCExpressionParameters.Parameter parameter in defaults.parameters)
                {
                    // If param name ends in capital F or the word Float
                    if (Regex.IsMatch(parameter.name, @"(?:\b|[a-z])[Ff]loat$|[^A-Z]F$"))
                    {
                        // This must be a param that is being cast to a float for blendTree reasons, lets treat it as a float.
                        // The parameters list that we're storing the params in wont let us supply default values outside of the range for that type,
                        // so we have to just guess the type used in the animator based on the name.
                        driver.Sets(presetsLayer.FloatParameter(parameter.name), parameter.defaultValue);
                    }
                    else
                    {
                        switch (parameter.valueType)
                        {
                            case VRCExpressionParameters.ValueType.Float:
                                driver.Sets(presetsLayer.FloatParameter(parameter.name), parameter.defaultValue);
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                driver.Sets(presetsLayer.BoolParameter(parameter.name), (parameter.defaultValue > 0.5));
                                break;
                            case VRCExpressionParameters.ValueType.Int:
                                driver.Sets(presetsLayer.IntParameter(parameter.name), (int)parameter.defaultValue);
                                break;
                        }
                    }
                }
                driver.Sets(firstRun, false);
                driver.Locally();
            });

            return presetsLayer;
        }

        /// <summary>
        /// Creates and adds a new layer that only allows one of the supplied parameters to be true/1 at a time, sets all others to false/0
        /// Automatically converts bools to Floats (with the suffix "_Float") and drives them to match their bool counterpart.
        /// </summary>
        public AacFlLayer NewExclusiveParametersLayer(string layerNamePrefix, AacFlParameter[] parameters)
        {
            var layer = _controller.NewLayer($"{layerNamePrefix} Exclusive States");
            var waitingState = layer.NewState("Waiting", 0, 0);
            waitingState.TransitionsFromEntry();

            // one extra index for the all-off state
            for (int i = 0; i < parameters.Length + 1; i++)
            {
                AacFlParameter parameter;
                AacFlState state;
                if (i < parameters.Length)
                {
                    // Add regular state that sets all other params to off
                    parameter = parameters[i];
                    state = layer.NewState(PrefixToClipName(parameter.Name), 1, i);

                    if (parameter is AacFlFloatParameter)
                    {
                        state.TransitionsFromAny().When(((AacFlFloatParameter)parameter).IsGreaterThan(0.001f)).Or().When(((AacFlFloatParameter)parameter).IsLessThan(-0.001f));
                    }
                    else if (parameter is AacFlBoolParameter)
                    {
                        state.TransitionsFromAny().When(((AacFlBoolParameter)parameter).IsEqualTo(true));
                    }
                    else
                    {
                        throw new Exception($"Unsupported parameter type: {parameter.GetType().Name}");
                    }
                }
                else
                {
                    // Add extra off state to reset all params to off
                    // (param being null will cause next stage that sets up driver to drive all params)
                    parameter = null;
                    state = layer.NewState($"{layerNamePrefix} All Off", 1, i + 2);
                    var condition = state.TransitionsFromAny().When(layer.IntParameter("TrackingType").IsGreaterThan(2));
                    foreach (var offParameter in parameters)
                    {
                        if (offParameter is AacFlFloatParameter)
                        {
                            condition = condition.And(((AacFlFloatParameter)offParameter).IsLessThan(0.5f));
                        }
                        else if (offParameter is AacFlBoolParameter)
                        {
                            condition = condition.And(((AacFlBoolParameter)offParameter).IsFalse());
                        }
                        else
                        {
                            throw new Exception($"Unsupported parameter type: {offParameter.GetType().Name}");
                        }
                    }
                }

                state.Driving(driver =>
                {
                    foreach (AacFlParameter otherParameter in parameters)
                    {
                        if (otherParameter is AacFlFloatParameter)
                        {
                            if (otherParameter != parameter) driver.Sets((AacFlFloatParameter)otherParameter, 0);
                        }
                        else if (otherParameter is AacFlBoolParameter)
                        {
                            if (otherParameter != parameter)
                            {
                                // Normal operation: set all others to false
                                driver.Sets((AacFlBoolParameter)otherParameter, false);
                                // Set other bools to false as a Float (for use in blend trees)
                                // driver.Sets(layer.FloatParameter($"{otherParameter.Name}_Float"), 0);
                            }
                            else
                            {
                                // Set self to true as a Float (for use in blend trees)
                                // driver.Sets(layer.FloatParameter($"{otherParameter.Name}_Float"), 1);
                            }
                        }
                    }
                    driver.Locally();
                });
                // NOT Locally:
                state.Driving(driver =>
                {
                    foreach (AacFlParameter otherParameter in parameters)
                    {
                        if (otherParameter is AacFlBoolParameter)
                        {
                            if (otherParameter != parameter)
                            {
                                // Set other bools to false as a Float (for use in blend trees)
                                driver.Sets(layer.FloatParameter($"{otherParameter.Name}_Float"), 0);
                            }
                            else
                            {
                                // Set self to true as a Float (for use in blend trees)
                                driver.Sets(layer.FloatParameter($"{otherParameter.Name}_Float"), 1);
                            }
                        }
                    }
                });
            }

            return layer;
        }

        /// <summary>
        /// Creates a new nested tree of 1D blendTrees that allows each parameter to enable the respective clip if value is 1.
        /// Only Allows one clip to be active at a time, parameters are assumed to be Exclusively driven.
        /// The final clip in clips[] is run when all params are 0.
        /// </summary>
        public AacFlBlendTree1D NewExclusiveToggleTree(AacFlFloatParameter[] parameters, AacFlClip[] clips)
        {

            if (clips.Length != parameters.Length + 1) throw new Exception("Must provide exactly one more clips than parameters");

            AacFlBlendTree1D firstTree = null;
            AacFlBlendTree1D lastTree = null;

            for (int i = 0; i < parameters.Length; i++)
            {
                var tree = _aac.NewBlendTree().Simple1D(parameters[i]);
                if (firstTree == null) firstTree = tree;
                tree.WithAnimation(clips[i], 1);
                if (lastTree != null) lastTree.WithAnimation(tree, 0);
                lastTree = tree;
            }

            lastTree.WithAnimation(clips[clips.Length - 1], 0);

            return firstTree;
        }

        public AacFlFloatParameter CopyBoolToFloat(AacFlBoolParameter boolParameter)
        {
            string floatParameterName = $"{boolParameter.Name}_Float";
            var layer = NewCopyBoolToFloatLayer(boolParameter, floatParameterName);
            return layer.FloatParameter(floatParameterName);
        }

        public AacFlLayer NewCopyBoolToFloatLayer(AacFlBoolParameter boolParameter, string floatParameterName)
        {
            var layer = _controller.NewLayer($"{PrefixToClipName(boolParameter.Name)} To Float");
            var waitingState = layer.NewState("Waiting", 0, 0);
            waitingState.TransitionsFromEntry();

            foreach (bool condition in new[] { true, false })
            {
                var state = layer.NewState(condition.ToString(), 1, condition ? 1 : 0);
                state.TransitionsFromAny().When(boolParameter.IsEqualTo(condition));
                state.Driving(driver =>
                {
                    driver.Copies(boolParameter, layer.FloatParameter(floatParameterName));
                });
            }

            return layer;
        }

        public static float MapRange(float input, float input_start, float input_end, float output_start, float output_end)
        {
            return output_start + ((output_end - output_start) / (input_end - input_start)) * (input - input_start);
        }
    }

}

#endif