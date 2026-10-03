"""Original low-poly university salvage kit. Run Blender -b -P this_file.

Editable sources live in Source~ (Unity ignores them); explicit FBX exports make
the remake portable without changing the established landmark .blend pipeline.
Unity metres, forward -Z, up Y; compound colliders are owned by gameplay.
"""
import os
import math
import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Remake")
os.makedirs(os.path.join(OUT, "Source~"), exist_ok=True)

COLORS = {
    "Chalk": (0.73, 0.72, 0.65, 1), "Ink": (0.035, 0.048, 0.052, 1),
    "Steel": (0.22, 0.27, 0.28, 1), "Oxblood": (0.38, 0.045, 0.035, 1),
    "Amber": (0.92, 0.51, 0.08, 1), "Screen": (0.08, 0.7, 0.56, 1),
    "Glass": (0.08, 0.19, 0.22, 1), "Skin": (0.57, 0.34, 0.24, 1),
    "Eye": (1, 0.08, 0.02, 1), "Wood": (0.39, 0.23, 0.11, 1),
}

def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for name, color in COLORS.items():
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color = color

def box(name, pos, size, mat="Steel", bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(pos[0], -pos[2], pos[1]))
    ob = bpy.context.object
    ob.name = name
    ob.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(bpy.data.materials[mat])
    if bevel:
        mod = ob.modifiers.new("Machined_edges", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob

def wheel(pos, radius=0.43):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=0.24,
        location=(pos[0], -pos[2], pos[1]), rotation=(0, math.pi / 2, 0))
    ob = bpy.context.object
    ob.name = "Tire"
    ob.data.materials.append(bpy.data.materials["Ink"])

def cylinder(name, pos, radius, depth, mat="Steel", horizontal=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=depth,
        location=(pos[0], -pos[2], pos[1]), rotation=(math.pi/2, 0, 0) if horizontal else (0,0,0))
    ob=bpy.context.object; ob.name=name; ob.data.materials.append(bpy.data.materials[mat]); return ob

def export(name):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Source~", name + ".blend"))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"),
        object_types={"MESH"}, add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP")
    print("[remake_asset]", name)

reset()
box("Keyboard", (0, -0.13, 0), (0.65, 0.07, 0.45), "Ink", 0.02)
box("Display", (0, 0.08, 0.18), (0.65, 0.4, 0.035), "Steel", 0.015)
box("LCD", (0, 0.08, 0.156), (0.57, 0.32, 0.015), "Screen")
for row in range(4):
    for x in range(12):
        box("Key", (-0.26+x*0.047, -0.088, 0.10-row*.044), (0.034, 0.012, 0.03), "Steel", .003)
box("Trackpad", (0,-.089,-.145), (.18,.01,.075), "Steel", .006)
box("Hinge", (0,-.10,.184), (.52,.05,.04), "Ink", .008)
for row in range(4):
    box("LCD_line", (-.07,.18-row*.054,.145), (.34-row*.04,.012,.008), "Chalk")
for x in (-.29,.29):
    box("USB", (x,-.13,-.05), (.012,.025,.045), "Glass")
export("Laptop")

reset()
box("Body", (0, 0, 0), (0.6, 0.26, 0.46), "Chalk", 0.045)
box("Lens", (-0.16, 0, -0.25), (0.16, 0.16, 0.045), "Glass", 0.035)
for x in range(8):
    box("Vent", (-0.2+x*0.055, 0.132, 0.05), (0.022, 0.01, 0.16), "Ink")
export("Projector")

reset()
box("Base", (0, -0.35, 0), (0.44, 0.1, 0.38), "Ink", 0.025)
box("Stand", (0, -0.05, 0.1), (0.08, 0.58, 0.1), "Chalk")
box("Stage", (0, -0.08, -0.02), (0.32, 0.045, 0.3), "Steel")
cylinder("Optics", (0,.22,-.04), .064,.32,"Chalk")
cylinder("Eyepiece", (0,.4,-.04), .046,.09,"Ink")
for x in (-.055,0,.055): cylinder("Objective", (x,.043,-.06), .018,.10,"Steel")
cylinder("Illuminator", (0,-.28,-.05), .075,.045,"Glass")
box("Focus", (.12,.1,.1), (.16,.13,.12), "Ink", .03)
export("Microscope")

