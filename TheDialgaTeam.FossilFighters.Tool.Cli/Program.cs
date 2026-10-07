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

using System.CommandLine;
using TheDialgaTeam.FossilFighters.Tool.Cli.Commands;

namespace TheDialgaTeam.FossilFighters.Tool.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand(Localization.FossilFightersToolDescription)
        {
            new CompressCommand(),
            new DecompressCommand(),
            new ConvertCommand()
        };

        if (args.Length > 0)
        {
            if (File.Exists(args[0]) || Directory.Exists(args[0]))
            {
                var newArgs = new List<string>(args);
                newArgs.Insert(0, "decompress");
                args = newArgs.ToArray();
            }
        }

        return rootCommand.Parse(args).Invoke();
    }
}