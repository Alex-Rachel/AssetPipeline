using System;
using System.Collections.Generic;
using AssetPipeline.Import;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline.Processors
{
    [AssetProcessorDescription(typeof(Texture), ImportAssetTypeFlag.Textures)]
    public class SetTextureFormat : AssetProcessor
    {
        [Header("是否每次导入都触发")] [SerializeField] private bool fireOnEveryImport = false;
        public bool NeedCheckMinMaxTextureSize = false;
        
        [NonSerialized] private static TextureImporter m_DummyImporter;
        [SerializeField] private TexturePreprocessorConfig config;


        public override bool FireOnEveryImport()
        {
            return fireOnEveryImport;
        }

        public override bool IsConfigOK(AssetImporter importer)
        {
            if (importer == null)
            {
                return false;
            }

            var ti = importer as TextureImporter;
            if (ti == null)
            {
                return false;
            }

            return config.IsConfigOK(ti);
            ;
        }

        void OnPostprocessTexture(string assetPath, TextureImporter importer)
        {
            config.PreprocessTexture(this, importer);

            ImportProfileUserData.AddOrUpdateProcessor(assetPath, this);
            Debug.Log($"[{GetName()}] Set TextureFormat for <b>{assetPath}</b>");
        }

        public override void OnPostprocessTexture(string assetPath, TextureImporter importer, Texture2D tex)
        {
            OnPostprocessTexture(assetPath, importer);
        }


        public enum PlatformName
        {
            Android,
            iPhone,
            WebGL,
        }
        
        public enum CompressionQuality
        {
            Fast = 0,
            Normal = 50,
            High = 100,
        }

        [Serializable]
        public class TexturePreprocessorPlatformConfig
        {
            public PlatformName platformName;
            public int maxSize = 2048;
            public TextureImporterFormat format = TextureImporterFormat.ASTC_6x6;
            public TextureImporterCompression compression = TextureImporterCompression.Compressed;
            public CompressionQuality compressionQuality = CompressionQuality.Normal;
        }

        [Serializable]
        public class TexturePreprocessorConfig
        {
            private static readonly string androidPlatform = "Android";
            private static readonly string iPhonePlatform = "iPhone";
            private static readonly string webGLPlatform = "WebGL";
            public static readonly string[] ValidPlatforms = new[] { androidPlatform, iPhonePlatform, webGLPlatform };

            [SerializeField] private bool isEnabled = false;

            [Header("Advanced")] [SerializeField] private bool readWriteEnabled;
            [SerializeField] private bool generateMipMaps = false;


            [SerializeField] private List<TexturePreprocessorPlatformConfig> platformConfigs = new List<TexturePreprocessorPlatformConfig>()
            {
                new()
                {
                    platformName = PlatformName.Android, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                },
                new()
                {
                    platformName = PlatformName.iPhone, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                },
                new()
                {
                    platformName = PlatformName.WebGL, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                },
            };

            public bool IsConfigOK(TextureImporter importer)
            {
                if (importer == null)
                {
                    return false;
                }

                if (importer.isReadable != readWriteEnabled)
                {
                    return false;
                }

                if (importer.mipmapEnabled != generateMipMaps)
                {
                    return false;
                }

                foreach (var platformName in ValidPlatforms)
                {
                    var setting = platformConfigs.Find(x => x.platformName.ToString() == platformName);
                    if (setting != null)
                    {
                        var orignSetting = importer.GetPlatformTextureSettings(platformName);
                        if (orignSetting.maxTextureSize > setting.maxSize)
                        {
                            return false;
                        }

                        if (orignSetting.format != setting.format)
                        {
                            return false;
                        }

                        if (orignSetting.textureCompression != setting.compression)
                        {
                            return false;
                        }
                        
                        if (orignSetting.compressionQuality != (int)setting.compressionQuality)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public void PreprocessTexture(SetTextureFormat setTextureFormat,TextureImporter importer)
            {
                if (importer == null)
                {
                    return;
                }

                bool isDirty = false;

                if (importer.isReadable != readWriteEnabled)
                {
                    importer.isReadable = readWriteEnabled;
                    isDirty = true;
                }

                if (importer.mipmapEnabled != generateMipMaps)
                {
                    importer.mipmapEnabled = generateMipMaps;
                    isDirty = true;
                }

                int minMaxTextureSize = platformConfigs[0].maxSize;
                if (setTextureFormat.NeedCheckMinMaxTextureSize)
                {
                    var mainAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath.FixPathSeparators()) as Texture2D;
                    if (mainAsset != null)
                    {
                        minMaxTextureSize = Mathf.Min(minMaxTextureSize, Mathf.Max(mainAsset.width, mainAsset.height));
                        minMaxTextureSize = Mathf.Min(minMaxTextureSize, importer.maxTextureSize);
                    }
                }
                

                foreach (var platformName in ValidPlatforms)
                {
                    var setting = platformConfigs.Find(x => x.platformName.ToString() == platformName);
                    if (setting != null)
                    {
                        var orignSetting = importer.GetPlatformTextureSettings(platformName);

                        if (orignSetting.maxTextureSize > setting.maxSize)
                        {
                            if (setTextureFormat.NeedCheckMinMaxTextureSize)
                            {
                                orignSetting.maxTextureSize = minMaxTextureSize;
                            }
                            else
                            {
                                orignSetting.maxTextureSize = setting.maxSize;
                            }
                            isDirty = true;
                        }

                        if (orignSetting.format != setting.format)
                        {
                            orignSetting.format = setting.format;
                            isDirty = true;
                        }

                        if (orignSetting.textureCompression != setting.compression)
                        {
                            orignSetting.textureCompression = setting.compression;
                            isDirty = true;
                        }
                        
                        if (orignSetting.compressionQuality != (int)setting.compressionQuality)
                        {
                            orignSetting.compressionQuality = (int)setting.compressionQuality;
                            isDirty = true;
                        }

                        if (orignSetting.overridden == false)
                        {
                            orignSetting.overridden = true;
                            isDirty = true;
                        }

                        if (isDirty)
                        {
                            importer.SetPlatformTextureSettings(orignSetting);
                        }
                    }
                }

                if (isDirty)
                {
                    importer.SaveAndReimport();
                }
            }
        }
    }
}