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

using System.Diagnostics;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using TheDialgaTeam.FossilFighters.Tool.Gui.Utilities;
using TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            Debug.Assert(ViewModel != null, nameof(ViewModel) + " != null");

            ViewModel.ShowMessageBox.RegisterHandler(context => { context.SetOutput(this.ShowMessageBox(context.Input)); }).DisposeWith(disposable);

            ViewModel.ShowErrorMessageBox.RegisterHandler(context => { context.SetOutput(this.ShowMessageBox("Error", context.Input.ToString())); }).DisposeWith(disposable);

            ViewModel.OpenFilePicker.RegisterHandler(async context =>
            {
                var result = await StorageProvider.OpenFilePickerAsync(context.Input);
                context.SetOutput(result);
            }).DisposeWith(disposable);

            ViewModel.SaveFilePicker.RegisterHandler(async context =>
            {
                var result = await StorageProvider.SaveFilePickerAsync(context.Input);
                context.SetOutput(result);
            }).DisposeWith(disposable);

            ViewModel.OpenFolderPicker.RegisterHandler(async context =>
            {
                var result = await StorageProvider.OpenFolderPickerAsync(context.Input);
                context.SetOutput(result);
            }).DisposeWith(disposable);

            ViewModel.TryGetFileFromPath.RegisterHandler(async context =>
            {
                var result = await StorageProvider.TryGetFileFromPathAsync(context.Input);
                context.SetOutput(result);
            }).DisposeWith(disposable);
        });
    }
}