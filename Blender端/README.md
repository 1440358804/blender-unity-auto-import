# 导入unity

Blender 4.0+ 插件。它将选中的网格物体导出到 Unity 项目的 `Assets/`，并在 FBX 同目录写入 Unity 可读的导入配置。Unity Editor 需要打开该项目，才能自动绑定已存在材质并生成 Prefab。

## 安装

1. 在 Blender 打开 Edit > Preferences > Add-ons，点击右上角菜单并选择 Install from Disk。
2. 选择 `导入unity-1.0.0.zip` 并点击 Install Add-on。
3. 在 Add-ons 列表搜索并启用“导入unity”。
4. 在 3D View 按 `N`，打开“导入unity”侧栏。

## 使用

1. 选择 Unity 项目根目录（应包含 `ProjectSettings/ProjectVersion.txt`）。
2. 点击“根据 Unity 根目录生成默认路径”，插件会生成以下默认位置：`Assets/Game/Art/Environment/TokyoRooftops`、`Assets/Game/WorldMap/Props/Materials` 和 `Assets/Game/WorldMap/Props/TokyoRooftops`。
3. 三个目录和模型名称仍可手动编辑；选中要导出的物体后点击“导入名称”，即可把最后选中的网格物体名称填入“模型名称”。多选时使用最后选中的物体。
4. 选中要导出的网格物体，点击“导出并导入 Unity”。
4. Unity 自动生成/更新对应 Prefab。每个含 MeshRenderer 或 SkinnedMeshRenderer 的 GameObject 各有一个 BoxCollider。

材质只在指定目录按精确名称匹配。唯一匹配会绑定；找不到时保留 FBX 导入材质且不创建 `.mat`；重名候选会报错并停止处理。Prefab 目录中已存在但不属于本插件的同名 Prefab 不会被覆盖。

## FBX 参数

- 仅导出选中物体中的 Mesh。
- Scale 1.00，Apply Scalings = FBX Unit Scale。
- Forward = -Z Forward，Up = Y Up。
- Apply Unit Scale、Use Space Transform、Apply Transform 开启。
- Custom Properties 关闭。

插件会检查当前 FBX exporter 是否提供这些必要属性；缺少时停止导出。材质贴图打包、动画和骨架不是本插件的处理目标。
