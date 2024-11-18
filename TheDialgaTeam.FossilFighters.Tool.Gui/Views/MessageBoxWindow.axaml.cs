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

using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Views;

public partial class MessageBoxWindow : ReactiveWindow<MessageBoxWindowViewModel>
{
    public MessageBoxWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            this.Bind(ViewModel, viewModel => viewModel.Title, view => view.Title)
                .DisposeWith(disposable);

            this.Bind(ViewModel, viewModel => viewModel.Message, view => view.MessageTextBlock.Text)
                .DisposeWith(disposable);

            this.BindCommand(ViewModel, viewModel => viewModel.Okay, view => view.OkayButton)
                .DisposeWith(disposable);

            this.WhenAnyValue(view => view.ViewModel!.Close).Subscribe(close =>
            {
                if (close) Window.Close();
            }).DisposeWith(disposable);
        });
    }
}