reset()
box("Tower", (0, 0, 0), (0.48, 0.9, 0.58), "Ink", 0.035)
box("Panel", (0, 0.03, -0.297), (0.39, 0.69, 0.015), "Steel")
for i in range(5):
    box("Drive", (0, 0.26-i*0.115, -0.31), (0.31, 0.045, 0.012), "Ink")
    box("LED", (0.16, 0.26-i*0.115, -0.32), (0.025, 0.025, 0.015), "Screen")
for row in range(4):
    for col in range(4): box("Intake", (-.13+col*.085,-.33+row*.043,-.313), (.045,.012,.012), "Ink")
box("AssetTag", (0,.38,-.32), (.13,.033,.012), "Amber")
for x in (-.17,.17):
    for y in (-.36,.37): cylinder("Fastener", (x,y,-.323), .012,.015,"Steel", True)
export("Workstation")

reset()
box("Battery", (0, 0, 0), (0.52, 0.65, 0.6), "Steel", 0.025)
box("Warning", (0, 0.1, -0.305), (0.32, 0.16, 0.015), "Amber")
box("Status", (0, -0.15, -0.315), (0.15, 0.08, 0.02), "Screen")
for x in (-.19,.19):
    box("CarryHandle", (x,.34,0), (.055,.1,.30), "Ink", .012)
for row in range(5): box("BatteryVent", (0,-.24+row*.038,.308), (.35,.016,.012), "Ink")
export("UPS")

reset()
box("Body", (0, 0, 0), (0.75, 0.55, 0.65), "Chalk", 0.04)
box("Scanner", (0, 0.3, 0), (0.76, 0.07, 0.66), "Ink", 0.015)
box("PaperTray", (0, -0.11, -0.37), (0.48, 0.13, 0.18), "Steel")
box("Control", (0.19, 0.24, -0.3), (0.24, 0.075, 0.15), "Screen")
box("Paper", (0,-.09,-.39), (.42,.008,.20), "Chalk")
for row in range(3): box("Drawer", (0,-.17+row*.14,-.329), (.65,.009,.013), "Steel")
box("ScannerGlass", (0,.341,0), (.56,.012,.45), "Glass")
export("Printer")

reset()
box("Deck", (0, 0, 0), (1.3, 0.12, 1.65), "Oxblood")
for x in (-0.63, 0.63):
    box("Side", (x, 0.2, 0), (0.08, 0.4, 1.65), "Steel")
    for z in (-0.59, 0.59): wheel((x, -0.2, z), 0.16)
    box("HandlePost", (x, 0.52, 0.77), (0.07, 1, 0.07), "Steel")
box("Handle", (0, 1.01, 0.77), (1.3, 0.075, 0.075), "Ink")
export("Cart")

reset()
box("Chassis", (0, 0.45, 0.35), (2.65, 0.25, 7), "Ink")
box("CargoFloor", (0, 0.75, -0.8), (2.7, 0.18, 4.7), "Wood")
for x in (-1.42, 1.42):
    box("BoxWall", (x, 1.91, -0.8), (0.16, 2.42, 4.7), "Chalk")
    box("Stripe", (x*1.065, 1.33, -0.8), (0.025, 0.23, 4.72), "Oxblood")
    for z in (-1.95, 2.35): wheel((x, 0.43, z))
box("CargoRoof", (0, 3.14, -0.8), (3, 0.15, 4.7), "Chalk")
box("Bulkhead", (0, 1.9, 1.53), (2.7, 2.4, 0.15), "Chalk")
for x in (-1.35,1.35):
    box("Cargo rail", (x,1.35,-.8), (.04,.075,4.45), "Steel")
    box("Cargo rail", (x,2.15,-.8), (.04,.075,4.45), "Steel")
    box("Rear frame", (x,1.91,-3.10), (.07,2.35,.08), "Steel")
