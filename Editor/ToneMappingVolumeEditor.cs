using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(ToneMappingVolumeComponent))]
public class ToneMappingVolumeEditor : VolumeComponentEditor
{
    void CustomPropertyField(SerializedDataParameter property)
    {
        if (property.attributes.Length > 0)
        {
            if (!string.IsNullOrEmpty((property.attributes[0] as DisplayInfoAttribute).name))
                PropertyField(property, new GUIContent((property.attributes[0] as DisplayInfoAttribute).name));
            else
                PropertyField(property);
        }
        else
            PropertyField(property);
    }

    public override void OnInspectorGUI()
    {
        var o = new PropertyFetcher<ToneMappingVolumeComponent>(serializedObject);
        ToneMappingVolumeComponent obj = serializedObject.targetObject as ToneMappingVolumeComponent;
        CustomPropertyField(Unpack(serializedObject.FindProperty("enabled")));
        CustomPropertyField(Unpack(serializedObject.FindProperty("toneMapper")));

        EditorGUILayout.Space();

        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.TumblinRushmeier)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("Ldmax")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("Cmax")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.Schlick)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("p")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("hiVal")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.Ward)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("Ldmax")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.ReinhardExtended)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("Pwhite")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.Hable)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("shoulderStrength")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("linearStrength")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("linearAngle")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("toeStrength")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("toeNumerator")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("toeDenominator")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("linearWhitePoint")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.Uchimura)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("maxBrightness")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("contrast")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("linearStart")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("linearLength")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("blackTightnessShape")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("blackTightnessOffset")));
        }
        if (obj.toneMapper.value == ToneMappingVolumeComponent.Tonemappers.Custom)
        {
            CustomPropertyField(Unpack(serializedObject.FindProperty("lutTexture")));
            CustomPropertyField(Unpack(serializedObject.FindProperty("postExposure")));
        }

        serializedObject.ApplyModifiedProperties();
        //base.OnInspectorGUI();
    }
}
