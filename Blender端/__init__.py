bl_info = {
    "name": "导入unity",
    "author": "OpenAI",
    "version": (1, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > 导入unity",
    "description": "Export selected mesh objects to a Unity project and write its import settings",
    "category": "Import-Export",
}

import json
import os
import tempfile
import bpy
from bpy.props import StringProperty


DEFAULT_MODEL_RELATIVE_PATH = os.path.join("Assets", "Game", "Art", "Environment", "TokyoRooftops")
DEFAULT_MATERIAL_RELATIVE_PATH = os.path.join("Assets", "Game", "WorldMap", "Props", "Materials")
DEFAULT_PREFAB_RELATIVE_PATH = os.path.join("Assets", "Game", "WorldMap", "Props", "TokyoRooftops")


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


class UNITYIMPORT_OT_set_defaults(bpy.types.Operator):
    bl_idname = "unityimport.set_defaults"
    bl_label = "根据 Unity 根目录生成默认路径"
    bl_description = "按 Unity 项目根目录生成 FBX、材质和 Prefab 默认位置，并回填选中模型名称"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        project = _absolute(scene.unityimport_project_root)
        if not os.path.isdir(project) or not os.path.isfile(os.path.join(project, "ProjectSettings", "ProjectVersion.txt")):
            self.report({'ERROR'}, "请选择有效的 Unity 项目根目录")
            return {'CANCELLED'}

        scene.unityimport_model_directory = os.path.join(project, DEFAULT_MODEL_RELATIVE_PATH)
        scene.unityimport_material_directory = os.path.join(project, DEFAULT_MATERIAL_RELATIVE_PATH)
        scene.unityimport_prefab_directory = os.path.join(project, DEFAULT_PREFAB_RELATIVE_PATH)
        self.report({'INFO'}, "已根据 Unity 根目录生成默认路径")
        return {'FINISHED'}


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


class UNITYIMPORT_OT_export(bpy.types.Operator):
    bl_idname = "unityimport.export_selected"
    bl_label = "导出并导入 Unity"
    bl_description = "导出选中的网格，并通知打开的 Unity 项目自动绑定材质及生成 Prefab"
    bl_options = {'REGISTER'}

    def execute(self, context):
        scene = context.scene
        project = _normal(scene.unityimport_project_root)
        model_dir = _normal(scene.unityimport_model_directory)
        material_dir = _normal(scene.unityimport_material_directory)
        prefab_dir = _normal(scene.unityimport_prefab_directory)
        if not os.path.isdir(project) or not os.path.isfile(os.path.join(project, "ProjectSettings", "ProjectVersion.txt")):
            self.report({'ERROR'}, "请选择有效的 Unity 项目根目录")
            return {'CANCELLED'}

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
        output_model_dir = _absolute(scene.unityimport_model_directory)
        output_material_dir = _absolute(scene.unityimport_material_directory)
        output_prefab_dir = _absolute(scene.unityimport_prefab_directory)
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
        layout.operator(UNITYIMPORT_OT_set_defaults.bl_idname, icon='FILE_REFRESH')
        layout.prop(scene, "unityimport_model_directory", text="FBX 导入位置")
        layout.prop(scene, "unityimport_material_directory", text="材质搜索位置")
        layout.prop(scene, "unityimport_prefab_directory", text="Prefab 保存位置")
        layout.prop(scene, "unityimport_model_name", text="模型名称")
        layout.operator(UNITYIMPORT_OT_import_name.bl_idname, icon='SORTALPHA')
        layout.operator(UNITYIMPORT_OT_export.bl_idname, icon='EXPORT')


CLASSES = (UNITYIMPORT_OT_set_defaults, UNITYIMPORT_OT_import_name, UNITYIMPORT_OT_export, UNITYIMPORT_PT_panel)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.unityimport_project_root = StringProperty(name="Unity 项目根目录", subtype='DIR_PATH')
    bpy.types.Scene.unityimport_model_directory = StringProperty(
        name="FBX 导入位置", subtype='DIR_PATH', default=DEFAULT_MODEL_RELATIVE_PATH)
    bpy.types.Scene.unityimport_material_directory = StringProperty(
        name="材质搜索位置", subtype='DIR_PATH', default=DEFAULT_MATERIAL_RELATIVE_PATH)
    bpy.types.Scene.unityimport_prefab_directory = StringProperty(
        name="Prefab 保存位置", subtype='DIR_PATH', default=DEFAULT_PREFAB_RELATIVE_PATH)
    bpy.types.Scene.unityimport_model_name = StringProperty(name="模型名称", default="Model")


def unregister():
    for name in (
        "unityimport_project_root", "unityimport_model_directory",
        "unityimport_material_directory", "unityimport_prefab_directory", "unityimport_model_name",
    ):
        if hasattr(bpy.types.Scene, name):
            delattr(bpy.types.Scene, name)
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
