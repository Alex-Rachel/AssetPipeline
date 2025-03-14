using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetPipeline.Import;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace AssetPipeline.Processors
{
    [InitializeOnLoad]
    public class SpriteAtlasHelper
    {
        /// <summary>
        /// 图集路径。
        /// </summary>
        public const string NormalAtlasDir = "Assets/Atlas";

        /// <summary>
        /// 图集配置prefab路径。
        /// </summary>
        public const string SpriteAtlasPrefabsDir = "Assets/Resources/Config/SpriteConfig/";

        /// <summary>
        /// 图集配置映射路径。
        /// </summary>
        public const string SpriteConfigPath = "Assets/Resources/Config/CacheConfig/SpriteCfg.bytes";

        /// <summary>
        /// 不打图集的目录。
        /// </summary>
        public const string NoAtlasPathDir = "Assets/Resources/UITexture";

        /// <summary>
        /// 不打图集的Texture配置prefab路径。
        /// </summary>
        public const string TextureConfigPath = "Assets/Resources/Config/CacheConfig/TextureCfg.bytes";

        private static readonly Dictionary<string, List<string>> m_allASprites = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> m_uiAtlasMap = new Dictionary<string, string>();
        private static bool m_dirty { get; set; } = false;
        private static readonly List<string> m_dirtyAtlasList = new List<string>();
        private static readonly Dictionary<string, string> m_splitAtlasDic = new Dictionary<string, string>();

        static SpriteAtlasHelper()
        {
            Init();
            EditorApplication.update += CheckDirty;
        }

        /// <summary>
        /// 当前是否已经初始化。
        /// </summary>
        private static bool m_hadInit = false;

        private static void Init()
        {
            if (m_hadInit)
            {
                return;
            }

            m_allASprites.Clear();
            // 读取所有图集信息。
            string[] findAssets = AssetDatabase.FindAssets("t:spriteatlas", new[] { NormalAtlasDir });
            foreach (var findAsset in findAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(findAsset);
                SpriteAtlas sa = AssetDatabase.LoadAssetAtPath(path, typeof(SpriteAtlas)) as SpriteAtlas;
                if (sa == null)
                {
                    Debug.LogError($"加载图集数据{path}失败");
                    continue;
                }

                string atlasName = Path.GetFileNameWithoutExtension(path);
                if (!m_allASprites.TryGetValue(atlasName, out List<string> list))
                {
                    list = new List<string>();
                    m_allASprites.Add(atlasName, list);
                }

                var objects = sa.GetPackables();
                foreach (var o in objects)
                {
                    list.Add(AssetDatabase.GetAssetPath(o));
                }
            }

            //读取所有不打图集的图片信息。
            string[] findUIRawAssets = AssetDatabase.FindAssets("t:sprite", new[] { NoAtlasPathDir });
            foreach (var findAsset in findUIRawAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(findAsset);
                Sprite sprite = AssetDatabase.LoadAssetAtPath(path, typeof(Sprite)) as Sprite;
                if (sprite == null)
                {
                    Debug.LogError($"加载图集数据{path}失败");
                    continue;
                }

                string atlasName = sprite.name;
                if (!m_allASprites.TryGetValue(atlasName, out List<string> list))
                {
                    list = new List<string>();
                    list.Add(path);
                    m_allASprites.Add(atlasName, list);
                }
            }

            // 读取spriteCfg
            FileInfo fileInfo = new FileInfo(SpriteConfigPath);
            StreamReader sr = fileInfo.OpenText();
            string str = sr.ReadToEnd();
            sr.Close();

            // m_uiAtlasMap.Clear();
            // if (MiniJSON.Json.Deserialize(str) is Dictionary<string, object> data)
            // {
            //     foreach (var kv in data)
            //     {
            //         m_uiAtlasMap[kv.Key] = (string)kv.Value;
            //     }
            // }

            m_hadInit = true;
        }

        public static void CheckDirty()
        {
            if (m_dirty)
            {
                m_splitAtlasDic.Clear();
                m_dirty = false;

                AssetDatabase.Refresh();

                SaveUISpriteCfg();
                for (int i = 0; i < m_dirtyAtlasList.Count; i++)
                {
                    var path = m_dirtyAtlasList[i];
                    if (path.StartsWith(NoAtlasPathDir))
                    {
                        continue;
                    }

                    string atlasName = GetPackageTag(path);
                    bool isUI = m_dirtyAtlasList[i].StartsWith("Assets/UIRaw");
                    SaveAtlas(atlasName, isUI);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);

                m_dirtyAtlasList.Clear();
            }
        }

        public static void OnImportSprite(string assetPath)
        {
            if (!assetPath.StartsWith("Assets"))
            {
                return;
            }

            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            
            if (ti != null && ti.textureType == TextureImporterType.Sprite)
            {
                OnProcessSprite(assetPath);
            }
        }

        public static void OnDeleteSprite(string assetPath)
        {
            Init();
            string atlasName = GetPackageTag(assetPath);


            if (!m_allASprites.TryGetValue(atlasName, out var ret))
            {
                return;
            }

            //改成文件名的匹配
            if (!ret.Exists(s => Path.GetFileName(s) == Path.GetFileName(assetPath)))
            {
                return;
            }

            if (assetPath.StartsWith("Assets/UIRaw"))
            {
                var spriteName = Path.GetFileNameWithoutExtension(assetPath);
                if (m_uiAtlasMap.ContainsKey(spriteName))
                {
                    m_uiAtlasMap.Remove(spriteName);
                    m_dirty = true;
                }
            }

            ret.Remove(assetPath);
            m_dirty = true;
            m_dirtyAtlasList.Add(assetPath);
        }


        public static void OnProcessSprite(string assetPath)
        {
            if (!assetPath.StartsWith("Assets"))
            {
                return;
            }

            Init();

            string atlasName = SpriteAtlasHelper.GetPackageTag(assetPath);
            if (string.IsNullOrEmpty(atlasName))
            {
                return;
            }

            if (assetPath.StartsWith("Assets/UIRaw"))
            {
                var spriteName = Path.GetFileNameWithoutExtension(assetPath);
                if (m_uiAtlasMap.TryAdd(spriteName, atlasName))
                {
                    m_dirty = true;
                }
            }
            else if (assetPath.StartsWith(NoAtlasPathDir))
            {
                var spriteName = Path.GetFileNameWithoutExtension(assetPath);
                if (m_uiAtlasMap.ContainsKey(spriteName))
                {
                    m_uiAtlasMap.Remove(spriteName);
                    m_dirty = true;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    ProcessSpriteSaveInfo(new List<Object>() { sprite }, spriteName);
                }
            }

            if (!m_allASprites.TryGetValue(atlasName, out var ret))
            {
                ret = new List<string>();
                m_allASprites.Add(atlasName, ret);
            }

            if (!ret.Contains(assetPath))
            {
                ret.Add(assetPath);
                m_dirty = true;
                m_dirtyAtlasList.Add(assetPath);
            }
        }

        private static void ProcessSpriteSaveInfo(List<Object> spriteList, string atlasName)
        {
            string cfgPath = SpriteAtlasPrefabsDir + atlasName + ".prefab";
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(cfgPath);
            // SpriteSaveInfo saveInfo;
            // bool isNew = false;
            // if (go == null)
            // {
            //     go = new GameObject();
            //     saveInfo = go.AddComponent<SpriteSaveInfo>();
            //     isNew = true;
            // }
            // else
            // {
            //     saveInfo = go.GetComponent<SpriteSaveInfo>();
            // }

            // saveInfo.Sprites.Clear();
            // for (int i = 0; i < spriteList.Count; i++)
            // {
            //     saveInfo.Sprites.Add((Sprite)spriteList[i]);
            // }
            //
            // if (isNew)
            // {
            //     PrefabUtility.SaveAsPrefabAsset(go, cfgPath, out var success);
            //     if (!success)
            //     {
            //         Debug.LogError($"save prefab to {cfgPath} failed");
            //     }
            //     else
            //     {
            //         Debug.Log($"save prefab [{atlasName}] success");
            //     }
            //
            //     Object.DestroyImmediate(go);
            // }
            // else
            // {
            //     EditorUtility.SetDirty(go);
            // }
        }

        public static void SaveAtlas(string atlasName, bool isUI)
        {
            if (!m_allASprites.ContainsKey(atlasName))
            {
                return;
            }

            var list = m_allASprites[atlasName];
            list.Sort(StringComparer.Ordinal);

#if UNITY_WEBGL
            if (atlasName.Contains("Background"))
            {
                var path = $"{NormalAtlasDir}/{atlasName}.spriteatlas";
                if (File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                string cfgPath = SpriteAtlasPrefabsDir + atlasName + ".prefab";
                if (File.Exists(cfgPath))
                {
                    AssetDatabase.DeleteAsset(cfgPath);
                }

                List<Object> totalList = new List<Object>();
                List<string> tmpList = new List<string>();
                int count = 1;
                string newAtlasName = atlasName;
                for (int i = 0; i < list.Count; i++)
                {
                    tmpList.Add(list[i]);
                    if (tmpList.Count >= 1)
                    {
                        newAtlasName = string.Format("{0}_{1:d2}", atlasName, count);
                        DoSaveAtlasH5(tmpList, newAtlasName, isUI, ref totalList);
                        m_splitAtlasDic[newAtlasName] = atlasName;
                        tmpList.Clear();
                        count++;
                    }
                }

                newAtlasName = string.Format("{0}_{1:d2}", atlasName, count);
                m_splitAtlasDic[newAtlasName] = atlasName;
                DoSaveAtlasH5(tmpList, newAtlasName, isUI, ref totalList);

                if (isUI && totalList.Count > 0)
                {
                    ProcessSpriteSaveInfo(totalList, atlasName);
                }
            }
            else
            {
                DoSaveAtlas(list, atlasName, isUI);
            }
#else
        DoSaveAtlas(list, atlasName, isUI);
#endif
        }

        private static void DoSaveAtlas(List<string> spriteNames, string atlasName, bool isUI)
        {
            List<Object> spriteList = new List<Object>();
            foreach (var s in spriteNames)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(s);
                if (sprite != null)
                {
                    spriteList.Add(sprite);
                }
            }

            var path = $"{NormalAtlasDir}/{atlasName}.spriteatlas";

            if (spriteList.Count == 0)
            {
                if (File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                string cfgPath = SpriteAtlasPrefabsDir + atlasName + ".prefab";
                if (File.Exists(cfgPath))
                {
                    AssetDatabase.DeleteAsset(cfgPath);
                }

                return;
            }

            var atlas = new SpriteAtlas();
            var setting = new SpriteAtlasPackingSettings();
            setting.blockOffset = 1;
            setting.padding = 2;
            setting.enableRotation = true;
            setting.enableTightPacking = !isUI;

            DoProcessSpriteAtlas(atlas, path);

            atlas.SetPackingSettings(setting);
            atlas.Add(spriteList.ToArray());

            AssetDatabase.CreateAsset(atlas, path);

            // Debug.Log($"[{GetName()}] Created SpriteAtlas: \"<b>{AssetDatabase.GetAssetPath(spriteAtlas)}</b>\"");

            if (isUI)
            {
                ProcessSpriteSaveInfo(spriteList, atlasName);
            }
        }

        private static void DoSaveAtlasH5(List<string> spriteNames, string atlasName, bool isUI, ref List<Object> totalSpriteList)
        {
            //临时处理下background..太大的问题。拆分处理，3张图为一个图集
            List<Object> spriteList = new List<Object>();
            foreach (var s in spriteNames)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(s);
                if (sprite != null)
                {
                    spriteList.Add(sprite);
                    totalSpriteList.Add(sprite);
                }
            }

            var path = string.Format("{0}/{1}.spriteatlas", NormalAtlasDir, atlasName);

            if (spriteList.Count == 0)
            {
                if (File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                string cfgPath = SpriteAtlasPrefabsDir + atlasName + ".prefab";
                if (File.Exists(cfgPath))
                {
                    AssetDatabase.DeleteAsset(cfgPath);
                }

                return;
            }

            var atlas = new SpriteAtlas();
            var setting = new SpriteAtlasPackingSettings();
            setting.blockOffset = 1;
            setting.padding = 2;
            setting.enableRotation = true;
            setting.enableTightPacking = !isUI;

            DoProcessSpriteAtlas(atlas, path);

            atlas.SetPackingSettings(setting);
            atlas.Add(spriteList.ToArray());

            AssetDatabase.CreateAsset(atlas, path);
        }

        #region GetPackageTag

        /// <summary>
        /// 根据文件路径，返回图集名称
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        public static string GetPackageTag(string fullName)
        {
            fullName = fullName.Replace("\\", "/");
            int idx = fullName.LastIndexOf("Assets/", StringComparison.Ordinal);
            if (idx == -1)
            {
                return "";
            }

            string str = fullName.Substring(idx);
            str = str.Substring(0, str.LastIndexOf("/", StringComparison.Ordinal)).Replace("Assets/", "").Replace("/", "_");

            return str;
        }

        #endregion

        #region UISpriteCfg

        private static void SaveUISpriteCfg()
        {
#if UNITY_WEBGL
            Dictionary<string, string> splitDic = new Dictionary<string, string>();
            foreach (var data in m_uiAtlasMap)
            {
                string val = data.Value;
                if (m_splitAtlasDic.ContainsKey(data.Value))
                {
                    val = m_splitAtlasDic[data.Value];
                }

                splitDic.Add(data.Key, val);
            }

            var pairs = splitDic.OrderBy(t => t.Key).OrderBy(t => t.Value);
#else
        var pairs = m_uiAtlasMap.OrderBy(t => t.Key).OrderBy(t => t.Value);
#endif
            Dictionary<string, string> tmp = new Dictionary<string, string>();
            foreach (var kv in pairs)
            {
                tmp.Add(kv.Key, kv.Value);
            }

            var json = JsonConvert.SerializeObject(tmp, Formatting.Indented);

            var fileInfo = new FileInfo(SpriteConfigPath);
            var streamWriter = fileInfo.CreateText();
            streamWriter.Write(json);
            streamWriter.Flush();
            streamWriter.Close();

            SaveTextureCfg();
        }


        #region Texture

        private static void SaveTextureCfg()
        {
            var textureDic = new Dictionary<string, string>();
            string[] findAssets = AssetDatabase.FindAssets("t:texture", new[] { "Assets/Resources/UITexture" });
            foreach (var findAsset in findAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(findAsset);
                var tt = AssetDatabase.LoadAssetAtPath(path, typeof(Texture)) as Texture;
                if (tt == null)
                {
                    Debug.LogError(string.Format("加载图集数据{0}失败", path));
                    continue;
                }

                string textureName = Path.GetFileNameWithoutExtension(path);
                var relativePath = Path.GetRelativePath("Assets/Resources", path);
                relativePath = Path.Combine(Path.GetDirectoryName(relativePath) ?? string.Empty, Path.GetFileNameWithoutExtension(relativePath));
                relativePath = relativePath.Replace("\\", "/");
                textureDic[textureName] = relativePath;
            }

            var json = JsonConvert.SerializeObject(textureDic, Formatting.Indented);

            var fileInfo = new FileInfo(TextureConfigPath);
            var streamWriter = fileInfo.CreateText();
            streamWriter.Write(json);
            streamWriter.Flush();
            streamWriter.Close();
        }

        #endregion

        #endregion

        public static void DoProcessSpriteAtlas(SpriteAtlas atlas, string path)
        {
            if (atlas == null)
            {
                return;
            }

            #region android

            var androidSetting = atlas.GetPlatformSettings("Android");
            androidSetting.overridden = true;
            androidSetting.format = TextureImporterFormat.ASTC_6x6;
            androidSetting.compressionQuality = 50;
            atlas.SetPlatformSettings(androidSetting);

            #endregion

            #region ios

            var iosSetting = atlas.GetPlatformSettings("iPhone");
            iosSetting.overridden = true;
            iosSetting.format = TextureImporterFormat.ASTC_5x5;
            iosSetting.compressionQuality = 50;
            atlas.SetPlatformSettings(iosSetting);

            #endregion

            #region webgl

            var webglSetting = atlas.GetPlatformSettings("WebGL");
            webglSetting.overridden = true;
#if UNITY_2021_1_OR_NEWER
            webglSetting.format = TextureImporterFormat.ASTC_6x6;
            webglSetting.compressionQuality = 50;
#else
            webglSetting.format = isOpaque ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
#endif
            atlas.SetPlatformSettings(webglSetting);

            #endregion
        }

        #region 强制生成图集
        private static bool IsIgnorePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.StartsWith("Assets/Scenes/MapEditor") || path.StartsWith("Assets/AATemp"))
            {
                return true;
            }
        
            return false;
        }
        
        
        [MenuItem("Tools/贴图/一键强制重新生成所有图集")]
        static void ForceReInit()
        {
            m_hadInit = false;
            Init();
            ForceGenAtlas(needForce: true);
            ForceGenSpriteConfig();
        }

        private static Dictionary<string, List<string>> m_tempAllASprites = new Dictionary<string, List<string>>();
        private static Dictionary<string, bool> m_tempIsUi = new Dictionary<string, bool>();

        [MenuItem("Tools/贴图/重新生成图集")]
        public static void ForceGenAtlas(bool needForce = false)
        {
            m_hadInit = false;
            Init();

            List<string> needSaveAtlas = new List<string>();
            m_tempAllASprites.Clear();
            m_tempIsUi.Clear();
            var findAssets = AssetDatabase.FindAssets("t:sprite", new[] { "Assets" });
            foreach (var findAsset in findAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(findAsset);
                if (IsIgnorePath(path))
                {
                    continue;
                }

                if (path.StartsWith(NoAtlasPathDir))
                {
                    continue;
                }

                var atlasName = GetPackageTag(path);
                if (!m_tempAllASprites.TryGetValue(atlasName, out var spriteList))
                {
                    spriteList = new List<string>();
                    m_tempAllASprites[atlasName] = spriteList;
                }

                if (!spriteList.Contains(path))
                {
                    spriteList.Add(path);
                }

                if (!m_tempIsUi.ContainsKey(atlasName))
                {
                    m_tempIsUi.Add(atlasName, path.StartsWith("Assets/UIRaw"));
                }
            }

            Dictionary<string, Sprite> noAtlasList = new Dictionary<string, Sprite>();
            //读取所有图集信息
            string[] findUIRawAssets = AssetDatabase.FindAssets("t:sprite", new[] { NoAtlasPathDir });
            foreach (var findAsset in findUIRawAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(findAsset);
                Sprite sprite = AssetDatabase.LoadAssetAtPath(path, typeof(Sprite)) as Sprite;
                if (sprite == null)
                {
                    Debug.LogError(string.Format("加载图集数据{0}失败", path));
                    continue;
                }

                string atlasName = sprite.name;
                if (noAtlasList.ContainsKey(atlasName))
                {
                    Debug.LogError(string.Format("重复名称！！！！{0}", atlasName));
                }

                noAtlasList[atlasName] = sprite;
            }

            //有变化的才刷
            var iter = m_tempAllASprites.GetEnumerator();
            while (iter.MoveNext())
            {
                bool needSave = false;
                var atlasName = iter.Current.Key;
                var newSpritesList = iter.Current.Value;

                List<string> existSprites;
                if (m_allASprites.TryGetValue(atlasName, out existSprites))
                {
                    if (existSprites.Count != newSpritesList.Count)
                    {
                        needSave = true;
                        existSprites.Clear();
                        existSprites.AddRange(newSpritesList);
                    }
                    else
                    {
                        for (int i = 0; i < newSpritesList.Count; i++)
                        {
                            if (!existSprites.Contains(newSpritesList[i]))
                            {
                                needSave = true;
                                break;
                            }
                        }

                        if (needSave)
                        {
                            existSprites.Clear();
                            existSprites.AddRange(newSpritesList);
                        }
                    }
                }
                else
                {
                    needSave = true;
                    m_allASprites.Add(atlasName, new List<string>(newSpritesList));
                }

                string cfgPath = SpriteAtlasPrefabsDir + atlasName + ".prefab";
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(cfgPath);
                if (go == null)
                {
                    needSave = true;
                }

#if UNITY_WEBGL
                if (!needSaveAtlas.Contains(atlasName))
#else
            if (needSave && !needSaveAtlas.Contains(atlasName))
#endif
                {
                    needSaveAtlas.Add(atlasName);
                }
            }

            m_splitAtlasDic.Clear();
            for (int i = 0; i < needSaveAtlas.Count; i++)
            {
                bool isUI = false;
                m_tempIsUi.TryGetValue(needSaveAtlas[i], out isUI);
                Debug.LogFormat("Gen atlas:{0}. isUI:{1}.", needSaveAtlas[i], isUI);
                SaveAtlas(needSaveAtlas[i], isUI);
            }

            foreach (var noAtlas in noAtlasList)
            {
                ProcessSpriteSaveInfo(new List<Object>() { noAtlas.Value }, noAtlas.Key);
                Debug.LogFormat("Gen atlas:{0}. isUI:{1}.", noAtlas.Key, true);
            }

            SaveUISpriteCfg();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("Gen end");
        }

        [MenuItem("Tools/贴图/重新生成SpriteCfg")]
        public static void ForceGenSpriteConfig()
        {
            m_hadInit = false;
            //按图集重新生成一下SpriteCfg.bytes
            Init();

            m_uiAtlasMap.Clear();
            var iter = m_allASprites.GetEnumerator();
            while (iter.MoveNext())
            {
                var atlasName = iter.Current.Key;
                var spritesList = iter.Current.Value;
                for (int i = 0; i < spritesList.Count; i++)
                {
                    if (spritesList[i].StartsWith("Assets/UIRaw") || spritesList[i].StartsWith(NoAtlasPathDir))
                    {
                        var spriteName = Path.GetFileNameWithoutExtension(spritesList[i]);
                        if (!m_uiAtlasMap.ContainsKey(spriteName))
                        {
                            m_uiAtlasMap[spriteName] = atlasName;
                        }
                        else
                        {
                            //Debug.LogFormat("repeat spriteName:{0}. atlas:[{1}, {2}]", spriteName, m_uiAtlasMap[spriteName], atlasName);
                            string tips = $"repeat spriteName:{spriteName}. atlas:[{m_uiAtlasMap[spriteName]}, {atlasName}]";
                            //EditorUtility.DisplayDialog("警告！警告！图片命名重复！", tips, "好的");
                            Debug.LogError(tips);
                        }
                    }
                }
            }

            SaveUISpriteCfg();
        }

        #endregion
        
        public static void DoProcessTexture(TextureImporter ti, string path, bool fromImport)
        {
            if (ti == null)
            {
                return;
            }
            if (path.StartsWith("Assets/UIRaw"))
            {
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;

                }
            }

            var androidSetting = ti.GetPlatformTextureSettings("Android");
            var iosSetting = ti.GetPlatformTextureSettings("iPhone");
            var webglSetting = ti.GetPlatformTextureSettings("WebGL");

            if (fromImport && androidSetting.overridden && iosSetting.overridden && webglSetting.overridden)
            {
                //防止死循环reimport
                return;
            }
            
            //MaxSize
            bool changeMaxSize = false;
            int maxSize = 2048;
            bool changeMaxSizeWebGL = false;
            int maxSizeWebGL = 2048;

            if (path.StartsWith("Assets/SceneRaw/Battleground/"))
            {//地图
                changeMaxSize = true;
                maxSize = 1024;
            }
            else if (path.StartsWith("Assets/UIRaw/Background"))
            {
                changeMaxSize = true;
                maxSize = 1024;
            }
            else if (path.StartsWith("Assets/Effect/"))
            {
                if (path.StartsWith("Assets/Effect/Ui/"))
                {
                    changeMaxSizeWebGL = true;
                    maxSizeWebGL = 1024;
                    maxSize = 1024;
                }
                else if (path.StartsWith("Assets/Effect/Textures/HighQuality/"))
                {
                    changeMaxSizeWebGL = true;
                    maxSizeWebGL = 1024;
                    maxSize = 1024;
                }
                else
                {
                    changeMaxSize = true;
                    maxSize = 256;
                }
            }
            else if (path.StartsWith("Assets/ActorModel/"))
            {
                changeMaxSize = true;
                maxSize = 1024;
            }

            //minmap
            ti.mipmapEnabled = false;
            //read/write
            ti.isReadable = false;

            #region android
            if (changeMaxSize && maxSize < androidSetting.maxTextureSize)
            //if (changeMaxSize)
            {
                androidSetting.maxTextureSize = maxSize;
            }
            androidSetting.overridden = true;
            ti.SetPlatformTextureSettings(androidSetting);
            #endregion

            #region ios
            if (ti.textureType == TextureImporterType.Sprite)
            {
                iosSetting.format = TextureImporterFormat.ASTC_5x5;
                iosSetting.compressionQuality = 100;
            }
            if (changeMaxSize && maxSize < iosSetting.maxTextureSize) 
            {
                iosSetting.maxTextureSize = maxSize;
            }
            iosSetting.overridden = true;
            ti.SetPlatformTextureSettings(iosSetting);
            #endregion

            #region webgl
            if (ti.DoesSourceTextureHaveAlpha())
            {
                webglSetting.format = TextureImporterFormat.DXT5;
            }
            else
            {
                webglSetting.format = TextureImporterFormat.DXT1;
            }

            if (changeMaxSizeWebGL && maxSizeWebGL < webglSetting.maxTextureSize)
            {
                webglSetting.maxTextureSize = maxSizeWebGL;
            }
            else if (changeMaxSize && maxSize < webglSetting.maxTextureSize)
            {
                webglSetting.maxTextureSize = maxSize;
            }
            webglSetting.overridden = true;
            ti.SetPlatformTextureSettings(webglSetting);
            #endregion

            ti.SaveAndReimport();
        }
    }

    /// <summary>
    /// 图片打成图集并保存到Atlas的处理。
    /// </summary>
    [AssetProcessorDescription(typeof(SpriteAtlas), ImportAssetTypeFlag.Textures)]
    public class GeneratorSpriteAtlas : AssetProcessor
    {
        public bool needGeneratorPrefab = false;

        public override void OnPostprocess(Object asset, string assetPath)
        {
            SpriteAtlasHelper.DoProcessTexture(AssetImporter.GetAtPath(assetPath) as TextureImporter, assetPath, true);
            SpriteAtlasHelper.OnImportSprite(assetPath);
            AssetDatabase.SaveAssets();
            ImportProfileUserData.AddOrUpdateProcessor(assetPath, this);
            Debug.Log($"[{GetName()}] Added \"<b>{(assetPath)}</b>\" to sprite atlas: \"<b>{SpriteAtlasHelper.GetPackageTag(assetPath)}</b>\"");
        }

        public override void OnDeletedAsset(string assetPath)
        {
            SpriteAtlasHelper.OnDeleteSprite(assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{GetName()}] Deleted \"<b>{(assetPath)}</b>\" to sprite atlas: \"<b>{SpriteAtlasHelper.GetPackageTag(assetPath)}</b>\"");
        }

        public override void OnMovedAsset(Object asset, string sourcePath, string destinationPath)
        {
            SpriteAtlasHelper.OnDeleteSprite(sourcePath);
            SpriteAtlasHelper.OnImportSprite(destinationPath);
            AssetDatabase.SaveAssets();
            ImportProfileUserData.AddOrUpdateProcessor(destinationPath, this);
            Debug.Log($"[{GetName()}] Moved \"<b>{(destinationPath)}</b>\" to sprite atlas: \"<b>{SpriteAtlasHelper.GetPackageTag(destinationPath)}</b>\"");
        }
    }
}