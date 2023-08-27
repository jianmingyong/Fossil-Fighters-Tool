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

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public sealed class MessageBoxWindowViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; }

    public Interaction<Unit, Unit> CloseWindow { get; } = new();

    public string Title { get; }

    public string Message { get; }

    [Reactive]
    public ReactiveCommand<Unit, Unit>? Okay { get; private set; }

    public MessageBoxWindowViewModel(string title, string message)
    {
        Activator = new ViewModelActivator();

        Title = title;
        Message = message;

        this.WhenActivated(disposable => { Okay = ReactiveCommand.CreateFromTask(async () => await CloseWindow.Handle(Unit.Default)).DisposeWith(disposable); });
    }
}