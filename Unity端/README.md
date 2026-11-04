# Blender 到 Unity 自动导入

## 所有权与入口

Blender 插件源码位于本机交付目录 `D:\Blender插件\导入unity`，插件名为“导入unity”，支持 Blender 4.0+。Unity Editor 侧实现位于 `Assets/Game/Editor/BlenderUnityAutoImport.cs`，属于 Editor-only 资产制作，不进入运行时业务模块。

在 Blender 的 View3D 按 `N` 打开“导入unity”面板，填写 Unity 项目根目录、FBX 输出目录、材质搜索目录和 Prefab 保存目录。三个资产目录必须位于该项目 `Assets/` 下；材质目录必须已存在。选中一个或多个 Mesh 后导出。Unity Editor 必须打开对应项目，检测到配置侧车后才会自动完成后续处理。

Blender 输出 `<name>.fbx` 和 `<name>.fbx.acepro-import.json`。配置 schema v1 只保存 Unity 项目相对路径、材质搜索目录、Prefab 目录和 Prefab 名称，不写机器绝对路径。FBX 固定选中对象/仅 Mesh、scale 1、FBX Unit Scale、Forward -Z、Up Y、Apply Unit、Use Space Transform、Apply Transform；自定义属性关闭。当前导出器缺少任何必要属性时会停止导出。

Windows 下 Unity AssetDatabase 路径按项目实际大小写保存。导入器会兼容旧版插件生成的小写 sidecar，并按 AssetDatabase 将材质搜索和 Prefab 目录逐级映射回现有资产目录的真实大小写。

## 导入行为

`BlenderUnityModelImportPostprocessor` 只对有有效配置侧车的 FBX 排队，并用 `EditorApplication.delayCall` 避免在 AssetDatabase 导入回调中重入。处理阶段临时使用 `ImportStandard`；完成材质 Remap 与 Prefab 保存后将材质导入模式切为 `None`。按源材质名称在指定目录递归精确查找：

- 唯一匹配：将项目材质写入 FBX ModelImporter 的 External Object Remap。
- 无匹配：保留 FBX 自带导入材质，记录 warning，不创建 `.mat`。
- 多个同名匹配：输出全部候选路径并拒绝本轮 Prefab 更新，不随机选取。

Remap 变化后保存并重新导入 FBX；下一次回调再创建 Prefab。Prefab 根名使用配置名称，保存到配置目录。每个含 `MeshRenderer` 或 `SkinnedMeshRenderer` 的 GameObject 是一个部件，各自有且只有一个非 Trigger BoxCollider；包围盒使用该节点局部 Mesh bounds / Skinned `localBounds`。零厚度方向最小扩为 1 mm。Collider 在 Prefab，不写回 FBX。

完整处理顺序为：使用 `ImportStandard` 导入材质并应用 Remap、保存 Prefab，然后把 FBX 的 `materialImportMode` 设为 `None`。Prefab 所有权 sidecar 记录 FBX 与导入配置的 SHA-256；模式切为 None 后由此识别本轮已完成，避免自触发 Reimport 循环。FBX 或配置内容变化后才重新启用标准材质导入并重建 Prefab。

同名 Prefab 不存在时创建；已存在时必须有 `<prefab>.acepro-owner.json` 且其中 FBX 路径与当前来源相同才允许更新。无所有权标记、标记损坏或来源不同均拒绝覆盖。工具不创建材质、不做 Shader 转换、不拆分凹形碰撞体、不配置骨架/动画。

## 验证

定向 EditMode 程序集为 `AcePro.BlenderUnityImport.Tests`，覆盖指定目录与大小写精确材质搜索、Windows sidecar 路径大小写兼容、None 模式自 Reimport 幂等、路径逃逸拒绝、各 Mesh 节点单独 Collider、空节点不添加、重复调用不重复添加及零厚度处理。运行入口：Unity MCP EditMode 选择该程序集，或选择 `BlenderUnityAutoImportTests`。

Blender 4.0.2 本机验证：插件可导入/注册，FBX exporter RNA 中全部必需属性存在；验证真实菜单操作时还应检查导出的 FBX 朝向、Unity 尺寸及复杂蒙皮边界。该插件不是完整材质转换器，生成 Prefab 后仍需检查实际材质外观及 Collider 对玩法的影响。
