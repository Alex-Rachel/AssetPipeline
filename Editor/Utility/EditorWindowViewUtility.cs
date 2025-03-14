using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline
{
    public static class EditorWindowViewUtility
    {
        [Flags]
        public enum ViewEdge
        {
            None = 0,
            Left = 1,
            Bottom = 2,
            Top = 4,
            Right = 8,
            BottomLeft = Bottom | Left, // 0x00000003
            BottomRight = Right | Bottom, // 0x0000000A
            TopLeft = Top | Left, // 0x00000005
            TopRight = Right | Top, // 0x0000000C
            FitsVertical = Top | Bottom, // 0x00000006
            FitsHorizontal = Right | Left, // 0x00000009
            Before = TopLeft, // 0x00000005
            After = BottomRight, // 0x0000000A
        }

        static readonly Type HostViewType;
        static readonly Type ContainerWindowType;
        static readonly Type DropInfoType;
        static readonly Type DropInfoTypeType;
        static readonly Type SplitViewType;
        static readonly Type SplitViewViewEdgeType;
        static readonly Type ExtraDropInfoType;
        static readonly Type DockAreaType;

        static readonly FieldInfo m_m_ParentInfo;
        static readonly PropertyInfo m_windowInfo;
        static readonly PropertyInfo m_rootViewInfo;
        static readonly PropertyInfo m_allChildren;
        static readonly FieldInfo m_userDataInfo;
        static readonly FieldInfo m_typeInfo;
        static readonly FieldInfo m_rectInfo;
        static readonly FieldInfo m_originalDragSourceInfo;
        static readonly MethodInfo m_performDropInfo;

        static readonly MethodInfo m_removeTabInfo;
        static readonly MethodInfo m_addTabInfo;

        static readonly FieldInfo m_panesInfo;

        static EditorWindowViewUtility()
        {
            HostViewType = UnityEditorDynamic.UnityEditorAssembly.GetType("UnityEditor.HostView");
            ContainerWindowType = UnityEditorDynamic.UnityEditorAssembly.GetType("UnityEditor.ContainerWindow");
            DropInfoType = UnityEditorDynamic.UnityEditorAssembly.GetType("UnityEditor.DropInfo");
            DropInfoTypeType = DropInfoType.GetNestedType("UnityEditor.Type", BindingFlags.NonPublic);
            SplitViewType = UnityEditorDynamic.UnityEditorAssembly.GetType("UnityEditor.SplitView");
            SplitViewViewEdgeType = SplitViewType.GetNestedType("UnityEditor.ViewEdge", BindingFlags.NonPublic);
            ExtraDropInfoType = SplitViewType.GetNestedType("UnityEditor.ExtraDropInfo", BindingFlags.NonPublic);
            DockAreaType = UnityEditorDynamic.UnityEditorAssembly.GetType("UnityEditor.DockArea");

            m_m_ParentInfo = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic)!;
            m_windowInfo = HostViewType.GetProperty("window", BindingFlags.Instance | BindingFlags.Public)!;
            m_rootViewInfo = ContainerWindowType.GetProperty("rootView", BindingFlags.Instance | BindingFlags.Public)!;
            m_allChildren = HostViewType.GetProperty("allChildren", BindingFlags.Instance | BindingFlags.Public)!;
            m_userDataInfo = DropInfoType.GetField("userData", BindingFlags.Instance | BindingFlags.Public)!;
            m_typeInfo = DropInfoType.GetField("type", BindingFlags.Instance | BindingFlags.Public)!;
            m_rectInfo = DropInfoType.GetField("rect", BindingFlags.Instance | BindingFlags.Public)!;
            m_originalDragSourceInfo = DockAreaType.GetField("s_OriginalDragSource", BindingFlags.Static | BindingFlags.NonPublic)!;
            m_performDropInfo = SplitViewType.GetMethod("PerformDrop", new[] { typeof(EditorWindow), DropInfoType, typeof(Vector2) })!;

            m_removeTabInfo = DockAreaType.GetMethod("RemoveTab", new[] { typeof(EditorWindow), typeof(bool), typeof(bool) });
            m_addTabInfo = DockAreaType.GetMethod("AddTab", new[] { typeof(int), typeof(EditorWindow), typeof(bool) });

            m_panesInfo = DockAreaType.GetField("m_Panes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        }

        public static void AdsorbWindow(this EditorWindow self, EditorWindow targetWindow, ViewEdge viewEdge, Vector2? screenPos = null, int? index = null)
        {
            var view = m_m_ParentInfo.GetValue(self);
            var targetView = m_m_ParentInfo.GetValue(targetWindow);

            var containerWindow = m_windowInfo.GetValue(view);
            var targetContainerWindow = m_windowInfo.GetValue(targetView);

            var rootView = m_rootViewInfo.GetValue(containerWindow);
            var targetRootView = m_rootViewInfo.GetValue(targetContainerWindow);

            var dropInfo = Activator.CreateInstance(DropInfoType, new object[] { null });

            var viewEdgeValue = Enum.ToObject(SplitViewViewEdgeType, (int)viewEdge);

            var allChildren = (Array)m_allChildren.GetValue(view);
            if (index == null)
            {
                index = allChildren.Length - 1;
            }
            else
            {
                index = Math.Min(allChildren.Length - 1, index.Value);
            }

            var extraDropInfo = Activator.CreateInstance(ExtraDropInfoType, GetIsRootWindow(self, targetWindow), viewEdgeValue, index);

            m_userDataInfo.SetValue(dropInfo, extraDropInfo);
            if (rootView == targetRootView)
            {
                m_typeInfo.SetValue(dropInfo, GetDropInfoType(self, targetWindow));
            }
            
            m_rectInfo.SetValue(dropInfo, targetWindow.position);

            m_originalDragSourceInfo.SetValue(null, targetView);

            screenPos ??= Vector2.zero;
            m_performDropInfo.Invoke(rootView, new[] { targetWindow, dropInfo, screenPos });
        }

        public static void RemoveTab(this EditorWindow self, EditorWindow targetWindow, bool killIfEmpty = true, bool sendEvents = true)
        {
            var view = m_m_ParentInfo.GetValue(self);
            m_removeTabInfo.Invoke(view, new object[] { targetWindow, killIfEmpty, sendEvents });
        }

        public static void AddTab(this EditorWindow self, EditorWindow targetWindow, bool sendPaneEvents = true, int? index = null)
        {
            var view = m_m_ParentInfo.GetValue(self);
            var panes = (List<EditorWindow>)m_panesInfo.GetValue(view);
            if (index == null)
            {
                index = panes.Count;
            }
            else
            {
                index = Math.Min(panes.Count, index.Value);
            }

            m_addTabInfo.Invoke(view, new object[] { index, targetWindow, sendPaneEvents });
        }

        private static bool GetIsRootWindow(EditorWindow rootWindow, EditorWindow targetWindow)
        {
            var view = m_m_ParentInfo.GetValue(rootWindow);
            var targetView = m_m_ParentInfo.GetValue(targetWindow);

            var containerWindow = m_windowInfo.GetValue(view);
            var targetContainerWindow = m_windowInfo.GetValue(targetView);

            var rootView = m_rootViewInfo.GetValue(containerWindow);
            var targetRootView = m_rootViewInfo.GetValue(targetContainerWindow);

            return rootView == targetRootView;
        }

        private static object GetDropInfoType(EditorWindow rootWindow, EditorWindow targetWindow)
        {
            var view = m_m_ParentInfo.GetValue(rootWindow);
            var targetView = m_m_ParentInfo.GetValue(targetWindow);

            var containerWindow = m_windowInfo.GetValue(view);
            var targetContainerWindow = m_windowInfo.GetValue(targetView);

            var rootView = m_rootViewInfo.GetValue(containerWindow);
            var targetRootView = m_rootViewInfo.GetValue(targetContainerWindow);

            var targetPanes = (List<EditorWindow>)m_panesInfo.GetValue(targetView);
            if (targetPanes.Count > 1)
            {
                return Enum.ToObject(DropInfoTypeType, 0);
            }

            if (rootView == targetRootView)
            {
                return Enum.ToObject(DropInfoTypeType, 1);
            }

            return Enum.ToObject(DropInfoTypeType, 2);
        }
    }
}
