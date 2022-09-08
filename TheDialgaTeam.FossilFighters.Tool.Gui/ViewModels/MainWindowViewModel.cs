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
using ReactiveUI;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<NitroRomNode> NitroContents { get; } = new();

    public ObservableCollection<NitroRomNode> SelectedFiles { get; } = new();

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

    public ReactiveCommand<Unit, Unit> CompressCommand { get; }

    public ReactiveCommand<Unit, Unit> DecompressCommand { get; }

    public bool IsRomLoaded
    {
        get => _isRomLoaded;

        private set
        {
            _isRomLoaded = value;
            this.RaisePropertyChanged();
        }
    }

    private bool _isRomLoaded;
    private NdsFilesystem? _loadedRom;

    public MainWindowViewModel(Window window) : base(window)
    {
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        CompressCommand = ReactiveCommand.Create(Compress);
        DecompressCommand = ReactiveCommand.Create(Decompress);
    }

    private async Task OpenFile()
    {
        try
        {
            var fileDialog = new OpenFileDialog { AllowMultiple = false, Filters = new List<FileDialogFilter> { new() { Extensions = { "nds" } } } };
            var selectedFile = await fileDialog.ShowAsync(Window);
            if (selectedFile == null) return;

            _loadedRom = NdsFilesystem.FromFile(selectedFile[0]);
            IsRomLoaded = true;

            NitroContents.Clear();

            foreach (var subDirectory in _loadedRom.RootDirectory.SubDirectories)
            {
                NitroContents.Add(new NitroRomNode(subDirectory));
            }

            foreach (var file in _loadedRom.RootDirectory.Files)
            {
                NitroContents.Add(new NitroRomNode(file));
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