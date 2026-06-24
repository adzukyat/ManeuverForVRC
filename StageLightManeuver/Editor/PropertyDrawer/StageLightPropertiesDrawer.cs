using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

namespace StageLightManeuver
{
    /// <summary>
    /// <see cref="SlmProperty"/>の配列を描画するプロパティドロワー
    /// </summary>
    // [CustomPropertyDrawer(typeof(List<SlmProperty>))]
    public class StageLightPropertiesDrawer : SlmBaseDrawer
    {
        private static readonly List<Type> ClipProperty = new List<Type> { typeof(ClockProperty), typeof(StageLightOrderProperty) };
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null)
            {
                return;
            }

            var stageLightProperties = GetValueFromCache(property) as List<SlmProperty>;
            if (stageLightProperties == null)
            {
                return;
            }

            stageLightProperties.RemoveAll(x => x == null);
            var orderedStageLightProperties = CreateOrderedPropertyView(stageLightProperties);

            // ClipProperty に含まれるプロパティは先頭に描画
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(SlmEditorStyleConst.NoSpacing);
            EditorGUILayout.HelpBox("ClipProperties", MessageType.None);
            EditorGUILayout.Space(SlmEditorStyleConst.Spacing);
            EditorGUI.indentLevel++;
            foreach (var propertyType in ClipProperty)
            {
                var serializedSlmProperty = GetArrayElementForPropertyType(property, stageLightProperties, propertyType);
                if (serializedSlmProperty == null)
                {
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedSlmProperty, true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedSlmProperty.serializedObject.ApplyModifiedProperties();
                }
            }


            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.HelpBox("StageLightProperties", MessageType.None);
            EditorGUI.indentLevel++;

            for (int i = 0; i < orderedStageLightProperties.Count; i++)
            {
                var slmProperty = orderedStageLightProperties[i];
                if (slmProperty == null || ClipProperty.Contains(slmProperty.GetType())) continue;

                // EditorGUI.BeginDisabledGroup(slmProperty.isEditable == false);
                var serializedSlmProperty = GetArrayElementForPropertyInstance(property, stageLightProperties, slmProperty);
                if (serializedSlmProperty == null)
                {
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedSlmProperty, true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedSlmProperty.serializedObject.ApplyModifiedProperties();
                    // serializedSlmProperty.serializedObject.Update();
                }

                if (serializedSlmProperty.isExpanded)
                {
                    var propertyAttribute = slmProperty.GetType().GetCustomAttribute<SlmPropertyAttribute>();
                    if (propertyAttribute is { isRemovable: false }) continue;
                    var action = new Action(() =>
                    {
                        stageLightProperties.Remove(slmProperty);
                        ClearCache();
                    });
                    DrawRemoveButton(property.serializedObject, stageLightProperties, action);
                    GUILayout.Space(SlmEditorStyleConst.Spacing);
                }

                // EditorGUI.EndDisabledGroup();
            }

            GUILayout.Space(SlmEditorStyleConst.AddPropertyButtonTopMargin / 2);
            // EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
            GUILayout.Space(SlmEditorStyleConst.AddPropertyButtonTopMargin / 2);
            DrawAddPropertyButton(property.serializedObject, stageLightProperties);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0f;
            if (property == null)
            {
                return height;
            }

            var stageLightProperties = GetValueFromCache(property) as List<SlmProperty>;
            if (stageLightProperties == null)
            {
                return height;
            }

            stageLightProperties.RemoveAll(x => x == null);
            var orderedStageLightProperties = CreateOrderedPropertyView(stageLightProperties);

