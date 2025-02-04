using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

internal class Program
{
    private static string _repoUrl;
    private static string _projectFile;
    private static string _tempDir;
    private static string _projectDirectory;
    private static string _solutionName;
    private static string _solutionDirectory;

    private static void Main()
    {
        LoadConfiguration();

        DownloadTestsFromGithub();

        CreateSolution();
    }

    private static void CreateSolution()
    {
        var solutionPath = Path.Combine(_solutionDirectory, _solutionName);

        try
        {
            // Создание папки, если её нет
            Directory.CreateDirectory(_solutionDirectory);

            // Создание решения
            RunCommand("dotnet", $"new sln -n {_solutionName}  --force", _solutionDirectory);

            // Добавление проекта в решение
            var projectPath = Path.Combine(_projectDirectory, _projectFile);
            RunCommand("dotnet", $"sln {_solutionName}.sln add \"{projectPath}\" ", Path.GetFullPath(_solutionDirectory));

            Console.WriteLine($"Файл решения создан: {solutionPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void RunCommand(string command, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
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

        using var process = Process.Start(psi);
        process.WaitForExit();
        
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!string.IsNullOrWhiteSpace(output))
            Console.WriteLine(output);

        if (!string.IsNullOrWhiteSpace(error))
            throw new Exception($"Ошибка: {error}");
    }
    
    private static void DownloadTestsFromGithub()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Console.WriteLine("Временная папка уже существует, удаляем...");
                RunCommand("cmd.exe", $"/c rmdir /s /q \"{_tempDir}\"");
            }
            
            Console.WriteLine("Клонируем репозиторий (без файлов)...");
            RunCommand("git", $"clone {_repoUrl} {_tempDir}");

            Console.WriteLine("Ищем нужный файл...");
            var foundFolder = FindFolderContainingFile(_tempDir, _projectFile);
            if (foundFolder == null)
            {
                Console.WriteLine($"Файл '{_projectFile}' не найден.");
                return;
            }

            Console.WriteLine($"Файл найден в папке: {foundFolder}");

            Console.WriteLine("Перемещаем найденную папку...");
            var projectDir = Path.Combine(_solutionDirectory, _projectDirectory);
            if (Directory.Exists(projectDir))
            {
                Console.WriteLine("Выходная папка уже существует, удаляем...");
                Directory.Delete(projectDir, true);
            }

            if (!Directory.Exists(_solutionDirectory))
            {
                Directory.CreateDirectory(_solutionDirectory);
            }
            
            Directory.Move(foundFolder, projectDir);

            Console.WriteLine($"Готово! Папка сохранена в {projectDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(_tempDir))
                RunCommand("cmd.exe", $"/c rmdir /s /q \"{_tempDir}\"");
        }
    }


    private static void LoadConfiguration()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _repoUrl = config["GitHub:RepoUrl"];
        _projectFile = config["GitHub:ProjectFile"];
        _tempDir = config["GitHub:TempDir"];
        _projectDirectory = config["GitHub:ProjectDirectory"];
        _solutionName = config["GitHub:SolutionName"];
        _solutionDirectory = config["GitHub:SolutionDirectory"];
    }

    private static string FindFolderContainingFile(string rootDir, string searchFile)
    {
        foreach (var file in Directory.GetFiles(rootDir, searchFile, SearchOption.AllDirectories))
        {
            return Path.GetDirectoryName(file)?
                .Replace(rootDir + Path.DirectorySeparatorChar, "")
                .Replace("\\", "/");
        }
        return null;
    }

    private static void RunCommand(string command, string arguments)
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
