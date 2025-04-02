using System;
using System.Collections.Generic;
using AssetPipeline.Import;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline.Processors
{
    [AssetProcessorDescription(typeof(Texture), ImportAssetTypeFlag.Textures)]
    public class SetTextureAllSettings : AssetProcessor
    {
        [Header("是否每次导入都触发")] [SerializeField] private bool fireOnEveryImport = false;
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
        }

        void OnPostprocessTexture(string assetPath, TextureImporter importer)
        {
            config.PreprocessTexture(importer);

            ImportProfileUserData.AddOrUpdateProcessor(assetPath, this);
            Debug.Log($"[{GetName()}] Set All TextureAllSettings for <b>{assetPath}</b>");
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

        [Serializable]
        public class TexturePreprocessorPlatformConfig
        {
            public PlatformName platformName;
            public int maxSize = 2048;
            public TextureImporterFormat format = TextureImporterFormat.ASTC_6x6;
            public TextureImporterCompression compression = TextureImporterCompression.Compressed;
            public bool useCrunchCompression = false;
        }

        [Serializable]
        public class TexturePreprocessorConfig
        {
            private static readonly string androidPlatform = "Android";
            private static readonly string iPhonePlatform = "iPhone";
            private static readonly string webGLPlatform = "WebGL";
            public static readonly string[] ValidPlatforms = new[] { androidPlatform, iPhonePlatform, webGLPlatform };

            [SerializeField] private bool isEnabled = false;

            [SerializeField] private TextureImporterType textureType = TextureImporterType.Default;
            [SerializeField] private TextureImporterShape textureShape = TextureImporterShape.Texture2D;
            [SerializeField] private bool sRGBTexture = true;
            [SerializeField] private TextureImporterAlphaSource alphaSource = TextureImporterAlphaSource.FromInput;
            [SerializeField] private bool alphaIsTransparency;
            [SerializeField] private bool ignorePNGFileGamma;

            [Header("Advanced")] [SerializeField] private TextureImporterNPOTScale nonPowerOf2 = TextureImporterNPOTScale.ToNearest;
            [SerializeField] private bool readWriteEnabled;
            [SerializeField] private bool streamingMipmaps;
            [SerializeField] private bool vitrualTextureOnly;
            [SerializeField] private bool generateMipMaps = false;
            [SerializeField] private bool borderMipMaps;
            [SerializeField] private TextureImporterMipFilter mipmapFilter = TextureImporterMipFilter.BoxFilter;
            [SerializeField] private bool mipMapsPreserveCoverage;
            [SerializeField] private bool fadeoutMipMaps;

            [SerializeField] private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
            [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

            [SerializeField, UnityEngine.Range(0, 16)]
            private int anisoLevel = 1;

            [SerializeField] private List<TexturePreprocessorPlatformConfig> platformConfigs = new List<TexturePreprocessorPlatformConfig>()
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

            public bool IsConfigOK(TextureImporter importer)
            {
                if (importer == null)
                {
                    return false;
                }

                if (importer.textureType != textureType)
                {
                    return false;
                }

                if (importer.textureShape != textureShape)
                {
                    return false;
                }

                if (importer.sRGBTexture != sRGBTexture)
                {
                    return false;
                }

                if (importer.alphaSource != alphaSource)
                {
                    return false;
                }

                if (importer.ignorePngGamma != ignorePNGFileGamma)
                {
                    return false;
                }

                if (importer.npotScale != nonPowerOf2)
                {
                    return false;
                }

                if (importer.isReadable != readWriteEnabled)
                {
                    return false;
                }

                if (importer.streamingMipmaps != streamingMipmaps)
                {
                    return false;
                }

                if (importer.vtOnly != vitrualTextureOnly)
                {
                    return false;
                }

                if (importer.mipmapEnabled != generateMipMaps)
                {
                    return false;
                }

                if (importer.borderMipmap != borderMipMaps)
                {
                    return false;
                }

                if (importer.mipmapFilter != mipmapFilter)
                {
                    return false;
                }

                if (importer.mipMapsPreserveCoverage != mipMapsPreserveCoverage)
                {
                    return false;
                }

                if (importer.fadeout != fadeoutMipMaps)
                {
                    return false;
                }

                if (importer.wrapMode != wrapMode)
                {
                    return false;
                }

                if (importer.filterMode != filterMode)
                {
                    return false;
                }

                if (importer.anisoLevel != anisoLevel)
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

                        if (orignSetting.crunchedCompression != setting.useCrunchCompression)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public void PreprocessTexture(TextureImporter importer)
            {
                if (importer == null)
                {
                    return;
                }

                importer.textureType = textureType;

                importer.textureShape = textureShape;
                importer.sRGBTexture = sRGBTexture;
                importer.alphaSource = alphaSource;
                importer.alphaIsTransparency = alphaIsTransparency;
                importer.ignorePngGamma = ignorePNGFileGamma;
                importer.npotScale = nonPowerOf2;
                importer.isReadable = readWriteEnabled;
                importer.streamingMipmaps = streamingMipmaps;
                importer.vtOnly = vitrualTextureOnly;
                importer.mipmapEnabled = generateMipMaps;
                importer.borderMipmap = borderMipMaps;
                importer.mipmapFilter = mipmapFilter;
                importer.mipMapsPreserveCoverage = mipMapsPreserveCoverage;
                importer.fadeout = fadeoutMipMaps;
                importer.wrapMode = wrapMode;
                importer.filterMode = filterMode;
                importer.anisoLevel = anisoLevel;


                foreach (var platformName in ValidPlatforms)
                {
                    var setting = platformConfigs.Find(x => x.platformName.ToString() == platformName);
                    if (setting != null)
                    {
                        var orignSetting = importer.GetPlatformTextureSettings(platformName);

                        if (orignSetting.maxTextureSize > setting.maxSize)
                        {
                            orignSetting.maxTextureSize = setting.maxSize;
                        }

                        orignSetting.format = setting.format;
                        orignSetting.textureCompression = setting.compression;
                        orignSetting.crunchedCompression = setting.useCrunchCompression;
                        orignSetting.overridden = true;

                        importer.SetPlatformTextureSettings(orignSetting);
                    }
                }
            }
        }
    }
}