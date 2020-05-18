using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace DiscordBot
{
    public class Grader
    {
        private const string ExtractFolderName = "extract";
        public static async Task GradeCSharp(CommandContext commandContext, DiscordAttachment attachment, string currentDirectory, string programToGrade, bool final)
        {
            string runPath = Path.Combine(Config.Instance.GraderDump, $"{commandContext.User.Id}/CS2430/{programToGrade}");

            bool finalExists = Directory.Exists(runPath) && Directory.EnumerateDirectories(runPath, "*-final").Any();
            if (finalExists)
            {
                await commandContext.RespondAsync("Unable to grade, you've already submitted your final submission");
            }
            else
            {
                string graderDataFolder = Path.Join(currentDirectory, "GraderData", "CS2430", programToGrade);
                if (Directory.Exists(graderDataFolder))
                {
                    await DownloadAndExtractFiles(attachment);
                    string sourcePath = Path.Join(currentDirectory, ExtractFolderName);

                    File.WriteAllText(Path.Combine(ExtractFolderName, "Build.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>netcoreapp3.1</TargetFramework></PropertyGroup></Project>");
                    ProcessStartInfo buildStartInfo = new ProcessStartInfo("dotnet", "build") { WorkingDirectory = ExtractFolderName };
                    (string buildStandard, string buildError, bool buildTimeout) = RunProcess(buildStartInfo, 5000);
                    string exeFile = Path.Combine(ExtractFolderName, "bin\\debug\\netcoreapp3.1\\Build.exe");

                    if (!buildTimeout && File.Exists(exeFile))
                    {
                        StringBuilder runsStringBuilder = new StringBuilder();
                        string fileGradeResults = GradeCSFiles(Directory.EnumerateFiles(sourcePath, "*.cs"));

                        if (!string.IsNullOrWhiteSpace(fileGradeResults))
                        {
                            runsStringBuilder.AppendLine("File Grade Results:");
                            runsStringBuilder.AppendLine(fileGradeResults);
                        }

                        await commandContext.RespondAsync("Build succeeded: Starting to test!");
                        List<string> filesToZip = new List<string>();
                        string runsFolder = Path.Join(ExtractFolderName, "Runs");
                        if (Directory.Exists(runsFolder))
                        {
                            Directory.Delete(runsFolder, true);
                        }
                        bool anyFailed = false;
                        Directory.CreateDirectory($"{ExtractFolderName}/Runs");
                        foreach (string inputFilePath in Directory.EnumerateFiles(graderDataFolder, "Input*").OrderBy(a => a))
                        {
                            FileInfo fileInfo = new FileInfo(inputFilePath);
                            string runNumber = fileInfo.Name.Replace("Input", "").Replace(".txt", "");
                            string input = File.ReadAllText(inputFilePath);
                            string expectedOutput = File.ReadAllText(Path.Join(graderDataFolder, $"Output{runNumber}.txt"));
                            string results = RunProgram(sourcePath, exeFile, input);
                            anyFailed |= !GradeOutput(runsStringBuilder, runNumber, expectedOutput, results);
                            string expFileName = Path.Join(runsFolder, $"{programToGrade}Expected{runNumber}.txt");
                            string actFileName = Path.Join(runsFolder, $"{programToGrade}Actual{runNumber}.txt");
                            File.WriteAllText(expFileName, expectedOutput);
                            File.WriteAllText(actFileName, results);
                            filesToZip.AddRange(new[] { expFileName, actFileName });
                        }

                        string filePrefix = $"{programToGrade}_{DateTime.Now:MMddyyyy-HH-mm-ss}";
                        string shortZipFileName = $"{filePrefix}.zip";
                        string zipFileName = Path.Combine(ExtractFolderName, shortZipFileName);
                        ZipFile.CreateFromDirectory(runsFolder, zipFileName);
                        Directory.Delete(runsFolder, true);

                        string finalResult = runsStringBuilder.ToString();
                        string shortFileName = $"{filePrefix}_GraderResult.txt";
                        string fileName = Path.Combine(ExtractFolderName, shortFileName);
                        File.WriteAllText(fileName, finalResult);

                        using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
                        using (FileStream zipFileStream = new FileStream(zipFileName, FileMode.Open))
                        {
                            await commandContext.RespondWithFilesAsync(new Dictionary<string, Stream>() { { shortFileName, fileStream }, { shortZipFileName, zipFileStream } },
                                anyFailed ? "You're gonna wanna check the files" : "Nice work");
                        }
                    }
                    else
                    {
                        var extractDir = new DirectoryInfo("extract");
                        string failString = buildStandard.Replace(extractDir.FullName, "");
                        DiscordEmbedBuilder discordEmbedBuilder = Config.Instance.GetDiscordEmbedBuilder()
                            .WithTitle("Failed to build");
                        if (failString.Length > 1500)
                        {
                            File.WriteAllText("Fail.txt", failString);
                            await commandContext.RespondWithFileAsync("Fail.txt", embed: discordEmbedBuilder.Build());
                        }
                        else
                        {
                            discordEmbedBuilder = discordEmbedBuilder.WithDescription(failString);
                            await commandContext.RespondAsync(embed: discordEmbedBuilder.Build());
                        }
                    }
                    ProcessStartInfo cleanStartInfo = new ProcessStartInfo("dotnet", "clean") { WorkingDirectory = ExtractFolderName };
                    RunProcess(cleanStartInfo, 5000);
                    Directory.CreateDirectory(runPath);
                    Directory.Move(ExtractFolderName, Path.Combine(runPath, $"{DateTime.Now:MMddyyyy-HH-mm-ss}{(final ? "-final" : "")}"));
                }
                else
                {
                    await commandContext.RespondAsync($"I am not yet ready to grade {programToGrade}!");
                }
            }
        }

        public static async Task GradeCPP(CommandContext commandContext, DiscordAttachment attachment, string currentDirectory, string programToGrade, bool final)
        {
            string runPath = Path.Combine(Config.Instance.GraderDump, $"{commandContext.User.Id}/CS1430/{programToGrade}");

            bool finalExists = Directory.Exists(runPath) && Directory.EnumerateDirectories(runPath, "*-final").Any();
            if (finalExists)
            {
                await commandContext.RespondAsync("Unable to grade, you've already submitted your final submission");
            }
            else
            {
                string graderDataFolder = Path.Join(currentDirectory, "GraderData", programToGrade);
                if (Directory.Exists(graderDataFolder))
                {
                    await DownloadAndExtractFiles(attachment);
                    string sourcePath = Path.Join(currentDirectory, ExtractFolderName);

                    List<string> sourceFiles = new List<string>();
                    sourceFiles.AddRange(Directory.EnumerateFiles(sourcePath, "*.cpp"));
                    sourceFiles.AddRange(Directory.EnumerateFiles(sourcePath, "*.h"));

                    string sources = string.Join(' ', sourceFiles.Select(a => $"\"{a}\""));
                    string exeFile = Path.Join(sourcePath, $"{programToGrade}.exe");
                    string argumentString = $"{sources} -o {exeFile} -static-libgcc -static-libstdc++";
                    try
                    {
                        ProcessStartInfo buildPSI = new ProcessStartInfo(@"C:\MinGW\bin\g++.exe", argumentString) { WorkingDirectory = @"C:\MinGW\bin\" };
                        var (buildStandard, buildError, buildTimeout) = RunProcess(buildPSI, 5000);
                        if (File.Exists(exeFile))
                        {
                            StringBuilder runsStringBuilder = new StringBuilder();
                            string fileGradeResults = GradeCPPFiles(sourceFiles);

                            if (!string.IsNullOrWhiteSpace(fileGradeResults))
                            {
                                runsStringBuilder.AppendLine("File Grade Results:");
                                runsStringBuilder.AppendLine(fileGradeResults);
                            }

                            await commandContext.RespondAsync("Build succeeded: Starting to test!");
                            List<string> filesToZip = new List<string>();
                            string runsFolder = Path.Join(ExtractFolderName, "Runs");
                            if (Directory.Exists(runsFolder))
                            {
                                Directory.Delete(runsFolder, true);
                            }
                            bool anyFailed = false;
                            Directory.CreateDirectory($"{ExtractFolderName}/Runs");
                            foreach (string inputFilePath in Directory.EnumerateFiles(graderDataFolder, "Input*").OrderBy(a => a))
                            {
                                FileInfo fileInfo = new FileInfo(inputFilePath);
                                string runNumber = fileInfo.Name.Replace("Input", "").Replace(".txt", "");
                                string input = File.ReadAllText(inputFilePath);
                                string expectedOutput = File.ReadAllText(Path.Join(graderDataFolder, $"Output{runNumber}.txt"));
                                try
                                {
                                    string results = RunProgram(sourcePath, exeFile, input);
                                    anyFailed |= !GradeOutput(runsStringBuilder, runNumber, expectedOutput, results);
                                    string expFileName = Path.Join(runsFolder, $"{programToGrade}Expected{runNumber}.txt");
                                    string actFileName = Path.Join(runsFolder, $"{programToGrade}Actual{runNumber}.txt");
                                    File.WriteAllText(expFileName, expectedOutput);
                                    File.WriteAllText(actFileName, results);
                                    filesToZip.AddRange(new[] { expFileName, actFileName });
                                }
                                catch (Exception exc)
                                {
                                    anyFailed = true;
                                    runsStringBuilder.AppendLine($"Exception thrown on Run {runNumber}:");
                                    runsStringBuilder.AppendLine(exc.Message);
                                }
                            }

                            string filePrefix = $"{programToGrade}_{DateTime.Now:MMddyyyy-HH-mm-ss}";
                            string shortZipFileName = $"{filePrefix}.zip";
                            string zipFileName = Path.Combine(ExtractFolderName, shortZipFileName);
                            ZipFile.CreateFromDirectory(runsFolder, zipFileName);
                            Directory.Delete(runsFolder, true);

                            string finalResult = runsStringBuilder.ToString();
                            string shortFileName = $"{filePrefix}_GraderResult.txt";
                            string fileName = Path.Combine(ExtractFolderName, shortFileName);
                            File.WriteAllText(fileName, finalResult);

                            using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
                            using (FileStream zipFileStream = new FileStream(zipFileName, FileMode.Open))
                            {
                                await commandContext.RespondWithFilesAsync(new Dictionary<string, Stream>() { { shortFileName, fileStream }, { shortZipFileName, zipFileStream } },
                                    anyFailed ? "You're gonna wanna check the files" : "Nice work");
                            }
                        }
                        else
                        {
                            var extractDir = new DirectoryInfo("extract");
                            string failString = buildError.Replace(extractDir.FullName, "");
                            DiscordEmbedBuilder discordEmbedBuilder = Config.Instance.GetDiscordEmbedBuilder()
                                .WithTitle("Failed to build");
                            if (failString.Length > 1500)
                            {
                                File.WriteAllText("Fail.txt", failString);
                                await commandContext.RespondWithFileAsync("Fail.txt", embed: discordEmbedBuilder.Build());
                            }
                            else
                            {
                                discordEmbedBuilder = discordEmbedBuilder.WithDescription(failString);
                                await commandContext.RespondAsync(embed: discordEmbedBuilder.Build());
                            }
                        }
                        Directory.CreateDirectory(runPath);
                        Directory.Move(ExtractFolderName, Path.Combine(runPath, $"{DateTime.Now:MMddyyyy-HH-mm-ss}{(final ? "-final" : "")}"));
                    }
                    catch (Exception e)
                    {
                        await commandContext.RespondAsync($"Failed to build: {e.Message}");
                    }
                }
                else
                {
                    await commandContext.RespondAsync($"I am not yet ready to grade {programToGrade}!");
                }
            }
        }

        private static bool GradeOutput(StringBuilder runsStringBuilder, string runNumber, string expectedOutput, string results)
        {
            if (results == expectedOutput)
            {
                runsStringBuilder.AppendLine($"Yay, no differences for run {runNumber}");
            }
            else
            {
                runsStringBuilder.AppendLine($"Run {runNumber}:");
                runsStringBuilder.AppendLine($"Expected Output:");
                runsStringBuilder.AppendLine("--------------");
                runsStringBuilder.AppendLine(expectedOutput);
                runsStringBuilder.AppendLine("--------------");
                runsStringBuilder.AppendLine($"Program Output");
                runsStringBuilder.AppendLine("--------------");
                runsStringBuilder.AppendLine(results);
                runsStringBuilder.AppendLine("--------------");
            }
            return results == expectedOutput;
        }

        private static string GradeCPPFiles(IEnumerable<string> sourceFiles)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string filePath in sourceFiles)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                List<int> linesThatAreTooLong = new List<int>();
                List<int> linesThatAreIncorrectlyIndented = new List<int>();
                List<int> linesThatHaveTabs = new List<int>();
                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int lineNumber = i + 1;
                    if (line.Length > 73)
                    {
                        linesThatAreTooLong.Add(lineNumber);
                    }
                    if (!string.IsNullOrWhiteSpace(line) && (line.Length - line.TrimStart(' ').Length) % 3 != 0)
                    {
                        linesThatAreIncorrectlyIndented.Add(lineNumber);
                    }
                    if (line.Contains('\t'))
                    {
                        linesThatHaveTabs.Add(lineNumber);
                    }
                }
                if (linesThatAreTooLong.Count > 0 || linesThatAreIncorrectlyIndented.Count > 0 || linesThatHaveTabs.Count > 0)
                {
                    stringBuilder.AppendLine(fileInfo.Name);
                    if (linesThatAreTooLong.Count > 0)
                    {
                        stringBuilder.AppendLine($"Too long: {string.Join(", ", linesThatAreTooLong)}");
                    }
                    if (linesThatAreIncorrectlyIndented.Count > 0)
                    {
                        stringBuilder.AppendLine($"Incorrect indentation: {string.Join(", ", linesThatAreIncorrectlyIndented)}");
                    }
                    if (linesThatHaveTabs.Count > 0)
                    {
                        stringBuilder.AppendLine($"Has tabs: {string.Join(", ", linesThatHaveTabs)}");
                    }
                }
            }
            return stringBuilder.ToString();
        }

        private static string GradeCSFiles(IEnumerable<string> sourceFiles)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string filePath in sourceFiles)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                List<int> linesThatAreTooLong = new List<int>();
                List<int> linesThatAreIncorrectlyIndented = new List<int>();
                List<int> linesThatHaveTabs = new List<int>();
                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int lineNumber = i + 1;
                    if (line.Length > 73)
                    {
                        linesThatAreTooLong.Add(lineNumber);
                    }
                    if (!string.IsNullOrWhiteSpace(line) && (line.Length - line.TrimStart(' ').Length) % 3 != 0)
                    {
                        linesThatAreIncorrectlyIndented.Add(lineNumber);
                    }
                    if (line.Contains('\t'))
                    {
                        linesThatHaveTabs.Add(lineNumber);
                    }
                }
                if (linesThatAreTooLong.Count > 0 || linesThatAreIncorrectlyIndented.Count > 0 || linesThatHaveTabs.Count > 0)
                {
                    stringBuilder.AppendLine(fileInfo.Name);
                    if (linesThatAreTooLong.Count > 0)
                    {
                        stringBuilder.AppendLine($"Too long: {string.Join(", ", linesThatAreTooLong)}");
                    }
                    if (linesThatAreIncorrectlyIndented.Count > 0)
                    {
                        stringBuilder.AppendLine($"Incorrect indentation: {string.Join(", ", linesThatAreIncorrectlyIndented)}");
                    }
                    if (linesThatHaveTabs.Count > 0)
                    {
                        stringBuilder.AppendLine($"Has tabs: {string.Join(", ", linesThatHaveTabs)}");
                    }
                }
            }
            return stringBuilder.ToString();
        }

        private const int MessageLengthLimit = 2048;

        private static string RunProgram(string workingDir, string exeFile, string input)
        {
            ProcessStartInfo runPSI = new ProcessStartInfo(exeFile) { WorkingDirectory = workingDir };

            var (standard, error, timeout) = RunProcess(runPSI, 1000, input);
            if (timeout)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Execution timed out:");
                stringBuilder.AppendLine(standard);
                stringBuilder.AppendLine(error);
                return stringBuilder.ToString();
            }
            else
            {
                return standard;
            }

        }

        private static async Task DownloadAndExtractFiles(DiscordAttachment attachment)
        {
            WebRequest webRequest = WebRequest.Create(attachment.Url);
            WebResponse response = await webRequest.GetResponseAsync();
            Stream dataStream = response.GetResponseStream();
            string programToGrade = attachment.FileName[0..^4];
            string fileName = attachment.FileName;
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            FileStream fileStream = File.Create(fileName);
            await dataStream.CopyToAsync(fileStream);
            fileStream.Close();
            if (Directory.Exists(ExtractFolderName))
            {
                Directory.Delete(ExtractFolderName, true);
            }
            if (attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(fileName, ExtractFolderName);
            }
            else
            {
                Directory.CreateDirectory(ExtractFolderName);
                File.Copy(fileName, $"{ExtractFolderName}\\{fileName}");
            }

            File.Delete(fileName);
        }

        private static (string standard, string error, bool timeout) RunProcess(ProcessStartInfo startInfo, int timeout, string input = "")
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.UseShellExecute = false;
            using AutoResetEvent standardReset = new AutoResetEvent(false);
            using AutoResetEvent errorReset = new AutoResetEvent(false);
            using Process process = new Process() { StartInfo = startInfo };
            StringBuilder standard = new StringBuilder();
            StringBuilder error = new StringBuilder();
            process.OutputDataReceived += (sender, e) => { if (e.Data == null) { standardReset.Set(); } else { standard.AppendLine(e.Data); } };
            process.ErrorDataReceived += (sender, e) => { if (e.Data == null) { errorReset.Set(); } else { error.AppendLine(e.Data); } };
            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.StandardInput.Write(input);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            if (process.WaitForExit(timeout) && errorReset.WaitOne(timeout) && standardReset.WaitOne(timeout))
            {
                return (standard.ToString(), error.ToString(), false);
            }
            else
            {
                process.Kill(true);
                return (standard.ToString(), error.ToString(), true);
            }
        }
    }
}
