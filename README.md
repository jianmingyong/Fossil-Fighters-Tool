<div id="top"></div>

# Fossil Fighters Assets Extractor

<summary>Table of Contents</summary>
<ol>
    <li><a href="#about-the-project">About The Project</a></li>
    <li><a href="#game-file-headers">Game File Headers</a></li>
    <li><a href="#built-instructions">Built Instructions</a></li>
</ol>

## About The Project

This project aims to extract Fossil Fighters / Fossil Fighters Champion assets which are heavily compressed in the game.

Fossil Fighters seems to be using a [custom archive format](https://github.com/jianmingyong/Fossil-Fighters-Tool/wiki/Game-Archive-Header), which can contains files that are compressed in chunks of 8kb. We have no idea why it is designed that way, but it is assumed to be optimization for decompressing in-game.

<p align="right">(<a href="#top">back to top</a>)</p>

## Game File Headers

[Visit the wiki for all the known file headers used in-game](https://github.com/jianmingyong/Fossil-Fighters-Tool/wiki/Game-File-Headers).

<p align="right">(<a href="#top">back to top</a>)</p>

## Built Instructions

This project is build with [dotnet 6.0](https://dotnet.microsoft.com/download/dotnet/6.0).

1. Clone / Download the git repository source code. <br />
Git CLI: `git clone https://github.com/jianmingyong/Fossil-Fighters-Tool.git`

2. Build the program with dotnet CLI. <br />
For windows: `dotnet publish -r win-x64 -o bin/win-x64 --self-contained true` <br />
For linux: `dotnet publish -r linux-x64 -o bin/linux-x64 --self-contained true` <br />
For macOS: `dotnet publish -r osx-x64 -o bin/osx-x64 --self-contained true` <br />

<p align="right">(<a href="#top">back to top</a>)</p>
