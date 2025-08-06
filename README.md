# Crystalite - A demo of V-Slicing in action

## Experimental non-planar slicer!
Crystalite is an experimental slicer that implements variable-angle slicing, ideal for machines with enough hotend clearance and without overly aggressive collision protection.

It currently works best with printers like the original Ender 3 (at least 10° slope clearance recommended). Printers with auto nozzle cleaning or tight overhang limits (e.g., Bambu A1) may struggle or even risk collisions.

This is an early-stage demo. It can crash on large models or misbehave with weird geometry. Always double-check G-code with your preferred viewer before printing-Crystalite doesn’t include one yet.

## V-Slicing
V-Slicing is a slicing technique I developed that generates layers which adapt to the model's shape. These layers can bend and vary in thickness -even within a single layer- to closely follow the geometry, effectively eliminating stair-stepping and enabling clean horizontal overhangs.

The model is first converted into a voxel-based occupancy map. From that, adaptive layers are generated wherever plastic can be deposited with contact to existing geometry-not necessarily “from below,” just with enough adjacent surface. Toolpaths follow these layers exactly.

This approach is a complete departure from traditional slicing (and even most non-planar variants), which still rely on planar slicing of a distorted model. V-Slicing skips that entirely by slicing in voxel space directly.

It’s not perfect yet -toolpaths still come straight from the voxel data, which can limit accuracy. But if we mesh those voxel layers and slice the original model using those surfaces, we might unlock fully accurate non-planar slicing.

## The UI
Crystalite’s UI was built for tinkerers who want bleeding-edge slicing without wrestling a terminal. 

The name comes from the crystal-like patterns V-Slicing generates when slicing solids at steep angles-a visual metaphor baked into the app.

I started with WPF’s 3D viewport, but it struggled even on modest triangle counts and with dynamic model transforms (you have to rebuild all triangles on every move causing painful lag spikes and horrible UX). So I switched to Avalonia for its cross-platform support, OpenGL integration, and near-identical syntax.

Readability and visual clarity were the core priorities with teh new UI, so I used Gooch shading-a technique that replaces harsh light and shadow contrasts with smooth gradients between warm and cool colors. Combined with bold outlines highlighting key model edges, this makes shapes pop and features easier to spot without taxing performance, even on low-end hardware.

To help with spatial awareness, the scene has cast shadows. Without them, it’s tricky to tell if a model is on the build plate or far above it from most perspectives.

<img width="1917" height="1000" alt="CrystaliteUIDemo" src="https://github.com/user-attachments/assets/0a2167e9-1075-49ac-8ce9-be6aa9cc5236" />

Transform handles let you grab axes and move or rotate models, with mouse movement projected into world space making their use intuitive.

<img width="480" height="489" alt="CrystaliteUIHandlesDemo" src="https://github.com/user-attachments/assets/32cf6ed2-7cde-41f2-aefc-607210b57d6f" />

You can drag and drop models into the scene, though deleting them isn’t supported yet(sorry, but you'll have to restart the app).

## The generated G-code

Since Crystalite doesn’t have a built-in GCode viewer yet, you’ll need to open the output in your slicer of choice to inspect before printing. Be warned: the slicer can behave weirdly sometimes, like slicing models mid-air (and not the cool kind).

 <img width="996" height="832" alt="GCodeFullFile" src="https://github.com/user-attachments/assets/38629a9f-a634-4f66-a51b-b11ef23462f2" />
 
As mentioned, the layers adapt in both shape and thickness to the model, as you can see here. This allows for high-quality top surfaces and surprisingly good horizontal overhangs.

<img width="670" height="477" alt="GCodeHalfFile" src="https://github.com/user-attachments/assets/9c139739-7340-4107-a41a-f54dc54e20e8" />

The strength of adaptive layer thickness is clearly seen on the top layers of this benchy.

<img width="1200" height="800" alt="TopLayerDemo" src="https://github.com/user-attachments/assets/c19480f0-44f4-4c01-994f-9a591eea7f4d" />

While not perfect, the horizontal overhang printed with minimal warping (note: text was removed from the bottom because an earlier version struggled with it)

<img width="1200" height="800" alt="OverhangDemo" src="https://github.com/user-attachments/assets/687e7de9-2753-4b77-ab08-e63337463547" />

For further details on V-Slicing, pelase check out my blog at <a href="https://www.geekworksresearch.com">GeekworksResearch.com</a>. I will be posting further devlogs about the remaining steps, but I didn't want to share posts explaining how a certain stage works, until I made sure it was working, and so when I got stuck at layer generation not being reliable enough I stopped making the blog posts. I will also be splitting them into two parts, a part that is more technical and a part that explains it in a more approcahable way, since I didn't like the awkward inbetween tone of the first few devlogs.
