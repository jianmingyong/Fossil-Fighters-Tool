// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2022 Yong Jian Ming
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
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using JetBrains.Annotations;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    [ObservableAsProperty]
    [UsedImplicitly]
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
        this.WhenAnyValue(model => model.LoadedRom).Select(filesystem => filesystem != null).ToPropertyEx(this, model => model.IsRomLoaded);

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
        SaveFileCommand = ReactiveCommand.CreateFromTask(SaveFile, this.WhenAnyValue(model => model.LoadedRom).Select(filesystem => filesystem != null).AsObservable());
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
            var fileDialog = new OpenFileDialog { AllowMultiple = false, Filters = new List<FileDialogFilter> { new() { Extensions = { "nds" } } } };
            var selectedFile = await fileDialog.ShowAsync(Window);
            if (selectedFile == null) return;

            LoadedRom = NdsFilesystem.FromFile(selectedFile[0]);
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
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task ImportFile()
    {
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task ExportFile()
    {
        try
        {
            var folderDialog = new OpenFolderDialog();
            var selectedFolder = await folderDialog.ShowAsync(Window);
            if (selectedFolder == null) return;

            ExportFile(SelectedNitroRomNode!, selectedFolder);

            await ShowDialog("Export completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task CompressFile()
    {
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private async Task DecompressFile()
    {
        try
        {
            var folderDialog = new OpenFolderDialog();
            var selectedFolder = await folderDialog.ShowAsync(Window);
            if (selectedFolder == null) return;

            DecompressFile(SelectedNitroRomNode!, selectedFolder);

            await ShowDialog("Decompress completed.");
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", ex.ToString());
        }
    }

    private void DecompressFile(NitroRomNode targetFile, string targetLocation)
    {
        if (targetFile.IsFile)
        {
            if (targetFile.FileType != NitroRomType.MarArchive)
            {
                ExportFile(targetFile, targetLocation);
            }
            else
            {
                using var nitroRomFile = LoadedRom!.GetFileByPath(targetFile.FullPath).OpenRead();
                using var marArchive = new MarArchive(nitroRomFile);

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
                }
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
        if (targetFile.IsFile)
        {
            if (!Directory.Exists(targetLocation))
            {
                Directory.CreateDirectory(targetLocation);
            }

            using var outputFile = File.Open(Path.Combine(targetLocation, targetFile.Name), FileMode.Create);
            using var nitroRomFile = LoadedRom!.GetFileByPath(targetFile.FullPath).OpenRead();
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