﻿<div id="top"></div>

# Fossil Fighters Assets Extractor

<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#mar-archive">MAR Archive</a></li>
        <li><a href="#mcm-file">MCM File</a></li>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
  </ol>
</details>

## About The Project

This project aims to extract Fossil Fighters / Fossil Fighters Champion assets which are heavily compressed in the game.

Fossil Fighters seems to be using a custom archive format which can contains files that are compressed in chunks of 8kb. We have no idea why it is designed that way but it is assumed to be optimization for decompressing in-game.

<p align="right">(<a href="#top">back to top</a>)</p>

### MAR Archive

This archive format is rather simple and do not contain headers about original file names and such. The game uses indexes to identify files in each MAR archive.

MAR Header

```text
File Header
    0x00h 4     ID "MAR" (0x0052414D)
    0x04h 4     Number of files
    0x08h N*8   File Lists (see below)
    
File Lists
    0x00h 4     MCM File offset (Offset from MAR+0)
    0x04h 4     Data File size (Decompressed)
```

### MCM File

This file contains information about the original data file size and the type of compression used. After doing this step, you would get the actual raw binary data to be read in-game.

Files may be split into 8kb chunks which is then compressed via Huffman / LZ10 / RLE.

MCM Header

```text
File Header
    0x00h 4     ID "MCM" (0x004D434D)
    0x04h 4     Decompressed file size
    0x08h 4     Max size per chunk (Usually 0x2000)
    0x0Ch 4     Number of chunks
    0x10h 1     Compression Type 1 (0x00: None, 0x01: RLE, 0x02: LZ10, 0x03: Huffman)
    0x11h 1     Compression Type 2 (0x00: None, 0x01: RLE, 0x02: LZ10, 0x03: Huffman)
    0x12h 2     Padding
    0x14h N*4   File chunk (See below)
    ..    4     EOF offset (Offset from MCM+0)
    
File Chunk
    0x00h 4     Chunk offset (Offset from MCM+0)
```

### Built With

This project is build with Dotnet Core which you can download below.

* [dotnet 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)

<p align="right">(<a href="#top">back to top</a>)</p>
