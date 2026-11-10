# 导入unity

Blender 4.0+ 插件。它将选中的网格物体导出到 Unity 项目的 `Assets/`，并在 FBX 同目录写入 Unity 可读的导入配置。顶部“导入规格”下拉列表提供“自定义”和 6 个内置导入预设；预设路径可直接使用，也可以修改并保存在当前 `.blend` 中。

当前版本：**2.0.0**

## 安装

1. 在 Blender 打开 Edit > Preferences > Add-ons，点击右上角菜单并选择 Install from Disk。
2. 选择 `导入unity-2.0.0.zip` 并点击 Install Add-on。
3. 在 Add-ons 列表搜索并启用“导入unity”。
4. 在 3D View 按 `N`，打开“导入unity”侧栏。

## 使用

1. 在最上方选择 Unity 项目根目录（应包含 `ProjectSettings/ProjectVersion.txt`）。
2. 在“导入规格”下拉列表中选择一种模式：
   - **自定义**：填写三条自定义路径。
   - **静态物体 / 动态物体 / 可交换物体 / 无碰撞物体 / 装饰建筑 / 建筑外墙**：插件已按预设填好内置路径，通常无需修改。
3. 选中预设时，界面会显示“预设路径通常无需修改”。三条路径仍然可以编辑，修改会保存在当前 `.blend` 文件中；点击“恢复默认”可恢复插件内置值。
4. 选中要导出的网格物体，填写或通过“导入名称”填入“模型名称”，点击“导出并导入 Unity”。
5. “模型名称 / 导入名称 / 导出并导入 Unity”在所有模式下固定显示，不随下拉列表变化。

下拉列表是单选、排他的，同一时间只显示当前模式的路径内容。自定义路径与每个预设的路径互相独立，切换模式不会丢失各自的修改。

## 内置预设

预设定义在 `__init__.py` 的 `SPEC_PRESETS` 中，根路径由 `PRESET_ROOT` 控制，当前为 `D:\YHY\acepro`。所有预设共用：

- FBX 导入位置：`D:\YHY\acepro\Assets\Game\Art\Environment\TokyoRooftops`
- 材质搜索位置：`D:\YHY\acepro\Assets\Game\WorldMap\Materials`

| 预设 | Prefab 保存位置 |
| --- | --- |
| 静态物体 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\Static\TokyoRooftops` |
| 动态物体 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\DynamicProps\TokyoRooftops` |
| 可交换物体 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\InteractiveProps\TokyoRooftops` |
| 无碰撞物体 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\NoCollisionProps\TokyoRooftops` |
| 装饰建筑 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\AttachmentsModules\TokyoRooftops` |
| 建筑外墙 | `D:\YHY\acepro\Assets\Game\WorldMap\Facilities\ExteriorWalls\TokyoRooftops` |

如果 Unity 项目移动，可以直接修改界面中的路径（会保存在 `.blend` 中），或修改 `PRESET_ROOT` / `SPEC_PRESETS` 常量。预设路径必须位于所选 Unity 项目的 `Assets` 内，材质搜索位置必须是已存在的文件夹，否则导出会报错。

## 与 1.x 的差异

- 移除“创建按键 / 保存按键 / 已保存按键”及其数据。
- 移除“根据 Unity 根目录生成默认路径”。
- 新增顶部“导入规格”下拉列表、6 个内置预设和“恢复默认”。
- 1.x 保存在 `.blend` 中的按键配置不再显示。

材质只在指定目录按精确名称匹配。唯一匹配会绑定；找不到时保留 FBX 导入材质且不创建 `.mat`；重名候选会报错并停止处理。Prefab 目录中已存在但不属于本插件的同名 Prefab 不会被覆盖。

## FBX 参数

- 仅导出选中物体中的 Mesh。
- Scale 1.00，Apply Scalings = FBX Unit Scale。
- Forward = -Z Forward，Up = Y Up。
- Apply Unit Scale、Use Space Transform、Apply Transform 开启。
- Custom Properties 关闭。

插件会检查当前 FBX exporter 是否提供这些必要属性；缺少时停止导出。材质贴图打包、动画和骨架不是本插件的处理目标。

## 版本记录

- **2.0.0**：顶部“导入规格”下拉列表，新增 6 个内置预设、自定义模式、每个预设可编辑并保存、恢复默认；移除按键面板和默认路径生成。
- **1.1.0**：新增“创建按键”、可折叠按键面板、自定义按键名称、“保存按键”及面板外同名按键。
- **1.0.0**：初始版本。

Blender 端安装包文件名带版本号；每次发布新安装包时都会递增版本号。
