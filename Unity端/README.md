# Unity 端安装说明

## 直接解压安装

本目录已经按 Unity 项目根目录布局整理。下载仓库压缩包后，将 `Unity端` 目录中的内容解压或复制到目标 Unity 项目根目录，并允许其中的 `Assets` 目录与项目现有 `Assets` 目录合并。

例如目标项目是：

```text
D:\YHY\acepro\
```

复制完成后，核心文件应位于：

```text
D:\YHY\acepro\Assets\Plugins\BlenderUnityAutoImport\Editor\BlenderUnityAutoImport.cs
D:\YHY\acepro\Assets\Plugins\BlenderUnityAutoImport\Editor\BlenderUnityAutoImport.Editor.asmdef
```

不要把 `Unity端` 文件夹本身作为子文件夹放入 `Assets`，也不要形成 `Assets\Assets\Plugins` 这样的嵌套目录。

重新打开或切回 Unity 后，Unity 会自动导入脚本和 `.asmdef`，不需要手动挂载组件、创建程序集或修改代码。等待 Console 中编译完成且没有编译错误，即可使用 Blender 端导出流程。

## 使用前提

- Unity 项目必须能够正常编译。
- Blender 端需要另外安装 `Blender端/导入unity-1.0.0.zip`。
- Blender 导出的 FBX 和 `.fbx.acepro-import.json` 必须位于项目 `Assets/` 内。
- Unity Editor 需要打开该项目，才能自动处理 FBX。

## 自动处理内容

检测到有效导入清单后，Unity 端会自动按清单绑定同名材质、创建 Prefab、为每个含网格渲染器的模型部件添加一个 `Box Collider`，并在流程完成后将 FBX 的材质导入模式改为 `None`，然后删除已完成的导入清单。

材质匹配规则如下：指定目录内唯一的同名材质会被绑定；找不到同名材质时保留 FBX 导入材质且不创建新的材质；发现多个同名材质时停止本轮 Prefab 更新并输出错误。

## 可选测试

`可选测试/acepro/` 保留了 acepro 项目的专项 EditMode 测试，仅用于 acepro 本身的验证，不属于通用安装路径。它的程序集引用 `AcePro.Game.Editor`，其他 Unity 项目不应直接复制该目录。
