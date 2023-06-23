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

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Command;

internal sealed class ConvertCommand : System.CommandLine.Command
{
    public ConvertCommand() : base("convert", "List of converter available.")
    {
        AddCommand(new ConvertImageCommand());
    }
}

internal sealed class ConvertImageCommand : System.CommandLine.Command
{
    public ConvertImageCommand() : base("image", "Convert image to FF1/FFC compatible format.")
    {
        var inputArgument = new Argument<string>("targetImage", "Target image to convert.") { Arity = ArgumentArity.ExactlyOne };
        
        var outputOption = new Option<string>(new[] { "--output", "-o" }, () => string.Empty, "Output folder after conversion.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false };

        
    }
}