for z in range(12):
    box("FloorPlank", (0,.851,-2.95+z*.39), (2.62,.016,.014), "Ink")
box("RearHeader", (0,3.02,-3.1), (2.8,.11,.10), "Steel")
box("Cab", (0, 1.2, 2.72), (2.64, 1.58, 2.05), "Oxblood", 0.08)
box("Windshield", (0, 1.86, 3.69), (2.16, 0.72, 0.05), "Glass")
box("CabRoof", (0, 2.33, 2.7), (2.75, 0.15, 2.2), "Chalk")
for x in (-1.34,1.34):
    box("Side window", (x,1.83,2.60), (.022,.58,1.15), "Glass")
    box("Door handle", (x*1.013,1.25,2.24), (.04,.07,.20), "Steel")
    box("Mirror", (x*1.20,1.88,3.08), (.18,.3,.10), "Steel", .03)
box("Grille", (0,.97,3.77), (1.3,.25,.03), "Ink")
for x in range(7):box("GrilleSlat",(-.55+x*.18,.97,3.80),(.035,.21,.015),"Steel")
box("Plate", (0,.64,3.93), (.45,.14,.02), "Chalk")
box("Bumper", (0, 0.59, 3.82), (2.9, 0.16, 0.19), "Steel")
for x in (-0.97, 0.97):
    box("Headlight", (x, 0.99, 3.77), (0.41, 0.27, 0.06), "Amber")
    box("RearLight", (x, 0.92, -3.19), (0.28, 0.17, 0.06), "Eye")
export("Truck")

reset()
box("Uniform", (0, 1.28, 0), (0.5, 0.88, 0.28), "Oxblood", 0.08)
box("Uniform pocket",(-.13,1.43,-.159),(.13,.18,.018),"Steel",.01)
box("ID badge",(.12,1.46,-.16),(.09,.15,.015),"Chalk")
bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=(0,0,2.1))
bpy.context.object.name="Head";bpy.context.object.scale=(.23,.20,.3)
bpy.context.object.data.materials.append(bpy.data.materials["Chalk"])
box("Nose",(0,2.08,-.225),(.07,.10,.12),"Chalk",.025)
box("Mouth", (0, 1.92, -0.19), (0.22, 0.13, 0.025), "Ink")
for x in (-0.115, 0.115):
    box("Eye", (x, 2.17, -0.19), (0.075, 0.065, 0.03), "Eye")
    box("Leg", (x*1.5, 0.45, 0), (0.13, 0.9, 0.16), "Ink")
    box("Shoe", (x*1.5, 0.08, -0.08), (0.17, 0.13, 0.3), "Ink")
for x in (-0.33, 0.33):
    box("Sleeve", (x, 1.3, 0), (0.13, 0.85, 0.17), "Oxblood")
    box("Hand", (x, 0.85, 0), (0.12, 0.18, 0.14), "Skin")
box("MopHandle", (0.5, 1.18, -0.12), (0.045, 2.15, 0.045), "Wood")
box("Mop", (0.5, 0.13, -0.12), (0.5, 0.17, 0.28), "Chalk")
export("Caretaker")

reset()
box("Palm",(0,0,0),(.82,.67,.58),"Skin",.075)
box("Wrist",(0,0,-.4),(.55,.48,.32),"Skin",.045)
for finger in range(4):
    x=(finger-1.5)*.19
    box("Knuckle",(x,0,.32),(.17,.42,.29),"Skin",.045)
    box("Finger",(x,-.10,.50),(.16,.30,.27-(.04 if finger in (0,3) else 0)),"Skin",.04)
thumb=box("Thumb",(-.48,-.02,.1),(.23,.38,.45),"Skin",.055)
thumb.rotation_euler.z=math.radians(-30)
export("StudentHand")
os.makedirs(os.path.join(OUT,"Resources"),exist_ok=True)
os.replace(os.path.join(OUT,"StudentHand.fbx"),os.path.join(OUT,"Resources","StudentHand.fbx"))
