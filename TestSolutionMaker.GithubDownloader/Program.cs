using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

class Program
{
    private static string RepoUrl;
    private static string SearchFile;
    private static string TempDir;
    private static string OutputDir;
    private static string SolutionName;
    private static string SolutionDirectory;

    static void Main()
    {
        LoadConfiguration();

        DownloadTestsFromGithub();

        CreateSolution();
    }

    private static void CreateSolution()
    {
        string solutionPath = Path.Combine(SolutionDirectory, SolutionName);

        try
        {
            // Создание папки, если её нет
            Directory.CreateDirectory(SolutionDirectory);

            // Создание решения
            RunCommand("dotnet", $"new sln -n {SolutionName}  --force", SolutionDirectory);

            // Добавление проекта в решение
            string projectPath = Path.Combine(OutputDir, SearchFile);
            RunCommand("dotnet", $"sln {SolutionName} add \"{projectPath}\"  --force", SolutionDirectory);

            Console.WriteLine($"Файл решения создан: {solutionPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static void RunCommand(string command, string arguments, string workingDirectory)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            StandardOutputEncoding = Encoding.UTF8, // Явная установка кодировки
            StandardErrorEncoding = Encoding.UTF8
        };

        using Process process = Process.Start(psi);
        process.WaitForExit();
        
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        if (!string.IsNullOrWhiteSpace(output))
            Console.WriteLine(output);

        if (!string.IsNullOrWhiteSpace(error))
            throw new Exception($"Ошибка: {error}");
    }
    
    private static void DownloadTestsFromGithub()
    {
        try
        {
            if (Directory.Exists(TempDir))
            {
                Console.WriteLine("Временная папка уже существует, удаляем...");
                RunCommand("cmd.exe", $"/c rmdir /s /q \"{TempDir}\"");
            }
            
            Console.WriteLine("Клонируем репозиторий (без файлов)...");
            RunCommand("git", $"clone {RepoUrl} {TempDir}");

            Console.WriteLine("Ищем нужный файл...");
            string foundFolder = FindFolderContainingFile(TempDir, SearchFile);
            if (foundFolder == null)
            {
                Console.WriteLine($"Файл '{SearchFile}' не найден.");
                return;
            }

            Console.WriteLine($"Файл найден в папке: {foundFolder}");

            Console.WriteLine("Перемещаем найденную папку...");
            if (Directory.Exists(OutputDir))
            {
                Console.WriteLine("Выходная папка уже существует, удаляем...");
                Directory.Delete(OutputDir, true);
            }
            Directory.Move(foundFolder, OutputDir);

            Console.WriteLine($"Готово! Папка сохранена в {OutputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(TempDir))
                RunCommand("cmd.exe", $"/c rmdir /s /q \"{TempDir}\"");
        }
    }


    static void LoadConfiguration()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        RepoUrl = config["GitHub:RepoUrl"];
        SearchFile = config["GitHub:SearchFile"];
        TempDir = config["GitHub:TempDir"];
        OutputDir = config["GitHub:OutputDir"];
        SolutionName = config["GitHub:SolutionName"];
        SolutionDirectory = config["GitHub:SolutionDirectory"];
    }

    static string FindFolderContainingFile(string rootDir, string searchFile)
    {
        foreach (var file in Directory.GetFiles(rootDir, searchFile, SearchOption.AllDirectories))
        {
            return Path.GetDirectoryName(file)?
                .Replace(rootDir + Path.DirectorySeparatorChar, "")
                .Replace("\\", "/");
        }
        return null;
    }

    static void RunCommand(string command, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
        process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Ошибка выполнения: {command} {arguments}");
        }
    }
}
