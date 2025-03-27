# Unity 资产处理管线

- [README 中文](./README.md)
- [README English](./README_EN.md)

该工具是Unity中基于规则的资产后处理器解决方案，旨在开发可重复使用的处理器，这些处理器可以在资产导入时自动应用。

## 资产导入配置文件

资产导入配置文件是继承自 `ScriptableObject` 的配置文件，用于存储基于路径规则的资产导入策略。这些规则包含：
- 基础文件夹路径
- 要处理的资产类型
- 匹配资产后需要执行的处理器列表

### 支持资产类型

支持纹理、模型、音频、视频、字体、动画、材质、预制件、Sprite图集等多种类型。_其他_ 类型可用于匹配指定文件扩展名集合外的任意资产。

## 路径变量系统

在资产导入配置文件的路径定义和文件过滤器中，可以使用动态变量（使用 `{}` 包裹变量名）。例如路径 `Assets/3DGamekit/Art/Textures/Characters/{characterName}/` 将自动捕获文件夹名作为 `characterName` 变量值，该变量可在设置资源包名、标签等处理器中使用。

变量支持强制命名规范校验（通过后缀指定）：

| 命名规范        | 变量后缀     | 示例                |
| :------------: | :---------: | :----------------: |
| 无规范         | `:\none`    | The quick brown fox |
| 蛇形命名法      | `:\snake`   | the_quick_brown_fox |
| 大蛇形命名法    | `:\usnake`  | THE_QUICK_BROWN_FOX |
| 帕斯卡命名法    | `:\pascal`  | TheQuickBrownFox    |
| 驼峰命名法      | `:\camel`   | theQuickBrownFox    |
| 短横线命名法    | `:\kebab`   | the-quick-brown-fox |
| 全大写          | `:\upper`   | THE QUICK BROWN FOX |
| 全小写          | `:\lower`   | the quick brown fox |

后缀也支持正则表达式校验（例如 `:\d+` 强制数字格式）。在处理器中使用变量时，可通过后缀转换变量值的格式。

## 资产处理器

资产处理器是在资产导入时自动执行的模块化功能单元，当前包含以下核心处理器：

| 处理器            | 适用类型      | 功能描述                                                                 |
| :--------------: | :----------: | :---------------------------------------------------------------------: |
| 应用预设          | 纹理/模型/音频等 | 应用预定义的导入设置                                                     |
| 设置资源包        | 全部          | 指定AssetBundle名称和变体                                                |
| 设置资源标签      | 全部          | 添加资源标签                                                             |
| 添加到Sprite图集 | 纹理          | 自动创建/更新Sprite图集                                                  |
| 纹理通道打包      | 纹理          | 将纹理打包到其他纹理的指定通道                                            |
| 材质提取          | 模型          | 从模型自动提取材质                                                       |
| 材质配置          | 模型          | 自动配置材质属性并关联纹理                                                |
| 网格数据精简      | 模型          | 移除顶点颜色、冗余UV通道等非必要数据                                       |
| 变换重置          | 模型          | 重置模型的坐标/旋转/缩放                                                 |
| 预制件生成        | 模型          | 自动生成预制件并配置渲染参数                                               |

### .meta文件用户数据

多数处理器会在.meta文件中记录处理状态，防止重复处理。

### 自定义处理器

可通过继承 `AssetProcessor` 类创建自定义处理器，示例：

```csharp
// 资产标签设置处理器示例
using System.Linq;
using Daihenka.AssetPipeline.Import;
using UnityEditor;
using UnityEngine;

[AssetProcessorDescription("FilterByLabel@2x")]
public class SetAssetLabels : AssetProcessor
{
    [SerializeField] string[] Labels;

    public override void OnPostprocess(Object asset, string assetPath)
    {
        var assetLabels = AssetDatabase.GetLabels(asset).ToHashSet();
        assetLabels.AddRange(Labels);
        AssetDatabase.SetLabels(asset, assetLabels.ToArray());
        
        ImportProfileUserData.AddOrUpdateProcessor(assetPath, this);
    }
}
```

## 使用指南

通过菜单项 `Tools > Asset Pipeline > Import Profiles` 打开配置界面：

![配置界面示意图](https://user-images.githubusercontent.com/6211561/115570406-5fd1c100-a2be-11eb-8046-63deaf70f3f3.png)

![配置文件列表](https://user-images.githubusercontent.com/6211561/115570335-521c3b80-a2be-11eb-83a6-486bdb908c7a.png)

在此界面中：
1. 点击 `Create New`（新建）按钮创建导入配置文件
2. 双击配置文件条目即可打开编辑器进行配置

![配置文件编辑器](https://user-images.githubusercontent.com/6211561/115570637-91e32300-a2be-11eb-8b4d-352a371cd4a0.png)

配置项说明：
- 基础路径设置：指定资产扫描的根目录
- 资产过滤器：配置类型匹配规则与路径变量
- 处理器列表：添加/管理要执行的自动化处理流程

