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
using TheDialgaTeam.FossilFighters.Tool.Gui.Views;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<Node> NitroContents { get; } = new();

    public ObservableCollection<Node> SelectedFiles { get; set; }

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    private readonly Window _window;

    private NdsFilesystem? _romLoaded;

    public MainWindowViewModel(Window window)
    {
        _window = window;
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        ExitCommand = ReactiveCommand.Create(Exit);
    }

    private async Task OpenFile()
    {
        var fileDialog = new OpenFileDialog { AllowMultiple = false, Filters = new List<FileDialogFilter> { new() { Extensions = { "nds" } } } };
        var selectedFile = await fileDialog.ShowAsync(_window);

        if (selectedFile == null) return;

        try
        {
            _romLoaded = NdsFilesystem.FromFile(selectedFile[0]);

            NitroContents.Clear();

            foreach (var subDirectory in _romLoaded.RootDirectory.SubDirectories)
            {
                NitroContents.Add(new Node(subDirectory));
            }

            foreach (var file in _romLoaded.RootDirectory.Files)
            {
                NitroContents.Add(new Node(file));
            }
        }
        catch (Exception ex)
        {
            await MessageBoxWindow.ShowDialog(_window, "Error", ex.ToString());
        }
    }

    private void Exit()
    {
        _window.Close();
    }
}