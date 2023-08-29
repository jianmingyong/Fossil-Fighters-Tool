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
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;

public sealed class ProgressBarViewModel : ActivatableViewModel, IDisposable
{
    [ObservableAsProperty]
    public bool IsDone { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Reactive]
    public int Value { get; set; }

    [Reactive]
    public int MaxValue { get; set; } = 100;

    public bool IsIndeterminate { get; init; }

    public bool IsCancellable { get; init; }

    [Reactive]
    public ReactiveCommand<Unit, Unit>? Cancel { get; private set; }

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ProgressBarViewModel()
    {
        this.WhenActivated(disposable =>
        {
            this.WhenAnyValue(viewModel => viewModel.Value)
                .Select(value => value >= MaxValue)
                .ToPropertyEx(this, viewModel => viewModel.IsDone)
                .DisposeWith(disposable);

            Cancel = ReactiveCommand.Create(() => { _cancellationTokenSource.Cancel(); }).DisposeWith(disposable);
        });
    }

    public void Dispose()
    {
        Value = MaxValue;
        _cancellationTokenSource.Dispose();
    }
}