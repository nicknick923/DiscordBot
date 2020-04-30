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

        public static async Task Grade(CommandContext commandContext, DiscordAttachment attachment, string currentDirectory, string programToGrade, bool final)
        {
            bool finalExists = Directory.EnumerateDirectories(Config.Instance.GraderDump, $"{commandContext.User.Id}-{programToGrade}-*-final").Any();
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
                            string failString = buildProcess.StandardError.ReadToEnd().Replace(extractDir.FullName, "");
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

                        Directory.Move(ExtractFolderName, Path.Combine(Config.Instance.GraderDump, $"{commandContext.User.Id}-{programToGrade}-{DateTime.Now:MMddyyyy-HH-mm-ss}{(final ? "-final" : "")}"));
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

        private static string GradeFiles(List<string> sourceFiles)
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

        private static string RunProgram(string sourcePath, string exeFile, string input)
        {
            ProcessStartInfo runPSI = new ProcessStartInfo(exeFile)
            {
                WorkingDirectory = sourcePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process runProcess = new Process()
            {
                StartInfo = runPSI
            };
            runProcess.Start();
            runProcess.StandardInput.Write(input);
            runProcess.StandardInput.Close();
            if (!runProcess.WaitForExit(1000))
            {
                runProcess.Kill();
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Execution timed out:");
                stringBuilder.AppendLine(runProcess.StandardOutput.ReadToEnd());
                stringBuilder.AppendLine(runProcess.StandardError.ReadToEnd());
                throw new Exception(stringBuilder.ToString());
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
            if (!buildProcess.WaitForExit(2500))
            {
                buildProcess.Kill();
            }
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
    }
}
