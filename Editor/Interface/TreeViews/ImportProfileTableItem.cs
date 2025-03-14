using AssetPipeline.Import;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace AssetPipeline
{
    internal class ImportProfileTableItem : TreeViewItem
    {
        public readonly AssetImportProfile Profile;
        public readonly SerializedObject SerializedObject;

        public ImportProfileTableItem(int id, int depth, AssetImportProfile profile)
            : base(id, depth, profile.name)
        {
            Profile = profile;
            SerializedObject = new SerializedObject(Profile);
        }
    }
}