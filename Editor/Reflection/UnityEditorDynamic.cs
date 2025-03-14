using System;
using System.Reflection;
using UnityEditor;
using AssetPipeline.ReflectionMagic;

namespace AssetPipeline
{
    internal static class UnityEditorDynamic
    {
        public static readonly Assembly UnityEditorAssembly;
        public static readonly dynamic EditorGUIUtility;

        static UnityEditorDynamic()
        {
            UnityEditorAssembly = typeof(Editor).Assembly;
            EditorGUIUtility = typeof(EditorGUIUtility).AsDynamicType();
        }
    }

}