using System;
using System.Collections.Generic;
using AssetPipeline.Import;
using UnityEditor;
using UnityEditor.SpeedTree.Importer;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace AssetPipeline.Processors
{
    [AssetProcessorDescription(typeof(SpriteAtlas), ImportAssetTypeFlag.SpriteAtlases)]
    public class SetAtlasAllSettings : AssetProcessor
    {
        [Header("是否每次导入都触发")] [SerializeField] private bool fireOnEveryImport = false;
        [NonSerialized] private static SpriteAtlasImporter m_DummyImporter;
        [SerializeField] private SpriteAtlasPreprocessorConfig config;


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

            var ti = importer as SpriteAtlasImporter;
            if (ti == null)
            {
                return false;
            }

            return config.IsConfigOK(ti);
        }

        void OnPostprocessSpriteAtlas(string assetPath, SpriteAtlasImporter importer)
        {
            config.PreprocessSpriteAtlas(importer);

            ImportProfileUserData.AddOrUpdateProcessor(assetPath, this);
            Debug.Log($"[{GetName()}] Set All SpriteAtlasAllSettings for <b>{assetPath}</b>");
        }

        public override void OnPostprocessSpriteAtlas(string assetPath, SpriteAtlasImporter importer, SpriteAtlas spriteAtlas)
        {
            OnPostprocessSpriteAtlas(assetPath, importer);
        }
        
        public enum PlatformName
        {
            Android,
            iPhone,
            WebGL,
        }

        [Serializable]
        public class SpriteAtlasPreprocessorPlatformConfig
        {
            public PlatformName platformName;
            public int maxSize = 2048;
            public TextureImporterFormat format = TextureImporterFormat.ASTC_6x6;
            public TextureImporterCompression compression = TextureImporterCompression.Compressed;
            public bool useCrunchCompression = false;
        }

        [Serializable]
        public class SpriteAtlasPreprocessorConfig
        {
            private static readonly string androidPlatform = "Android";
            private static readonly string iPhonePlatform = "iPhone";
            private static readonly string webGLPlatform = "WebGL";
            public static readonly string[] ValidPlatforms = new[] { androidPlatform, iPhonePlatform, webGLPlatform };

            [SerializeField] private bool isEnabled = false;

            [Header("PackingSetting")]
            public int padding = 4;
            public bool enableRotation = true;
            public int blockOffset = 1;
            public bool tightPacking = true;

            [SerializeField] private List<SpriteAtlasPreprocessorPlatformConfig> platformConfigs = new List<SpriteAtlasPreprocessorPlatformConfig>()
            {
                new()
                {
                    platformName = PlatformName.Android, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                    useCrunchCompression = false
                },
                new()
                {
                    platformName = PlatformName.iPhone, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                    useCrunchCompression = false
                },
                new()
                {
                    platformName = PlatformName.WebGL, maxSize = 2048, format = TextureImporterFormat.ASTC_6x6, compression = TextureImporterCompression.Compressed,
                    useCrunchCompression = false
                },
            };

            public bool IsConfigOK(SpriteAtlasImporter importer)
            {
                if (importer == null)
                {
                    return false;
                }
                
                SpriteAtlasPackingSettings packingSettings = importer.packingSettings;

                if (packingSettings.padding != padding)
                {
                    return false;
                }

                if (packingSettings.enableRotation != enableRotation)
                {
                    return false;
                }

                if (packingSettings.blockOffset != blockOffset)
                {
                    return false;
                }

                if (packingSettings.enableTightPacking != tightPacking)
                {
                    return false;
                }

                foreach (var platformName in ValidPlatforms)
                {
                    var setting = platformConfigs.Find(x => x.platformName.ToString() == platformName);
                    if (setting != null)
                    {
                        var orignSetting = importer.GetPlatformSettings(platformName);
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

                        if (orignSetting.crunchedCompression != setting.useCrunchCompression)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public void PreprocessSpriteAtlas(SpriteAtlasImporter importer)
            {
                if (importer == null)
                {
                    return;
                }

                
                var packingSettings = new SpriteAtlasPackingSettings
                {
                    padding = padding,
                    enableRotation = enableRotation,
                    blockOffset = blockOffset,
                    enableTightPacking = tightPacking,
                    enableAlphaDilation = true
                };
                importer.packingSettings = packingSettings;

                foreach (var platformName in ValidPlatforms)
                {
                    var setting = platformConfigs.Find(x => x.platformName.ToString() == platformName);
                    if (setting != null)
                    {
                        var orignSetting = importer.GetPlatformSettings(platformName);

                        if (orignSetting.maxTextureSize > setting.maxSize)
                        {
                            orignSetting.maxTextureSize = setting.maxSize;
                        }

                        orignSetting.format = setting.format;
                        orignSetting.textureCompression = setting.compression;
                        orignSetting.crunchedCompression = setting.useCrunchCompression;
                        orignSetting.overridden = true;

                        importer.SetPlatformSettings(orignSetting);
                    }
                }
            }
        }
    }
}