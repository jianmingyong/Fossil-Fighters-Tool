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

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Commands;

internal sealed class ConvertCommand : Command
{
    public ConvertCommand() : base("convert", "Show a list of converter available.")
    {
        AddCommand(new ConvertDmgFileCommand());
        AddCommand(new ConvertDtxFileCommand());
    }
}

internal sealed class ConvertDmgFileCommand : Command
{
    public ConvertDmgFileCommand() : base("dmg", "Convert json file into dmg file format.")
    {
        var inputArgument = new Argument<FileInfo>("inputFile", "Input json file to convert.") { Arity = ArgumentArity.ExactlyOne };
        inputArgument.ExistingOnly();

        var outputArgument = new Argument<FileInfo>("outputFile", "Output file after conversion.") { Arity = ArgumentArity.ExactlyOne };

        AddArgument(inputArgument);
        AddArgument(outputArgument);

        this.SetHandler(static (inputFile, outputFile) =>
        {
            using var inputFileStream = inputFile.OpenRead();
            var dmgFile = JsonSerializer.Deserialize(inputFileStream, CustomJsonSerializerContext.Custom.DmgFile) ?? new DmgFile();

            using var outputFileStream = outputFile.OpenWrite();
            dmgFile.WriteToStream(outputFileStream);

            Console.WriteLine(Localization.FileConvertedFromTo, inputFile.FullName, outputFile.FullName);
        }, inputArgument, outputArgument);
    }
}

internal sealed class ConvertDtxFileCommand : Command
{
    public ConvertDtxFileCommand() : base("dtx", "Convert json file into dtx file format.")
    {
        var inputArgument = new Argument<FileInfo>("inputFile", "Input json file to convert.") { Arity = ArgumentArity.ExactlyOne };
        inputArgument.ExistingOnly();

        var outputArgument = new Argument<FileInfo>("outputFile", "Output file after conversion.") { Arity = ArgumentArity.ExactlyOne };

        AddArgument(inputArgument);
        AddArgument(outputArgument);

        this.SetHandler(static (inputFile, outputFile) =>
        {
            using var inputFileStream = inputFile.OpenRead();
            var dtxFile = JsonSerializer.Deserialize(inputFileStream, CustomJsonSerializerContext.Custom.DtxFile) ?? new DtxFile();

            using var outputFileStream = outputFile.OpenWrite();
            dtxFile.WriteToStream(outputFileStream);

            Console.WriteLine(Localization.FileConvertedFromTo, inputFile.FullName, outputFile.FullName);
        }, inputArgument, outputArgument);
    }
}