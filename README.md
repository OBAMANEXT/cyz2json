# Cyz2Json - Convert flow cytometry CYZ files to JSON format.

[![Update API from Latest Tag](https://github.com/OBAMANEXT/cyz2json/actions/workflows/API-update-from-tags.yml/badge.svg)](https://github.com/OBAMANEXT/cyz2json/actions/workflows/API-update-from-tags.yml)
[![Test Cyz2Json after changes](https://github.com/OBAMANEXT/cyz2json/actions/workflows/Check-Cyz2Json.yml/badge.svg)](https://github.com/OBAMANEXT/cyz2json/actions/workflows/Check-Cyz2Json.yml)
[![Publish a release of Cyz2Json](https://github.com/OBAMANEXT/cyz2json/actions/workflows/Release-Cyz2Json.yml/badge.svg)](https://github.com/OBAMANEXT/cyz2json/actions/workflows/Release-Cyz2Json.yml)

## Introduction

This program converts flow cytometry data stored in a CytoBuoy CYZ
format file to JSON format.

Although CYZ files work with the Cytobuoy supplied CytoClus program,
we want to access the data from a wider set of data science tooling
including Python and R. We therefore convert to JSON.

This program uses the [CytoBuoy CyzFile API](https://github.com/Cytobuoy/CyzFile-API) and targets the Microsoft
.NET 8 framework on Windows, macOS or Linux.

## Build instructions

### Build on the PC

#### Installation
The first step is to install the Microsoft dotnet runtime version 8.
Microsoft provides detailed instructions at
https://learn.microsoft.com/en-us/dotnet/core/install/.

For example, installation on Linux Ubuntu 22.04 is as follows:

```
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

You can test that `dotnet` is installed on your system by typing:

```
dotnet --info
```

To generate the version number, you will need to install the `nbgv` tool.
You can install this tool by typing:
`dotnet tool install -g nbgv`

The `Cyz2Json` program can then be compiled by cloning the project
from GitHub and typing `dotnet build -p:OutputPath=./bin/`.
The default version is the one given by the [Cyz2Json.csproj](https://github.com/OBAMANEXT/cyz2json/blob/main/Cyz2Json/Cyz2Json.csproj/) file (unless passed directly during build via -p:Version), the assembly informal version is  "Cyz2Json-the current Version-the latest Git SHA".

### Build with GitHub actions

The [Release-Cyz2Json.yml](https://github.com/OBAMANEXT/cyz2json/blob/main/.github/workflows/Release-Cyz2Json.yml/) workflow can be triggered manually to build and publish a release of the tool with cross platform binaries. A tag is required to be used as the release and tool version (replacing the default one), and a reason for the release can be given as well though it is not mandatory.

You will find the result in : https://github.com/OBAMANEXT/cyz2json/actions and release in : https://github.com/OBAMANEXT/cyz2json/releases

To install from release, download the release for you platfrom, and unpack on your folder choice.

#### On MacOSX
You will need to bypass security 
You have 2 possibilities :
using xattr command.
for exmaple:
```
xattr -d com.apple.quarantine  /opt/cytosense_to_ecotaxa_pipeline/bin/*
```
or if you do not want to use xattr you need to go in Systems Settings > Security & Privacy > General and allow the app to be opened for the 10+ libraries. You need to relauch sevaral time the cyz2json binary to acheive all neccessary permissions.

## Running the program

### Options

The program has a set of arguments and options that can be passed to fit the kind of data you need :
- `input` the cyz file to be converted, mandatory,
- `--output` the JSON output file,
- `--raw` to not apply the moving weighted average filtering algorithm to pulse shapes, export raw, unsmoothed data,
- `-V` display version information,
- `--metadatagreedy` save all possible measurement settings with your file, default = true,
- `--imaging-set-information` to export set information for imaging,
- `--imaging-set-definition` to override the definitions stored in the file, only if --imaging-set-information is true for file with set definitions, 
- `--image-processing` to perform cropping and image processing during the export of the image, default = false, this comes along with several other arguments to adjust the processing options :
- `--image-processing-threshold` the minimum pixel value difference from the background to be considered an object, default = 9,
- `--image-processing-erosion-dilation` the size of the erosion/dilation filter to apply after thresholding, default = 1,
- `--image-processing-bright-field-correction` correct the image for variation in the lighting, default = true,
- `--image-processing-margin-base` add a marging of this many pixels around the detected object, default = 25,
- `--image-processing-margin-percentage` add an extra margin that is a percentage of the size of the detected object, default = 10,
- `--image-processing-extend-object-detection` when seperate objects are detected close (in the margin) of the main object then extend the rectangle to include these objects as well, default = true.

### Linux

The program can be run, on Linux, for example, as follows:

```
dotnet bin/Cyz2Json.dll input.cyz --output output.json
```

Running with executable loaded from github
```
export cyzversion=v0.0.49
rm -rf cyz2json/* && curl -L -o cyz2json/cyz2json.zip https://github.com/ecotaxa/cyz2json/releases/download/$cyzversion/cyz2json-ubuntu-latest.zip && pushd cyz2json && unzip cyz2json.zip && popd && export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:. && cyz2json/Cyz2Json ./Deployment\ 1\ 2024-07-18\ 21h12.cyz --metadatagreedy=false --output ./Deployment\ 1\ 2024-07-18\ 21h12.json
```

### On Windows

```
Cyz2Json.EXE input.cyz --output output.json
```

### On MacOSX

bypass security with xattr command
```
xattr -d com.apple.quarantine ./cyz2json-macos-latest/*
```

2 methods to run
```
dotnet bin/Cyz2Json.dll input.cyz --output output.json
```
or
```
Cyz2Json.exe input.cyz --output output.json
```

### Bulk file conversion

We often find ourselves needing to undertake a bulk conversion of a
large set of files. The following shell script can help:

```
#!/usr/bin/env bash
for file in *.cyz
do
    dotnet ~/git/Cyz2Json/bin/Cyz2Json.dll "$file" --output "$file".json
done
```

## Processing CYZ JSON files 

### Using Python

To load a JSON flow cytometry data file into Python:

```
import json

data = json.load(open("pond.json", encoding="utf-8-sig"))
print(data)
```

Note the need to explicitly specify the encoding to deal with
Microsoft's and Python's differences in interpretation of the
standards regarding byte order marks in UTF-8 files.

### R example

To load a JSON flow cytometry data file into R:

```
library(jsonlite)

json_data <- fromJSON("pond.json")
print(json_data)
```

## Notes

### On images

If a CYZ file contains images, we currently base64 encode them and
include them inline in the JSON. This is costly in terms of disk and
only time will tell if it is a sensible design decision. A future
enhancement would be to include a flag that writes the files out as
JPEG images.

Note that the images are un-cropped by default. The `--image-processing` option can now be used to process and crop images when exporting. 
Initially, the tool didn't support cropping as an expedience to allow
cross platform operation (The CyzFile-API only supports cropping on
Windows platforms).

### On the API

Cyz2json is using a dll from the [CyzFile-API](https://github.com/Cytobuoy/CyzFile-API), at this moment, using version 1.6.1.0.

To update you can use the [API-update-from-tags.yml](https://github.com/OBAMANEXT/cyz2json/blob/main/.github/workflows/API-update-from-tags.yml/) workflow which fetches the latest version of the API (in tags), you can run it manually or let it work on schedule (15th of each month), in that case, it compares the latest tag with the current version in this repo to choose whether to update or not.

You can also update manually without the workflow, to do so :
- download the latest net8.0 release from https://github.com/Cytobuoy/CyzFile-API/releases,
- unzip the downloaded file,
- copy the files from the dll folder to the CyzFile folder,
- rebuild the project.

## Acknowledgements

My thanks to the following organisations for supporting this work:

[The Alan Turing Institute](https://www.turing.ac.uk/).

[The Finnish Marine Research Infrastructure consortium (FINMARI)](https://www.finmari-infrastructure.fi/)

[The Centre for Environment, Fisheries and Aquaculture Science (Cefas)](https://www.cefas.co.uk)

[CytoBuoy b.v.](https://www.cytobuoy.com/)

I am grateful to Rob Lievaart at Cytobuoy for providing the libraries,
code and examples on which this software is based. The CyzFile-API is
licensed under the terms described in CyzFile-API_LICENSE.TXT.

Thanks to Eric Payne at Cefas for Visual Studio wizardry.

## Disclaimers

The [OBAMA-NEXT](https://obama-next.eu/) project has been approved under
HORIZON-CL6-2022-BIODIV-01-01: Observing and mapping biodiversity and
ecosystems, with particular focus on coastal and marine ecosystems
(Grant Agreement 101081642). Funded by the European Union and UK
Research and Innovation. Views and opinions expressed are however
those of the authors only and do not necessarily reflect those of the
European Union or UK Research and Innovation. Neither the European
Union nor the granting authority can be held responsible for them.


