bl_info = {
    "name": "导入unity",
    "author": "OpenAI",
    "version": (2, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > 导入unity",
    "description": "Export selected mesh objects to a Unity project with built-in import presets",
    "category": "Import-Export",
}

import json
import os
import tempfile
import bpy
from bpy.props import EnumProperty, StringProperty


SPEC_CUSTOM = "CUSTOM"

# Built-in preset paths. Change PRESET_ROOT here if the Unity project moves.
PRESET_ROOT = r"D:\YHY\acepro"
PRESET_FBX_PATH = PRESET_ROOT + r"\Assets\Game\Art\Environment\TokyoRooftops"
PRESET_MATERIAL_PATH = PRESET_ROOT + r"\Assets\Game\WorldMap\Materials"

SPEC_FIELDS = (
    ("fbx_directory", "FBX 导入位置"),
    ("material_directory", "材质搜索位置"),
    ("prefab_directory", "Prefab 保存位置"),
)

# (stable id, dropdown label, (fbx path, material path, prefab path))
SPEC_PRESETS = (
    ("STATIC", "静态物体", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\Static\TokyoRooftops",
    )),
    ("DYNAMIC", "动态物体", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\DynamicProps\TokyoRooftops",
    )),
    ("INTERACTIVE", "可交换物体", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\InteractiveProps\TokyoRooftops",
    )),
    ("NO_COLLISION", "无碰撞物体", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\NoCollisionProps\TokyoRooftops",
    )),
    ("DECORATION", "装饰建筑", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\AttachmentsModules\TokyoRooftops",
    )),
    ("EXTERIOR_WALL", "建筑外墙", (
        PRESET_FBX_PATH,
        PRESET_MATERIAL_PATH,
        PRESET_ROOT + r"\Assets\Game\WorldMap\Facilities\ExteriorWalls\TokyoRooftops",
    )),
)

SPEC_DEFAULTS = {spec_id: paths for spec_id, _, paths in SPEC_PRESETS}

SPEC_ITEMS = (
    (SPEC_CUSTOM, "自定义", "使用三条自定义路径"),
) + tuple((spec_id, label, "使用预设: " + label) for spec_id, label, _ in SPEC_PRESETS)


def _normal(path):
    return os.path.normcase(os.path.realpath(bpy.path.abspath(path)))


def _absolute(path):
    # Keep the filesystem's original casing for Unity asset paths in the sidecar.
    return os.path.realpath(bpy.path.abspath(path))


def _inside(path, parent):
    try:
        return os.path.commonpath((_normal(path), _normal(parent))) == _normal(parent)
    except ValueError:
        return False


def _last_selected_mesh(context):
    active = context.view_layer.objects.active
    if active is not None and active.select_get() and active.type == 'MESH':
        return active
    meshes = [obj for obj in context.selected_objects if obj.type == 'MESH']
    return meshes[-1] if meshes else None


def _preset_property_name(spec_id, field):
    return "unityimport_preset_" + spec_id.lower() + "_" + field


def _custom_property_name(field):
    return "unityimport_custom_" + field


def _export_directories(scene):
    spec = scene.unityimport_spec
    if spec in SPEC_DEFAULTS:
        return tuple(
            getattr(scene, _preset_property_name(spec, field))
            for field, _ in SPEC_FIELDS
        )
    return tuple(getattr(scene, _custom_property_name(field)) for field, _ in SPEC_FIELDS)


class UNITYIMPORT_OT_import_name(bpy.types.Operator):
    bl_idname = "unityimport.import_name"
    bl_label = "导入名称"
    bl_description = "将最后选中的物体名称填入模型名称；多选时使用最后选中的物体"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        selected = _last_selected_mesh(context)
        if selected is None:
            self.report({'ERROR'}, "请至少选择一个网格物体")
            return {'CANCELLED'}
        context.scene.unityimport_model_name = bpy.path.clean_name(selected.name)
        self.report({'INFO'}, "已导入模型名称: " + selected.name)
        return {'FINISHED'}


