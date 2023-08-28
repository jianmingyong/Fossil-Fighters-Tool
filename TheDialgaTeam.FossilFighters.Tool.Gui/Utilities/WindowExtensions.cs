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

using System.Threading.Tasks;
using Avalonia.Controls;
using TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels;
using TheDialgaTeam.FossilFighters.Tool.Gui.Views;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Utilities;

public static class WindowExtensions
{
    public static Task ShowMessageBox(this Window window, string message)
    {
        return ShowMessageBox(window, string.Empty, message);
    }

    public static Task ShowMessageBox(this Window window, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(title))
        {
            title = window.Title ?? string.Empty;
        }

        var messageBox = new MessageBoxWindow
        {
            DataContext = new MessageBoxWindowViewModel(title, message)
        };
        return messageBox.ShowDialog(window);
    }

    public static Task ShowProgressBar(this Window window, ProgressBarViewModel progressBarViewModel)
    {
        var progressBar = new ProgressBarWindow
        {
            DataContext = progressBarViewModel
        };

        return progressBar.ShowDialog(window);
    }
}