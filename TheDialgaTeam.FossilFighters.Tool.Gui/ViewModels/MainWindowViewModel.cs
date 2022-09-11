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
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using ReactiveUI;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public HierarchicalTreeDataGridSource<NitroRomNode> NitroContentSource { get; }

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

    public ReactiveCommand<Unit, Unit> CompressCommand { get; }

    public ReactiveCommand<Unit, Unit> DecompressCommand { get; }

    public bool IsRomLoaded => _isRomLoaded;

    public NitroRomNode? SelectedNitroRomNode => _selectedNitroRomNode;

    private readonly ObservableCollection<NitroRomNode> _nitroContents = new();

    private bool _isRomLoaded;
    private NdsFilesystem? _loadedRom;
    private NitroRomNode? _selectedNitroRomNode;

    public MainWindowViewModel(Window window) : base(window)
    {
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        CompressCommand = ReactiveCommand.Create(Compress);
        DecompressCommand = ReactiveCommand.Create(Decompress);

        NitroContentSource = new HierarchicalTreeDataGridSource<NitroRomNode>(_nitroContents)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<NitroRomNode>(new TextColumn<NitroRomNode, string>("Name", node => node.Name), node => node.Subfolders, node => node.Subfolders.Count > 0),
                new TextColumn<NitroRomNode, string>("Type", node => node.FileType),
                new TextColumn<NitroRomNode, string>("Size", node => !node.IsFile ? string.Empty : $"{node.Size:N0} B")
            }
        };

        NitroContentSource.RowSelection!.SelectionChanged += NitroContentSourceOnSelectionChanged;
    }

    private void NitroContentSourceOnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<NitroRomNode> e)
    {
        this.RaiseAndSetIfChanged(ref _selectedNitroRomNode, e.SelectedItems[0], nameof(SelectedNitroRomNode));
    }

    private async Task OpenFile()
    {
        try
        {
            var fileDialog = new OpenFileDialog { AllowMultiple = false, Filters = new List<FileDialogFilter> { new() { Extensions = { "nds" } } } };
            var selectedFile = await fileDialog.ShowAsync(Window);
            if (selectedFile == null) return;
            
            _loadedRom = NdsFilesystem.FromFile(selectedFile[0]);
            this.RaiseAndSetIfChanged(ref _isRomLoaded, true, nameof(IsRomLoaded));

            _nitroContents.Clear();

            foreach (var subDirectory in _loadedRom.RootDirectory.SubDirectories)
            {
                _nitroContents.Add(new NitroRomNode(subDirectory));
            }

            foreach (var file in _loadedRom.RootDirectory.Files)
            {
                _nitroContents.Add(new NitroRomNode(file));
            }
        }
        catch (Exception ex)
        {
            await this.ShowDialog("Error", ex.ToString());
        }
    }

    private void Compress()
    {
    }

    private void Decompress()
    {
    }
}