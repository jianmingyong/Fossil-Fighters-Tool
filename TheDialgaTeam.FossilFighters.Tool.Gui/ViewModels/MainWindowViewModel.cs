// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2023 Yong Jian Ming
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.FileSystemGlobbing;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TheDialgaTeam.FossilFighters.Assets;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    [ObservableAsProperty]
    public bool IsRomLoaded { get; }

    public HierarchicalTreeDataGridSource<NitroRomNode> NitroRomNodeSource { get; }

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveFileCommand { get; }

    public ReactiveCommand<Unit, Unit> ImportFileCommand { get; }

    public ReactiveCommand<Unit, Unit> ExportFileCommand { get; }

    public ReactiveCommand<Unit, Unit> CompressFileCommand { get; }

    public ReactiveCommand<Unit, Unit> DecompressFileCommand { get; }

    private ObservableCollection<NitroRomNode> NitroRomNodes { get; } = new();

    [Reactive]
    private NdsFilesystem? LoadedRom { get; set; }

    [Reactive]
    private NitroRomNode? SelectedNitroRomNode { get; set; }

    public MainWindowViewModel(Window window) : base(window)
    {
        this.WhenAnyValue(model => model.LoadedRom).Select(filesystem => filesystem is not null).ToPropertyEx(this, model => model.IsRomLoaded);

        NitroRomNodeSource = new HierarchicalTreeDataGridSource<NitroRomNode>(NitroRomNodes)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<NitroRomNode>(new TextColumn<NitroRomNode, string>("Name", node => node.Name), node => node.ChildNodes, node => node.ChildNodes.Count > 0),
                new TextColumn<NitroRomNode, string>("Type", node => node.FileTypeDisplay),
                new TextColumn<NitroRomNode, string>("Size", node => !node.IsFile ? string.Empty : $"{node.Size:N0} B")
            }
        };

        NitroRomNodeSource.RowSelection!.SelectionChanged += NitroContentSourceOnSelectionChanged;

        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        SaveFileCommand = ReactiveCommand.CreateFromTask(SaveFile, this.WhenAnyValue(model => model.LoadedRom).Select(filesystem => filesystem is not null).AsObservable());
        ImportFileCommand = ReactiveCommand.CreateFromTask(ImportFile, this.WhenAnyValue(model => model.SelectedNitroRomNode).Select(node => node?.IsFile ?? false).AsObservable());
        ExportFileCommand = ReactiveCommand.CreateFromTask(ExportFile);
        CompressFileCommand = ReactiveCommand.CreateFromTask(CompressFile, this.WhenAnyValue(model => model.SelectedNitroRomNode).Select(node => node?.FileType == NitroRomType.MarArchive).AsObservable());
        DecompressFileCommand = ReactiveCommand.CreateFromTask(DecompressFile, this.WhenAnyValue(model => model.SelectedNitroRomNode).Select(node => node?.FileType is NitroRomType.MarArchive or NitroRomType.FileFolder).AsObservable());
    }

    private void NitroContentSourceOnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<NitroRomNode> e)
    {
        SelectedNitroRomNode = e.SelectedItems[0];
    }

    private async Task OpenFile()
    {
        try
        {
            var selectedFiles = await Window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Fossil Fighter ROM",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Nintendo DS ROM")
                    {
                        Patterns = new[] { "*.nds" },
                        MimeTypes = new[] { "application/x-nintendo-ds-rom" }
                    }
                }
            });

            if (selectedFiles.Count == 0) return;

            LoadedRom = NdsFilesystem.FromFile(selectedFiles[0].TryGetLocalPath()!);
            NitroRomNodes.Clear();

            foreach (var subDirectory in LoadedRom.RootDirectory.SubDirectories)
            {
                NitroRomNodes.Add(new NitroRomNode(subDirectory));
            }

            foreach (var file in LoadedRom.RootDirectory.Files)
            {
                NitroRomNodes.Add(new NitroRomNode(file));
            }
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task SaveFile()
    {
        Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");

        try
        {
            var selectedFile = await Window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Nintendo DS ROM")
                    {
                        Patterns = new[] { "*.nds" },
                        MimeTypes = new[] { "application/x-nintendo-ds-rom" }
                    }
                },
                ShowOverwritePrompt = true
            });

            if (selectedFile is null) return;

            LoadedRom.SaveChanges(selectedFile.TryGetLocalPath()!);
            await ShowDialog("Save completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task ImportFile()
    {
        Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");
        Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

        try
        {
            var selectedFiles = await Window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = $"Select file to import into {SelectedNitroRomNode.Name}"
            });

            if (selectedFiles.Count == 0) return;

            await LoadedRom.GetFileByPath(SelectedNitroRomNode.FullPath).WriteFromAsync(await selectedFiles[0].OpenReadAsync());
            SelectedNitroRomNode.Version++;

            await ShowDialog("Import completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task ExportFile()
    {
        Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

        try
        {
            var selectedFolders = await Window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (selectedFolders.Count == 0) return;

            ExportFile(SelectedNitroRomNode, selectedFolders[0].TryGetLocalPath()!);

            await ShowDialog("Export completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task CompressFile()
    {
        Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");
        Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

        try
        {
            var selectedFolders = await Window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = $"Select folder to compress into {SelectedNitroRomNode.Name}"
            });

            if (selectedFolders.Count == 0) return;

            var metaFilePath = Path.Combine(selectedFolders[0].TryGetLocalPath()!, "meta.json");

            if (!File.Exists(metaFilePath))
            {
                await ShowDialog("Error", "meta.json file does not exist. Please decompress the file first to generate a meta file.");
                return;
            }

            var mcmMetadata = JsonSerializer.Deserialize(File.OpenRead(metaFilePath), CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata);

            using var outputFile = new MemoryStream();

            using (var marArchive = new MarArchive(outputFile, MarArchiveMode.Create, true))
            {
                var matcher = new Matcher();
                matcher.AddIncludePatterns(new[] { "*.bin" });

                foreach (var file in matcher.GetResultsInFullPath(selectedFolders[0].TryGetLocalPath()!).OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s))))
                {
                    var marArchiveEntry = marArchive.CreateEntry();
                    await using var mcmFileStream = marArchiveEntry.OpenWrite();

                    if (mcmMetadata is not null)
                    {
                        mcmFileStream.LoadMetadata(mcmMetadata[int.Parse(Path.GetFileNameWithoutExtension(file))]);
                    }

                    await using var fileStream = File.OpenRead(file);
                    await fileStream.CopyToAsync(mcmFileStream);
                }
            }

            outputFile.Seek(0, SeekOrigin.Begin);
            await LoadedRom.GetFileByPath(SelectedNitroRomNode.FullPath).WriteFromAsync(outputFile);
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task DecompressFile()
    {
        Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

        try
        {
            var selectedFolders = await Window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select output folder for decompressed archive"
            });

            if (selectedFolders.Count == 0) return;

            DecompressFile(SelectedNitroRomNode, selectedFolders[0].TryGetLocalPath()!);

            await ShowDialog("Decompress completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private void DecompressFile(NitroRomNode targetFile, string targetLocation)
    {
        Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");

        if (targetFile.IsFile)
        {
            if (targetFile.FileType != NitroRomType.MarArchive)
            {
                ExportFile(targetFile, targetLocation);
            }
            else
            {
                using var nitroRomFile = LoadedRom.GetFileByPath(targetFile.FullPath).OpenRead();
                using var marArchive = new MarArchive(nitroRomFile);

                var mcmFileMetadata = new Dictionary<int, McmFileMetadata>();

                for (var index = 0; index < marArchive.Entries.Count; index++)
                {
                    var marArchiveEntry = marArchive.Entries[index];
                    var outputFileDirectory = Path.Combine(targetLocation, targetFile.Name);

                    if (!Directory.Exists(outputFileDirectory))
                    {
                        Directory.CreateDirectory(outputFileDirectory);
                    }

                    using var mcmFile = marArchiveEntry.OpenRead();
                    using var outputFile = File.Open(Path.Combine(outputFileDirectory, $"{index}.bin"), FileMode.Create);
                    mcmFile.CopyTo(outputFile);
                    mcmFileMetadata.Add(index, mcmFile.GetFileMetadata());
                }

                var mcmFileMetadataOutput = Path.Combine(targetLocation, targetFile.Name, "meta.json");
                File.WriteAllText(mcmFileMetadataOutput, JsonSerializer.Serialize(mcmFileMetadata, CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata));
            }
        }
        else
        {
            foreach (var targetFolderChildNode in targetFile.ChildNodes)
            {
                DecompressFile(targetFolderChildNode, targetFolderChildNode.IsFile ? targetLocation : Path.Combine(targetLocation, targetFolderChildNode.Name));
            }
        }
    }

    private void ExportFile(NitroRomNode targetFile, string targetLocation)
    {
        Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");

        if (targetFile.IsFile)
        {
            if (!Directory.Exists(targetLocation))
            {
                Directory.CreateDirectory(targetLocation);
            }

            using var outputFile = File.OpenWrite(Path.Combine(targetLocation, targetFile.Name));
            using var nitroRomFile = LoadedRom.GetFileByPath(targetFile.FullPath).OpenRead();
            nitroRomFile.CopyTo(outputFile);
        }
        else
        {
            foreach (var targetFolderChildNode in targetFile.ChildNodes)
            {
                ExportFile(targetFolderChildNode, targetFolderChildNode.IsFile ? targetLocation : Path.Combine(targetLocation, targetFolderChildNode.Name));
            }
        }
    }
}