class UNITYIMPORT_OT_reset_preset(bpy.types.Operator):
    bl_idname = "unityimport.reset_preset"
    bl_label = "恢复预设默认路径"
    bl_description = "把当前预设的三条路径恢复为插件内置默认值"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        spec = scene.unityimport_spec
        defaults = SPEC_DEFAULTS.get(spec)
        if defaults is None:
            self.report({'ERROR'}, "当前不是预设模式")
            return {'CANCELLED'}
        for (field, _), value in zip(SPEC_FIELDS, defaults):
            setattr(scene, _preset_property_name(spec, field), value)
        self.report({'INFO'}, "已恢复预设默认路径")
        return {'FINISHED'}


class UNITYIMPORT_OT_export(bpy.types.Operator):
    bl_idname = "unityimport.export_selected"
    bl_label = "导出并导入 Unity"
    bl_description = "导出选中的网格，并通知打开的 Unity 项目自动绑定材质及生成 Prefab"
    bl_options = {'REGISTER'}

    def execute(self, context):
        scene = context.scene
        project = _normal(scene.unityimport_project_root)
        if not os.path.isdir(project) or not os.path.isfile(os.path.join(project, "ProjectSettings", "ProjectVersion.txt")):
            self.report({'ERROR'}, "请选择有效的 Unity 项目根目录")
            return {'CANCELLED'}

        model_path, material_path, prefab_path = _export_directories(scene)
        if not all(path.strip() for path in (model_path, material_path, prefab_path)):
            self.report({'ERROR'}, "当前模式的三条路径不能为空")
            return {'CANCELLED'}

        model_dir = _normal(model_path)
        material_dir = _normal(material_path)
        prefab_dir = _normal(prefab_path)
        assets = os.path.join(project, "Assets")
        if not all(_inside(path, assets) for path in (model_dir, material_dir, prefab_dir)):
            self.report({'ERROR'}, "FBX、材质搜索和 Prefab 目录都必须位于 Unity 项目的 Assets 内")
            return {'CANCELLED'}
        if not os.path.isdir(material_dir):
            self.report({'ERROR'}, "材质搜索位置必须是 Unity Assets 内已存在的文件夹")
            return {'CANCELLED'}
        meshes = [obj for obj in context.selected_objects if obj.type == 'MESH']
        if not meshes:
            self.report({'ERROR'}, "请至少选择一个网格物体")
            return {'CANCELLED'}
        name = bpy.path.clean_name(scene.unityimport_model_name.strip())
        if not name:
            self.report({'ERROR'}, "模型文件名不能为空")
            return {'CANCELLED'}

        os.makedirs(model_dir, exist_ok=True)
        output_project = _absolute(scene.unityimport_project_root)
        output_model_dir = os.path.realpath(bpy.path.abspath(model_path))
        output_material_dir = os.path.realpath(bpy.path.abspath(material_path))
        output_prefab_dir = os.path.realpath(bpy.path.abspath(prefab_path))
        relative_model = os.path.relpath(os.path.join(output_model_dir, name + ".fbx"), output_project).replace(os.sep, "/")
        relative_material = os.path.relpath(output_material_dir, output_project).replace(os.sep, "/")
        relative_prefab = os.path.relpath(output_prefab_dir, output_project).replace(os.sep, "/")
        settings = {
            "schemaVersion": 1,
            "modelAssetPath": relative_model,
            "materialSearchDirectory": relative_material,
            "prefabDirectory": relative_prefab,
            "prefabName": name,
        }

        properties = bpy.ops.export_scene.fbx.get_rna_type().properties
        required = {
            "use_selection", "object_types", "global_scale", "apply_scale_options",
            "axis_forward", "axis_up", "apply_unit_scale", "use_space_transform",
            "bake_space_transform", "use_custom_props",
        }
        missing = sorted(required.difference(properties.keys()))
        if missing:
            self.report({'ERROR'}, "当前 Blender FBX 导出器缺少所需设置: " + ", ".join(missing))
            return {'CANCELLED'}

        result = bpy.ops.export_scene.fbx(
            filepath=os.path.join(model_dir, name + ".fbx"),
            use_selection=True,
            object_types={'MESH'},
            global_scale=1.0,
            apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z',
            axis_up='Y',
            apply_unit_scale=True,
            use_space_transform=True,
            bake_space_transform=True,
            use_custom_props=False,
            bake_anim=False,
            path_mode='AUTO',
        )
        if 'FINISHED' not in result:
            self.report({'ERROR'}, "Blender FBX 导出未完成")
            return {'CANCELLED'}

        settings_path = os.path.join(model_dir, name + ".fbx.acepro-import.json")
        fd, temporary_path = tempfile.mkstemp(prefix=name + ".", suffix=".tmp", dir=model_dir)
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(settings, stream, ensure_ascii=False, indent=2)
                stream.write("\n")
            os.replace(temporary_path, settings_path)
        finally:
            if os.path.exists(temporary_path):
                os.remove(temporary_path)
        self.report({'INFO'}, "FBX 和 Unity 导入设置已写入 Assets；Unity Editor 将自动处理材质和 Prefab")
        return {'FINISHED'}


