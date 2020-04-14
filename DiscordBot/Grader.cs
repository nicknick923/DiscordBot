using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace DiscordBot
{
    public class Grader
    {
        private const string ExtractFolderName = "extract";

        public static async Task Grade(CommandContext commandContext, DiscordAttachment attachment, string currentDirectory)
        {
            string programToGrade = attachment.FileName.Replace(".zip", "", StringComparison.OrdinalIgnoreCase);
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
                    Process buildProcess = BuildProgram(argumentString);
                    if (File.Exists(exeFile))
                    {
                        StringBuilder runsStringBuilder = new StringBuilder();
                        string fileGradeResults = GradeFiles(sourceFiles);

                        if (!string.IsNullOrWhiteSpace(fileGradeResults))
                        {
                            runsStringBuilder.AppendLine("File Grade Results:");
                            runsStringBuilder.AppendLine(fileGradeResults);
                        }

                        const string splitter = "---";
                        await commandContext.RespondAsync("Build succeeded: Starting to test!");
                        foreach (string inputFilePath in Directory.EnumerateFiles(graderDataFolder, "Input*").OrderBy(a => a))
                        {
                            FileInfo fileInfo = new FileInfo(inputFilePath);
                            string runNumber = fileInfo.Name.Replace("Input", "").Replace(".txt", "");
                            string input = File.ReadAllText(inputFilePath);
                            string expectedOutput = File.ReadAllText(Path.Join(graderDataFolder, $"Output{runNumber}.txt"));
                            try
                            {
                                string results = RunProgram(sourcePath, exeFile, input);

                                if (results == expectedOutput)
                                {
                                    runsStringBuilder.AppendLine($"Yay, no differences for run {runNumber}");
                                }
                                else
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine($"Run {runNumber}:");
                                    sb.AppendLine($"Expected Output:");
                                    sb.AppendLine("--------------");
                                    sb.AppendLine(expectedOutput);
                                    sb.AppendLine("--------------");
                                    sb.AppendLine($"Program Output");
                                    sb.AppendLine("--------------");
                                    sb.AppendLine(results);
                                    sb.AppendLine("--------------");
                                    runsStringBuilder.AppendLine(sb.ToString());
                                }
                                runsStringBuilder.AppendLine(splitter);
                            }
                            catch (Exception exc)
                            {
                                runsStringBuilder.AppendLine($"Exception thrown on Run {runNumber}:");
                                runsStringBuilder.AppendLine(exc.Message);
                            }
                        }

                        string finalResult = runsStringBuilder.ToString();
                        File.WriteAllText(Path.Combine(ExtractFolderName, "GraderResult.txt"), finalResult);
                        foreach (DiscordEmbed embed in SliceAndDice(finalResult))
                        {
                            await commandContext.RespondAsync(embed: embed);
                        }
                    }
                    else
                    {
                        DiscordEmbedBuilder discordEmbedBuilder = new DiscordEmbedBuilder()
                            .WithTitle("Failed to build")
                            .WithDescription(buildProcess.StandardError.ReadToEnd());
                        await commandContext.RespondAsync(embed: discordEmbedBuilder.Build());
                    }

                    Directory.Move(ExtractFolderName, $"{commandContext.User.Id}-{programToGrade}-{DateTime.Now:MMddyyyy-hh-mm-ss tt}");
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

        private static string GradeFiles(List<string> sourceFiles)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string filePath in sourceFiles)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                List<int> linesThatAreTooLong = new List<int>();
                List<int> linesThatAreIncorrectlyIndented = new List<int>();
                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int lineNumber = i + 1;
                    if (line.Length > 74)
                    {
                        linesThatAreTooLong.Add(lineNumber);
                    }
                    if (!string.IsNullOrWhiteSpace(line) && (line.Length - line.TrimStart(' ').Length) % 3 != 0)
                    {
                        linesThatAreIncorrectlyIndented.Add(lineNumber);
                    }
                }
                if (linesThatAreTooLong.Count > 0 || linesThatAreIncorrectlyIndented.Count > 0)
                {
                    stringBuilder.AppendLine(fileInfo.Name);
                    if (linesThatAreTooLong.Count > 0)
                    {
                        stringBuilder.AppendLine($"Too long: {string.Join(", ", linesThatAreTooLong)}");
                    }
                    if (linesThatAreIncorrectlyIndented.Count > 0)
                    {
                        stringBuilder.AppendLine($"Incorrect Indentation: {string.Join(", ", linesThatAreIncorrectlyIndented)}");
                    }
                }
            }
            return stringBuilder.ToString();
        }

        private const int MessageLengthLimit = 2048;

        private static List<DiscordEmbed> SliceAndDice(string runString)
        {
            List<DiscordEmbed> responses = new List<DiscordEmbed>();
            List<string> responseStrings = runString.SplitByLength(MessageLengthLimit).ToList();
            int count = responseStrings.Count;
            for (int i = 0; i < count; i++)
            {
                responses.Add(new DiscordEmbedBuilder()
                    .WithTitle($"Run Results ({i + 1}/{count})")
                    .WithDescription(responseStrings[i])
                    .Build());
            }
            return responses;
        }

        private static string RunProgram(string sourcePath, string exeFile, string input)
        {
            ProcessStartInfo runPSI = new ProcessStartInfo(exeFile)
            {
                WorkingDirectory = sourcePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            Process runProcess = new Process()
            {
                StartInfo = runPSI
            };

            runProcess.Start();
            runProcess.StandardInput.Write($"{input}\0");

            if (!runProcess.WaitForExit(500))
            {
                runProcess.Kill();
                throw new Exception("Execution timed out");
            }

            string results = runProcess.StandardOutput.ReadToEnd();
            return results;
        }

        private static Process BuildProgram(string argumentString)
        {
            ProcessStartInfo buildPSI = new ProcessStartInfo(@"C:\MinGW\bin\g++.exe", argumentString)
            {
                WorkingDirectory = @"C:\MinGW\bin\",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process buildProcess = new Process()
            {
                StartInfo = buildPSI
            };
            buildProcess.Start();
            buildProcess.WaitForExit();
            return buildProcess;
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
            ZipFile.ExtractToDirectory(fileName, ExtractFolderName);
            File.Delete(fileName);
        }
    }
}
