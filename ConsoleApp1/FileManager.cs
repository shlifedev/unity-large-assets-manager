﻿using System.Text.RegularExpressions;

namespace ConsoleApp1;

public class FileManager
{
    private readonly string path;
    private readonly string filePattern;
    public FileSystemNode root = null; 
    public Regex KeywordsRegex { get; set; }
    
    int totalFileCount = 0;
    int progressedFileCount = 0;

    
    public FileManager(string path, string filePattern)
    {
        this.path = path;
        this.filePattern = filePattern;
        
        
        Console.WriteLine("Regex 컴파일 중 입니다.");
        KeywordsRegex = new Regex(
            @"(shader[s]|3d|2d|stylized|animation[s]|rpg|medieval|lowpoly|poly|paint|terrain|texture|material|pixel|vfx|sfx|sound[s]|gui|ui|sword|bow|building|house|town|addon|prop|plant|animal|tool|sound|particle)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    }

    public async Task Initialize()
    { 
        Console.WriteLine("파일 분석중입니다.");
        Func<FileSystemNode, Task> modifyFile = node =>
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(node.Name);

            MatchCollection matches = KeywordsRegex.Matches(fileNameWithoutExtension); // 1. 최적화된 Regex 사용
            if (matches.Count > 0)
            {
                var matchedKeywords = matches.Cast<Match>().Select(match => match.Value).ToList();
                node.Tags = matchedKeywords;
                // Console.WriteLine($"'{node.Name}' has matching tags! {string.Join(", ", matchedKeywords)}");
                // Console.WriteLine($"Tags for '{node.Name}': {string.Join(", ", node.Tags)}");
            }

            progressedFileCount++;

            Console.WriteLine($"{progressedFileCount}");
            return Task.CompletedTask;
        }; 
        root = await TraverseTreeAsync(path, filePattern, modifyFile); 
    }
    public void FindFilesByName(FileSystemNode node, string targetFileName, List<string> matchingFiles)
    {
        if (node.Name.ToLowerInvariant().Contains(targetFileName.ToLowerInvariant()) &&
            Path.GetExtension(node.Name) == ".unitypackage")
        {
            matchingFiles.Add(node.FullPath);
        }

        foreach (var childNode in node.Children)
        {
            FindFilesByName(childNode, targetFileName, matchingFiles);
        }
    }


    public void FindFilesByTag(FileSystemNode node, string targetTags, List<string> resultFiles)
    {
        if (node == null)
            return;

        var targetTagArray = targetTags.Split(',').Select(tag => tag.Trim().ToLowerInvariant()).ToList();

        if (node.Tags != null &&
            targetTagArray.All(targetTag => node.Tags.Contains(targetTag, StringComparer.OrdinalIgnoreCase)) &&
            Path.GetExtension(node.FullPath).Contains("unitypackage"))
        {
            resultFiles.Add(node.Name);
        }

        foreach (var childNode in node.Children)
        {
            FindFilesByTag(childNode, targetTags, resultFiles);
        }
    }

 
    protected async Task<FileSystemNode> TraverseTreeAsync(string root, string regexPattern,
        Func<FileSystemNode, Task> fileModifier = null)
    {
        var node = new FileSystemNode { Name = Path.GetFileName(root), FullPath = root };
        var attr = File.GetAttributes(root);
        var regex = new Regex(regexPattern);

        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
        {
            node.NodeType = NodeType.Directory;

            var dirTasks = Directory.GetDirectories(root)
                .Select(dir => TraverseTreeAsync(dir, regexPattern, fileModifier));

            var fileTasks = Directory.GetFiles(root)
                .Where(file => regex.IsMatch(file))
                .Select(async file =>
                {
                    var fileNode = new FileSystemNode
                        { Name = Path.GetFileName(file), NodeType = NodeType.File, FullPath = file };
                    if (fileModifier != null)
                    {
                        await fileModifier(fileNode);
                    }

                    return fileNode;
                });

            var children = await Task.WhenAll(dirTasks.Concat(fileTasks));
            node.Children.AddRange(children);
        }
        else
        {
            node.NodeType = NodeType.File;
        }

        if (fileModifier != null)
        {
            await fileModifier(node);
        }

        return node;
    }
}