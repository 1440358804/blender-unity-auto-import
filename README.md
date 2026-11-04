# Blender 到 Unity 自动导入插件

本仓库包含“导入unity”插件的两个端：

- `Blender端/`：Blender 4.0+ 插件源码、安装包和 Blender 端说明。
- `Unity端/`：Unity Editor 导入器源码及其 EditMode 专项测试。

## 安装

### Blender 端

在 Blender 的 `Edit > Preferences > Add-ons > Install from Disk` 中选择
`Blender端/导入unity-1.0.0.zip`，启用“导入unity”，然后在 3D View 按 `N` 打开侧栏。

### Unity 端

将 `Unity端/Editor/BlenderUnityAutoImport.cs` 复制到目标 Unity 项目的 Editor-only
程序集目录中。该脚本依赖 UnityEditor、UnityEngine 和 AssetDatabase；它不应放入运行时程序集。

测试文件可复制到目标项目的 EditMode 测试程序集。仓库中的
`AcePro.BlenderUnityImport.Tests.asmdef` 引用了本项目的 `AcePro.Game.Editor`，属于 acepro
项目的测试配置；在其他 Unity 项目中应按目标项目的 Editor 程序集名称调整或重新创建测试程序集。

Blender 导出的 FBX 旁边需要存在 `.fbx.acepro-import.json` 配置清单。Unity Editor 打开项目后，
导入器会按清单绑定同名材质、创建 Prefab、为每个模型部件添加 Box Collider，并在流程完成后
将 FBX 材质导入模式改为 `None`，随后删除已完成的配置清单。

## 目录边界

本仓库只发布导入插件本身，不包含 acepro 项目的其他游戏代码、资产、材质或 Prefab。