            for (int i = 0; i < orderedStageLightProperties.Count; i++)
            {
                var slmProperty = orderedStageLightProperties[i];
                if (slmProperty == null) continue;
                var serializedSlmProperty = GetArrayElementForPropertyInstance(property, stageLightProperties, slmProperty);
                if (serializedSlmProperty == null) continue;
                height += EditorGUI.GetPropertyHeight(serializedSlmProperty);
                height += EditorGUIUtility.singleLineHeight;
            }
            height += EditorGUIUtility.singleLineHeight;
            return height;
        }

        private static SerializedProperty GetArrayElementForPropertyType(
            SerializedProperty arrayProperty,
            List<SlmProperty> stageLightProperties,
            Type propertyType)
        {
            var index = stageLightProperties.FindIndex(x => x != null && x.GetType() == propertyType);
            return GetArrayElementAtIndexSafe(arrayProperty, index);
        }

        private static List<SlmProperty> CreateOrderedPropertyView(List<SlmProperty> stageLightProperties)
        {
            return SlmEditorSettingsUtility.SortByPropertyOrder(new List<SlmProperty>(stageLightProperties));
        }

        private static SerializedProperty GetArrayElementForPropertyInstance(
            SerializedProperty arrayProperty,
            List<SlmProperty> stageLightProperties,
            SlmProperty targetProperty)
        {
            var index = stageLightProperties.IndexOf(targetProperty);
            return GetArrayElementAtIndexSafe(arrayProperty, index);
        }

        private static SerializedProperty GetArrayElementAtIndexSafe(SerializedProperty arrayProperty, int index)
        {
            if (arrayProperty == null || !arrayProperty.isArray || index < 0 || index >= arrayProperty.arraySize)
            {
                return null;
            }

            return arrayProperty.GetArrayElementAtIndex(index);
        }

        private static void DrawRemoveButton(SerializedObject serializedObject, List<SlmProperty> properties, Action onRemove)
        {
            GUILayout.Space(SlmEditorStyleConst.Spacing);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(120)))
                {
                    onRemove?.Invoke();
                    serializedObject.Update();
                    serializedObject.ApplyModifiedProperties();

                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(SlmEditorStyleConst.Spacing);
        }

        private void DrawAddPropertyButton(SerializedObject serializedObject, List<SlmProperty> stageLightProperties)
        {
            EditorGUI.BeginChangeCheck();
            var selectList = new List<string>();
            var addablePropertyTypes = GetAddablePropertyTypes(serializedObject);
            var propertyTypes = addablePropertyTypes ?? SlmEditorUtility.SlmPropertyTypes;

            propertyTypes.ForEach(t =>
            {
                if (t != typeof(RollProperty)) selectList.Add(t.Name);
            });

            selectList.Insert(0, "Add Property");
            foreach (var property in stageLightProperties)
            {
                if (property == null) continue;
                if (selectList.Find(x => x == property.GetType().Name) != null)
                {
                    selectList.Remove(property.GetType().Name);
                }
            }
            EditorGUI.BeginDisabledGroup(selectList.Count <= 1);
            var select = EditorGUILayout.Popup(0, selectList.ToArray(), GUILayout.MinWidth(200));
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                if (select <= 0)
                {
                    return;
                }

                var type = SlmEditorUtility.GetTypeByClassName(selectList[select]);
                if (type == null)
                {
                    return;
                }

                var property = Activator.CreateInstance(type) as SlmProperty;
                if (property == null)
                {
                    return;
                }

                if (property.GetType() == typeof(ManualLightArrayProperty))
                {
                    var manualLightArrayProperty = property as ManualLightArrayProperty;
                    var lightProperty = stageLightProperties.Find(x => x.GetType() == typeof(LightProperty)) as LightProperty;
                    var lightIntensityProperty = stageLightProperties.Find(x => x.GetType() == typeof(LightIntensityProperty)) as LightIntensityProperty;
                    if (lightProperty != null)
                    {
                        manualLightArrayProperty.initialValue.angle = lightProperty.spotAngle.value.constant;
                        manualLightArrayProperty.initialValue.innerAngle = lightProperty.innerSpotAngle.value.constant;
                        manualLightArrayProperty.initialValue.range = lightProperty.range.value.constant;
                    }
                    if (lightIntensityProperty != null)
                    {
                        manualLightArrayProperty.initialValue.intensity = lightIntensityProperty.lightToggleIntensity.value.constant;
                    }
                }
                var changedByFixtureInitialization =
                    TryInitializeAddedPropertyFromFixture(serializedObject, property);
                stageLightProperties.Add(property);
                ClearCache();

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedObject.targetObject);
                if (changedByFixtureInitialization)
                {
                    serializedObject.Update();
                }
            }
        }

        private static bool TryInitializeAddedPropertyFromFixture(SerializedObject serializedObject, SlmProperty property)
        {
            if (serializedObject?.targetObject is not StageLightTimelineClip timelineClip)
            {
                return false;
            }

            var director = TimelineEditor.masterDirector;
            return timelineClip.InitializeFixtureProperty(director, property, false);
        }

        private static List<Type> GetAddablePropertyTypes(SerializedObject serializedObject)
        {
            if (serializedObject == null || serializedObject.targetObjects == null)
            {
                return null;
            }

            var hasTimelineClip = false;
            var types = new List<Type>();
            foreach (var targetObject in serializedObject.targetObjects)
            {
                if (targetObject is StageLightTimelineClip timelineClip)
                {
                    hasTimelineClip = true;
                    types.AddRange(timelineClip.GetAddablePropertyTypes(TimelineEditor.masterDirector, false));
                }
            }

            return hasTimelineClip ? types.Distinct().ToList() : null;
        }
    }
}
