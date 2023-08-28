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
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TheDialgaTeam.FossilFighters.Assets;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public sealed class MainWindowViewModel : ViewModel
{
    [ObservableAsProperty]
    public bool IsRomLoaded { get; }

    [ObservableAsProperty]
    public bool IsBusy { get; }

    public Interaction<string, Task> ShowMessageBox { get; } = new();

    public Interaction<Exception, Task> ShowErrorMessageBox { get; } = new();

    public Interaction<ProgressBarViewModel, Task> ShowProgressBar { get; } = new();

    public Interaction<FilePickerOpenOptions, IReadOnlyList<IStorageFile>> OpenFilePicker { get; } = new();

    public Interaction<FilePickerSaveOptions, IStorageFile?> SaveFilePicker { get; } = new();

    public Interaction<FolderPickerOpenOptions, IReadOnlyList<IStorageFolder>> OpenFolderPicker { get; } = new();

    public Interaction<Uri, IStorageFile?> TryGetFileFromPath { get; } = new();

    public ReactiveCommand<Unit, Unit> OpenFile { get; }

    public ReactiveCommand<Unit, Unit> SaveFile { get; }

    public ReactiveCommand<Unit, Unit> ImportFile { get; }

    public ReactiveCommand<Unit, Unit> ExportFile { get; }

    public ReactiveCommand<Unit, Unit> CompressFile { get; }

    public ReactiveCommand<Unit, Unit> DecompressFile { get; }

    public HierarchicalTreeDataGridSource<NitroRomNode> NitroRomNodeSource { get; }

    private readonly ObservableCollection<NitroRomNode> _nitroRomNodes = new();

    [Reactive]
    private NdsFilesystem? LoadedRom { get; set; }

    [ObservableAsProperty]
    private NitroRomNode? SelectedNitroRomNode { get; set; }

    public MainWindowViewModel()
    {
        this.WhenAnyValue(model => model.LoadedRom)
            .Select(filesystem => filesystem is not null)
            .ToPropertyEx(this, model => model.IsRomLoaded);

        NitroRomNodeSource = new HierarchicalTreeDataGridSource<NitroRomNode>(_nitroRomNodes)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<NitroRomNode>(new TextColumn<NitroRomNode, string>("Name", node => node.Name), node => node.ChildNodes, node => node.ChildNodes.Count > 0),
                new TextColumn<NitroRomNode, string>("Type", node => node.FileTypeDisplay),
                new TextColumn<NitroRomNode, string>("Size", node => node.FileType == NitroRomType.FileFolder ? string.Empty : $"{node.Size:N0} B")
            }
        };

        Observable.FromEventPattern<EventHandler<TreeSelectionModelSelectionChangedEventArgs<NitroRomNode>>, TreeSelectionModelSelectionChangedEventArgs<NitroRomNode>>(
                handler => NitroRomNodeSource.RowSelection!.SelectionChanged += handler,
                handler => NitroRomNodeSource.RowSelection!.SelectionChanged -= handler)
            .Select(pattern => pattern.EventArgs.SelectedItems[0])
            .ToPropertyEx(this, model => model.SelectedNitroRomNode);

        OpenFile = ReactiveCommand.CreateFromTask(OpenFileImplementation, this.WhenAnyValue(model => model.IsBusy).Select(isBusy => !isBusy));
        SaveFile = ReactiveCommand.CreateFromTask(SaveFileImplementation, this.WhenAnyValue(model => model.IsRomLoaded, model => model.IsBusy, (isRomLoaded, isBusy) => isRomLoaded && !isBusy));

        ImportFile = ReactiveCommand.CreateFromTask(ImportFileImplementation, this.WhenAnyValue(model => model.SelectedNitroRomNode, model => model.IsBusy, (selectedNode, isBusy) => selectedNode?.FileType is NitroRomType.File or NitroRomType.MarArchive && !isBusy));
        ExportFile = ReactiveCommand.CreateFromTask(ExportFileImplementation, this.WhenAnyValue(model => model.SelectedNitroRomNode, model => model.IsBusy, (selectedNode, isBusy) => selectedNode is not null && !isBusy));
        CompressFile = ReactiveCommand.CreateFromTask(CompressFileImplementation, this.WhenAnyValue(model => model.SelectedNitroRomNode, model => model.IsBusy, (selectedNode, isBusy) => selectedNode?.FileType is NitroRomType.MarArchive && !isBusy));
        DecompressFile = ReactiveCommand.CreateFromTask(DecompressFileImplementation, this.WhenAnyValue(model => model.SelectedNitroRomNode, model => model.IsBusy, (selectedNode, isBusy) => selectedNode?.FileType is NitroRomType.MarArchive or NitroRomType.FileFolder && !isBusy));

        this.WhenAnyObservable(model => model.OpenFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
        this.WhenAnyObservable(model => model.SaveFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
        this.WhenAnyObservable(model => model.ImportFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
        this.WhenAnyObservable(model => model.ExportFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
        this.WhenAnyObservable(model => model.CompressFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
        this.WhenAnyObservable(model => model.DecompressFile.IsExecuting).ToPropertyEx(this, model => model.IsBusy);
    }

    private async Task OpenFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = true };

        try
        {
            var selectedFiles = await OpenFilePicker.Handle(new FilePickerOpenOptions
            {
                Title = "Select Fossil Fighters ROM",
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

            await ShowProgressBar.Handle(progress);

            await using var fileStream = await selectedFiles[0].OpenReadAsync();
            LoadedRom = await NdsFilesystem.FromFileAsync(fileStream);

            _nitroRomNodes.Clear();

            foreach (var subDirectory in LoadedRom.RootDirectory.SubDirectories)
            {
                _nitroRomNodes.Add(new NitroRomNode(subDirectory));
            }

            foreach (var file in LoadedRom.RootDirectory.Files)
            {
                _nitroRomNodes.Add(new NitroRomNode(file));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }

    private async Task SaveFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = true };

        try
        {
            Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");

            var selectedFile = await SaveFilePicker.Handle(new FilePickerSaveOptions
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

            await ShowProgressBar.Handle(progress);

            await using var outputFile = await selectedFile.OpenWriteAsync();
            await LoadedRom.WriteToAsync(outputFile);

            await ShowMessageBox.Handle("Save completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }

    private async Task ImportFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = true };

        try
        {
            Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");
            Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

            var selectedFiles = await OpenFilePicker.Handle(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = $"Select file to import into {SelectedNitroRomNode.Name}"
            });

            if (selectedFiles.Count == 0) return;

            await ShowProgressBar.Handle(progress);

            await using var file = await selectedFiles[0].OpenReadAsync();
            await SelectedNitroRomNode.WriteFromAsync(file);

            await ShowMessageBox.Handle("Import completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }

    private async Task ExportFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = true };

        try
        {
            Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

            var selectedFolders = await OpenFolderPicker.Handle(new FolderPickerOpenOptions());
            if (selectedFolders.Count == 0) return;

            await ShowProgressBar.Handle(progress);

            await SelectedNitroRomNode.ExportFilesAsync(selectedFolders[0]);

            await ShowMessageBox.Handle("Export completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }

    private async Task CompressFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = false, IsCancellable = true };

        try
        {
            Debug.Assert(LoadedRom != null, nameof(LoadedRom) + " != null");
            Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

            var selectedFolders = await OpenFolderPicker.Handle(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = $"Select folder to compress into {SelectedNitroRomNode.Name}"
            });

            if (selectedFolders.Count == 0) return;

            await ShowProgressBar.Handle(progress);

            IStorageItem? metaFileItem = null;
            var binFileItems = new SortedList<int, IStorageItem>();

            await foreach (var item in selectedFolders[0].GetItemsAsync())
            {
                if (item.Name.Equals("meta.json"))
                {
                    metaFileItem = item;
                }

                if (item.Name.EndsWith(".bin") && int.TryParse(Path.GetFileNameWithoutExtension(item.Name), out var fileIndex))
                {
                    binFileItems.Add(fileIndex, item);
                }
            }

            progress.MaxValue = binFileItems.Count;

            if (metaFileItem is null) throw new FileNotFoundException("meta.json file does not exist. Please decompress the file first to generate a meta file.");

            var metaFile = await TryGetFileFromPath.Handle(metaFileItem.Path);
            if (metaFile is null) throw new FileNotFoundException("meta.json file does not exist. Please decompress the file first to generate a meta file.");

            await using var metaFileStream = await metaFile.OpenReadAsync();
            var metadata = JsonSerializer.Deserialize(metaFileStream, CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata);

            using var outputFile = new MemoryStream();

            using (var marArchive = new MarArchive(outputFile, MarArchiveMode.Create, true))
            {
                foreach (var (fileIndex, value) in binFileItems)
                {
                    var binFile = await TryGetFileFromPath.Handle(value.Path);
                    if (binFile is null) throw new FileNotFoundException(null, value.Name);

                    var marArchiveEntry = marArchive.CreateEntry();
                    await using var mcmFileStream = marArchiveEntry.OpenWrite();

                    if (metadata is not null && metadata.TryGetValue(fileIndex, out var mcmFileMetadata))
                    {
                        mcmFileStream.LoadMetadata(mcmFileMetadata);
                    }

                    await using var binFileStream = await binFile.OpenReadAsync();
                    await binFileStream.CopyToAsync(mcmFileStream);

                    if (progress.CancellationToken.IsCancellationRequested) return;
                    progress.Value++;
                }
            }

            if (progress.CancellationToken.IsCancellationRequested) return;

            outputFile.Seek(0, SeekOrigin.Begin);
            await SelectedNitroRomNode.WriteFromAsync(outputFile);

            await ShowMessageBox.Handle("Compress completed.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }

    private async Task DecompressFileImplementation()
    {
        using var progress = new ProgressBarViewModel { IsIndeterminate = false };

        try
        {
            Debug.Assert(SelectedNitroRomNode != null, nameof(SelectedNitroRomNode) + " != null");

            var selectedFolders = await OpenFolderPicker.Handle(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select output folder to store decompress archives."
            });

            if (selectedFolders.Count == 0) return;

            await ShowProgressBar.Handle(progress);

            await SelectedNitroRomNode.DecompressFilesAsync(selectedFolders[0]);

            await ShowMessageBox.Handle("Decompress completed.");
        }
        catch (Exception ex)
        {
            await ShowErrorMessageBox.Handle(ex);
        }
    }
}