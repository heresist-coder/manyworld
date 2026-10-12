import pathlib,json,hashlib
from PIL import Image
root=pathlib.Path(r"C:\VC3\manyworld\docs\art\2026-10-12\selected-scenes-v3")
gen=pathlib.Path(r"C:\Users\heres\.codex\generated_images\01a12f87-1951-74b0-a49c-c4b54c3169ad")
mapping=[
("01-floating-house-road-shadow.png","exec-a2ad19ca-18f5-4d4c-9c1f-9754c1f404a2.png",["floating_house","road_crossing_shadow"]),
("02-inward-leaning-forest.png","exec-7e1f8fb6-e17f-4fa3-9315-f953631ca78e.png",["inward_leaning_forest"]),
("03-empty-forest-room-dolls.png","exec-b0c547b8-6650-432f-8892-216fae3372e0.png",["empty_forest_room","forest_dolls"]),
("04-iron-tree.png","exec-a3b4697d-acff-4fab-80d5-e63f79a94e9f.png",["iron_tree"]),
("05-unsinkable-rocks-submerged-house.png","exec-c7ab28cc-800f-4213-87b8-189e57cf7da8.png",["unsinkable_rocks","lit_submerged_house"]),
]
result={"date":"2026-10-12","status":"five generated target concepts; not Unity integration","satellite_excluded":"visually reviewed in all five generated outputs; no object detector used","files":[]}
for name,source,anomalies in mapping:
    p=root/name
    assert p.exists(),str(p)
    digest=hashlib.sha256(p.read_bytes()).hexdigest()
    match=digest==hashlib.sha256((gen/source).read_bytes()).hexdigest()
    assert match,name
    size=list(Image.open(p).size)
    result["files"].append({"path":name,"dimensions":size,"bytes":p.stat().st_size,"sha256":digest,"source":str(gen/source),"source_hash_matches":match,"selected_anomalies":anomalies})
assert len(result["files"])==5
assert len(set(a for r in result["files"] for a in r["selected_anomalies"]))==8
result["unique_requested_anomaly_count"]=8
(root/"verification.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print(json.dumps({"count":len(result["files"]),"selected_anomalies":8,"dimensions":[r["dimensions"] for r in result["files"]],"all_source_hashes_match":True}))

