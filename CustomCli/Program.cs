using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

var bundleOption = new Option<FileInfo>("--o", "File path and name");
var languageOption = new Option<string[]>("--l", () => new string[] { }, "A list of programming languages to include");
var noteOption = new Option<bool>("--n", "Include source file paths as comments in the bundle");
var sortOption = new Option<string>(
    "--s",
    description: "Sort files by 'name' (alphabetically by file name) or 'extension' (by file type). Default is 'name'.",
    getDefaultValue: () => "name"
);
var removeEmptyLinesOption = new Option<bool>(
    "--rel",
    description: "Remove empty lines from source code before adding to the bundle"
)
{
    IsRequired = false,
};
var authorOption = new Option<string>("--a", "Name of the author to include in the bundle header")
{
    IsRequired = false
};

languageOption.IsRequired = true;

var bundleCommand = new Command("bundle", "Bundle code files into a single file");
var languageExtensions = new Dictionary<string, string>
{
    { ".cs", "c#" },
    { ".java", "java" },
    { ".js", "javascript" },
    { ".ts", "typescript" },
    { ".py", "python" },
    { ".html", "html" },
    { ".htm", "html" },
    { ".css", "css" },
    { ".scss", "scss" },
    { ".sql", "sql" },
    { ".sh", "bash" },
    { ".ps1", "powershell" },
    { ".json", "json" },
    { ".xml", "xml" },
};

bundleCommand.AddOption(bundleOption);
bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler(async (FileInfo output, string[] languages, bool note, string sort, bool removeEmptyLines, string author) =>
{
    try
    {
        if (File.Exists(output.FullName))
        {
            Console.WriteLine("Choose another name. This name already exists");
            return;
        }

        Console.WriteLine($"Output file path: {output.FullName}");

        var filesToInclude = Directory.GetFiles(".", "*", SearchOption.AllDirectories)
            .Where(file =>
            {
                var extension = Path.GetExtension(file).ToLower();
                return languages.Contains("all") ||
                       languages.Any(lang => languageExtensions.TryGetValue(extension, out var matchedLang) && languages.Contains(matchedLang));
            }).ToArray();

        Console.WriteLine($"Files selected for bundling: {string.Join(", ", filesToInclude)}");

        var unknownExtensions = filesToInclude.Select(f => Path.GetExtension(f))
            .Distinct()
            .Except(languageExtensions.Keys);

        if (unknownExtensions.Any())
        {
            Console.WriteLine("Warning: Found files with unknown extensions:");
            foreach (var ext in unknownExtensions)
            {
                Console.WriteLine($"- {ext}");
            }
            Console.WriteLine("These files will not be included in the bundle.");
        }
        if (sort == "extension")
        {
            filesToInclude = filesToInclude.OrderBy(file => Path.GetExtension(file)).ThenBy(file => Path.GetFileName(file)).ToArray();
        }
        else
        {
            filesToInclude = filesToInclude.OrderBy(file => Path.GetFileName(file)).ToArray();
        }
        using (var writer = new StreamWriter(output.FullName, append: false))
        {
            if (!string.IsNullOrEmpty(author))
            {
                await writer.WriteLineAsync($"// Author: {author}");
            }

            foreach (var file in filesToInclude)
            {
                if (note)
                {
                    var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
                    await writer.WriteLineAsync($"// Source file: {relativePath}");
                }

                Console.WriteLine($"Adding file: {file}");
                var content = await File.ReadAllTextAsync(file);
                if (removeEmptyLines)
                {
                    content = string.Join(Environment.NewLine, content.Split(Environment.NewLine).Where(line => !string.IsNullOrWhiteSpace(line)));
                }
                await writer.WriteLineAsync(content);
            }
        }
    }
    catch (DirectoryNotFoundException ex)
    {
        Console.WriteLine("Error: Invalid file path.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}, bundleOption, languageOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

var createRspCommand = new Command("crsp", "Create a response file with prepared bundle command");
createRspCommand.SetHandler(() =>
{
    Console.Write("Enter output file path: ");
    var outputPath = Console.ReadLine();

    Console.Write("Enter languages to include (comma-separated, or 'all'): ");
    var languages = Console.ReadLine();

    Console.Write("Include source file paths as comments? (yes/no): ");
    var note = Console.ReadLine()?.ToLower() == "yes";

    Console.Write("Sort files by (name/extension): ");
    var sort = Console.ReadLine();

    Console.Write("Remove empty lines? (yes/no): ");
    var removeEmptyLines = Console.ReadLine()?.ToLower() == "yes";

    Console.Write("Enter author name (optional): ");
    var author = Console.ReadLine();

    Console.Write("Enter response file name (e.g., command.rsp): ");
    var rspFileName = Console.ReadLine();

    try
    {
        using (var writer = new StreamWriter(rspFileName, append: false))
        {
            writer.WriteLine($"--o {outputPath}");
            writer.WriteLine($"--l {languages}");
            if (note) writer.WriteLine("--n");
            writer.WriteLine($"--s {sort}");
            if (removeEmptyLines) writer.WriteLine("--rel");
            if (!string.IsNullOrEmpty(author)) writer.WriteLine($"--a \"{author}\"");
        }

        Console.WriteLine($"Response file '{rspFileName}' created successfully.");
        Console.WriteLine($"Run the command using: dotnet @{rspFileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
});

var rootCommand = new RootCommand("Root command for File Bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);

//Run commands:
//fib bundle --output my-file.txt --language c# --note --sort extension --rel --author "John Doe"
//fib crsp
//fib bundle @fileName.rsp



