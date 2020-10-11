# .las tools for Unity

LASer -file tools for Unity

Contains an editor tool for converting .las files into .pcache files
that visual effects graph already supports, and a vfxbinder that
binds a .las file/files directly into vfx graph.

![gif](https://i.imgur.com/ThOl003.gif)
![gif](https://i.imgur.com/dAAfjhO.jpg)

### Requirements

Unity 2019.4.1f1+ (might work with older ones, just not tested with)
HDRP, Shader graph, Visual effects graph

## Usage of LAS-binder for VFX graph
1. To directly supply a .las files' points to a VFX graph,
one can add a VFX property binder to a VFX graph (this is a standard monobehaviour component provided
by Unity in the VFX graph package).

2. Add "LAS-Binder" to the list of "Property Bindigs" of the VFX Property Binder.

3. Click the LAS-Binder and tweak the settings
![gif](https://i.imgur.com/8NXixx5.gif)

### Skip
Amount of points to skip in between readings (0 meaning every point will be read, 1 meaning every second, etc..)

### LAS Files in StreamingAssets
.las filenames in StreamingAssets to be read.

### Use First Point As Anchor
When checked, the system will zero the first point and offset every
other point from that one. Main use is to keep the points in a distance
that Unity can handle. (Sometimes the .las position are well beyond Unity's floating point precision)
Note: there is no guarantee which of the points will be read first when multiple files are read simutaneously.

### Update
Press this to actually update the internal textures which the shader graph will use to draw the points

## LAS 2 PCache
Editor tool that can be used to convert .las files into .pcache files.
One can find the tool in question by clicking "LASTools" in the upper navigation bar of Unity.

![gif](https://i.imgur.com/w7Jnsxy.png)

### Filenames
.las -files to be converted to .pcache/.pcaches
"Add file" adds selected file into the Filenames-list.

### Outputpath
Directory to write the .pcache/.pcaches to.

### Mergefiles
Whether to merge the .las files into one single .pcache file
or write them into separate ones.

Note: if this option is not used, the written files will be names
after the original source file. When merging the output file will be
named as "MERGED{unixts in seconds}.pcache".

### Anchor to First Point
Same as in LAS-Binder section above.

### Convert
Press to start the conversion.

## Visual effects graph and Shader graphs
Everything can be found in "Assets/LASTools/Graphs"

Note: When making a new version of the graph for 
the LAS-Binder, one needs to keep in mind that the binder
expects two accessable properties: "_PositionMapArray" (of type
Texture 2D array) and "_Depth" (of type int)

## Gotchas:
The system currently only takes in account the positions of the .las files
so no color's are read/used yet.

The work is based on .las format 1.2 so the structure of file is expected to
be in this format. Other formats might work if the byte offsets happen to be the
same.

## Acknowledgments

* https://www.asprs.org/a/society/committees/standards/asprs_las_format_v12.pdf
* https://opentopography.org/start

