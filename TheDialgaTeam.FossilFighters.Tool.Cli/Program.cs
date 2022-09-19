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

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using TheDialgaTeam.FossilFighters.Tool.Cli.Command;

namespace TheDialgaTeam.FossilFighters.Tool.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand(Localization.FossilFightersToolDescription);
        rootCommand.AddCommand(new DecompressCommand());
        rootCommand.AddCommand(new CompressCommand());

        if (args.Length > 0)
        {
            if (File.Exists(args[0]) || Directory.Exists(args[0]))
            {
                var newArgs = new List<string>(args);
                newArgs.Insert(0, "decompress");
                args = newArgs.ToArray();
            }
        }

        return await new CommandLineBuilder(rootCommand).UseDefaults().Build().InvokeAsync(args);
    }
}