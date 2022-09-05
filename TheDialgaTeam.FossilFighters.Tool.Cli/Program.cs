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
            var validCommand = new List<string>();
            var hasValidCommand = false;

            foreach (var subcommand in rootCommand.Subcommands)
            {
                foreach (var alias in subcommand.Aliases)
                {
                    validCommand.Add(alias);
                }
            }

            foreach (var command in validCommand)
            {
                if (args[0].Equals(command, StringComparison.OrdinalIgnoreCase))
                {
                    hasValidCommand = true;
                    break;
                }
            }

            if (!hasValidCommand)
            {
                var newArgs = new List<string>(args);
                newArgs.Insert(0, "decompress");
                args = newArgs.ToArray();
            }
        }

        return await new CommandLineBuilder(rootCommand).UseDefaults().Build().InvokeAsync(args);
    }
}