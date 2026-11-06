# Blender 到 Unity 自动导入插件

本仓库包含“导入unity”插件的两个端：

- `Blender端/`：Blender 4.0+ 插件源码、安装包和 Blender 端说明。
- `Unity端/`：可直接合并到 Unity 项目根目录的 Unity Editor 插件文件，以及可选测试。

## 安装

### Blender 端

在 Blender 的 `Edit > Preferences > Add-ons > Install from Disk` 中选择
`Blender端/导入unity-1.0.0.zip`，启用“导入unity”，然后在 3D View 按 `N` 打开侧栏。

### Unity 端

`Unity端` 已按“解压到 Unity 项目根目录即可使用”的结构整理。将其中的 `Assets` 目录合并到目标项目的 `Assets` 目录，核心插件会自动位于 `Assets/Plugins/BlenderUnityAutoImport/Editor`。详细步骤见 `Unity端/README.md`。

Unity 端核心程序集不引用 acepro 项目专属程序集，普通 Unity 项目无需创建程序集或修改代码。`Unity端/可选测试/acepro/` 仅供 acepro 项目验证使用，不属于通用安装路径。

## 目录边界

本仓库只发布导入插件本身，不包含 acepro 项目的其他游戏代码、资产、材质或 Prefab。
