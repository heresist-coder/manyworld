import bpy, pathlib, json, math
from mathutils import Vector
root=pathlib.Path(r"C:\VC3\manyworld\docs\art\2026-10-12\selected-scenes-v3\native-references")
root.mkdir(parents=True, exist_ok=True)
base=pathlib.Path(r"D:\Codex\cube125-unity\Assets\Synty")
items=[('forest-mannequin', 'D:\\Codex\\cube125-unity_asset_backups\\20260830_172201_city_cute_transition\\Assets\\Synty\\PolygonApocalypse\\Models\\SM_Prop_Mannequin_Male_01.fbx'), ('metal-antenna', 'D:\\Codex\\cube125-unity\\Assets\\Synty\\PolygonSciFiWorlds\\Models\\SM_Prop_Antenna_01.fbx'), ('metal-powerline', 'D:\\Codex\\cube125-unity\\Assets\\Synty\\PolygonSciFiWorlds\\Models\\SM_Prop_Powerline_Antenna_01.fbx')]
records=[]
for slug,filepath in items:
    src=pathlib.Path(filepath)
    pack=src.parent.parent.name
    name=src.stem
    rec={"slug":slug,"pack":pack,"model":name,"source":str(src),"exists":src.exists()}
    if not src.exists():
        records.append(rec); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(src), use_anim=False)
    mesh=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    pts=[o.matrix_world@Vector(p) for o in mesh for p in o.bound_box]
    if not pts:
        rec["error"]="No mesh";records.append(rec);continue
    lo=Vector(tuple(min(v[i] for v in pts) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in pts) for i in range(3)))
    cen=(lo+hi)/2
    size=hi-lo
    span=max(size)
    scene=bpy.context.scene
    scene.render.engine="BLENDER_WORKBENCH"
    scene.display.shading.light="STUDIO"
    scene.display.shading.studio_light="paint.sl"
    scene.display.shading.color_type="SINGLE"
    scene.display.shading.single_color=(0.53,0.57,0.62)
    scene.display.shading.show_shadows=True
    scene.display.shading.show_cavity=True
    scene.display.shading.cavity_type="WORLD"
    scene.display.shading.background_type="WORLD"
    scene.world=bpy.data.worlds.new("PreviewWorld")
    scene.world.color=(0.065,0.075,0.09)
    camdata=bpy.data.cameras.new("PreviewCamera")
    camera=bpy.data.objects.new("PreviewCamera",camdata)
    scene.collection.objects.link(camera)
    camera.location=cen+Vector((1.3,-1.6,1.05)).normalized()*max(1,span)*3
    camera.rotation_euler=(cen-camera.location).to_track_quat("-Z","Y").to_euler()
    camdata.type="ORTHO"
    camdata.ortho_scale=max(1,span)*1.55
    camdata.clip_end=max(1000,span*10)
    scene.camera=camera
    scene.render.resolution_x=400;scene.render.resolution_y=280;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.filepath=str(root/(slug+".png"))
    scene.view_settings.view_transform="Standard"
    bpy.ops.render.render(write_still=True)
    rec["mesh_count"]=len(mesh)
    rec["native_bounds_meters"]=list(size)
    rec["preview"]=str(root/(slug+".png"))
    rec["preview_material"]="neutral grey geometry proof; not source texture"
    records.append(rec)
    print("PREVIEW_OK",slug,flush=True)
(root/"native-model-previews.json").write_text(json.dumps(records,indent=2),encoding="utf-8")

