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
using System.Text.Json;
using TheDialgaTeam.FossilFighters.Assets;
using TheDialgaTeam.FossilFighters.Assets.GameData;

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Command;

internal sealed class ConvertCommand : System.CommandLine.Command
{
    public ConvertCommand() : base("convert", "List of converter available.")
    {
        AddCommand(new ConvertDtxFileCommand());
    }
}

internal sealed class ConvertDtxFileCommand : System.CommandLine.Command
{
    public ConvertDtxFileCommand() : base("dtx", "Convert json file into dtx file format.")
    {
        var inputArgument = new Argument<string>("input", "Input json file to convert.") { Arity = ArgumentArity.ExactlyOne };
        var outputArgument = new Argument<string>("output", "Output file after conversion.") { Arity = ArgumentArity.ExactlyOne };

        AddArgument(inputArgument);
        AddArgument(outputArgument);

        this.SetHandler(static (inputFilePath, outputFilePath) =>
        {
            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine(Localization.InputDoesNotExists, inputFilePath);
            }
            else
            {
                using var inputFileStream = File.OpenRead(inputFilePath);
                var dtxFile = JsonSerializer.Deserialize(inputFileStream, CustomJsonSerializerContext.Custom.DtxFile) ?? new DtxFile();

                using var outputFileStream = File.OpenWrite(outputFilePath);
                dtxFile.WriteToStream(outputFileStream);

                Console.WriteLine(Localization.ConvertDtxFileCommand_ConvertDtxFileCommand_Converted, inputFilePath, outputFilePath);
            }
        }, inputArgument, outputArgument);
    }
}