class UNITYIMPORT_PT_panel(bpy.types.Panel):
    bl_label = "导入unity"
    bl_idname = "UNITYIMPORT_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "导入unity"

    def draw(self, context):
        layout = self.layout
        scene = context.scene

        layout.prop(scene, "unityimport_project_root", text="Unity 项目")
        layout.prop(scene, "unityimport_spec", text="导入规格")

        spec = scene.unityimport_spec
        if spec in SPEC_DEFAULTS:
            layout.label(text="预设路径通常无需修改", icon='INFO')
            column = layout.column(align=True)
            for field, label in SPEC_FIELDS:
                column.prop(scene, _preset_property_name(spec, field), text=label)
            layout.operator(UNITYIMPORT_OT_reset_preset.bl_idname, text="恢复默认", icon='FILE_REFRESH')
        else:
            column = layout.column(align=True)
            for field, label in SPEC_FIELDS:
                column.prop(scene, _custom_property_name(field), text=label)

        layout.separator()
        layout.prop(scene, "unityimport_model_name", text="模型名称")
        layout.operator(UNITYIMPORT_OT_import_name.bl_idname, icon='SORTALPHA')
        layout.operator(UNITYIMPORT_OT_export.bl_idname, icon='EXPORT')


CLASSES = (
    UNITYIMPORT_OT_import_name,
    UNITYIMPORT_OT_reset_preset,
    UNITYIMPORT_OT_export,
    UNITYIMPORT_PT_panel,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)

    bpy.types.Scene.unityimport_project_root = StringProperty(name="Unity 项目根目录", subtype='DIR_PATH')
    bpy.types.Scene.unityimport_spec = EnumProperty(name="导入规格", items=SPEC_ITEMS, default=SPEC_CUSTOM)
    for field, label in SPEC_FIELDS:
        setattr(
            bpy.types.Scene,
            _custom_property_name(field),
            StringProperty(name=label, subtype='DIR_PATH', default=""),
        )
    for spec_id, _, defaults in SPEC_PRESETS:
        for (field, label), value in zip(SPEC_FIELDS, defaults):
            setattr(
                bpy.types.Scene,
                _preset_property_name(spec_id, field),
                StringProperty(name=label, subtype='DIR_PATH', default=value),
            )
    bpy.types.Scene.unityimport_model_name = StringProperty(name="模型名称", default="Model")


def unregister():
    names = ["unityimport_project_root", "unityimport_spec", "unityimport_model_name"]
    names.extend(_custom_property_name(field) for field, _ in SPEC_FIELDS)
    for spec_id, _, _ in SPEC_PRESETS:
        names.extend(_preset_property_name(spec_id, field) for field, _ in SPEC_FIELDS)
    for name in names:
        if hasattr(bpy.types.Scene, name):
            delattr(bpy.types.Scene, name)
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
