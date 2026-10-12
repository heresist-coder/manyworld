import pathlib
root=pathlib.Path(r"C:\VC3\manyworld\docs\art\2026-10-12")
src=(root/"anomalies/render-synty-previews.py").read_text(encoding="utf-8")
src=src.replace("2026-10-12\\anomalies","2026-10-12\\selected-scenes-v3\\native-references")
start=src.index("items=[")
end=src.index("records=[]")
items=[
("forest-mannequin",r"D:\Codex\cube125-unity_asset_backups\20260830_172201_city_cute_transition\Assets\Synty\PolygonApocalypse\Models\SM_Prop_Mannequin_Male_01.fbx"),
("metal-antenna",r"D:\Codex\cube125-unity\Assets\Synty\PolygonSciFiWorlds\Models\SM_Prop_Antenna_01.fbx"),
("metal-powerline",r"D:\Codex\cube125-unity\Assets\Synty\PolygonSciFiWorlds\Models\SM_Prop_Powerline_Antenna_01.fbx"),
]
src=src[:start]+"items="+repr(items)+"\n"+src[end:]
src=src.replace('for slug,pack,name in items:\n    src=base/pack/"Models"/(name+".fbx")','for slug,filepath in items:\n    src=pathlib.Path(filepath)\n    pack=src.parent.parent.name\n    name=src.stem')
out=root/"selected-scenes-v3/render-selected-references.py"
out.write_text(src,encoding="utf-8")
print(out)

