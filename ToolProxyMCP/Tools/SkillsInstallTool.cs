using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace ToolProxy.Tools
{
    [McpServerToolType]
    public class SkillsInstallTool
    {
        private readonly ILogger<SkillsInstallTool> _logger;

        public SkillsInstallTool(ILogger<SkillsInstallTool> logger)
        {
            _logger = logger;
        }

        [McpServerTool, Description(
            "Install ToolProxy's bundled Agent Skills into the host's skills directory. " +
            "Pass the full skills directory path (skills_root). For Claude Code project-local " +
            "install — the recommended default — that's `<project_root>/.claude/skills`. " +
            "Each toolproxy-* skill is copied unconditionally, overwriting any existing files. " +
            "If skills_root had to be created, the response includes a one-shot warning that the " +
            "user should restart their session before live skill reload starts watching the directory.")]
        public Task<string> InstallSkillsAsync(
            [Description("Absolute path to the skills directory (e.g. /path/to/project/.claude/skills). The proxy does not append .claude/skills — pass the full skills root.")] string skills_root,
            CancellationToken cancellationToken = default)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            try
            {
                if (string.IsNullOrWhiteSpace(skills_root))
                {
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        error = "skills_root must be a non-empty absolute path."
                    }, jsonOptions));
                }

                if (!Path.IsPathFullyQualified(skills_root))
                {
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        error = $"skills_root must be an absolute path. Got: '{skills_root}'."
                    }, jsonOptions));
                }

                var sourceRoot = Path.Combine(AppContext.BaseDirectory, "skills");
                if (!Directory.Exists(sourceRoot))
                {
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        error = $"ToolProxy skills source directory not found at '{sourceRoot}'. The proxy build is missing the skills/ payload."
                    }, jsonOptions));
                }

                var createdSkillsRoot = !Directory.Exists(skills_root);
                if (createdSkillsRoot)
                {
                    Directory.CreateDirectory(skills_root);
                    _logger.LogInformation("Created skills_root '{SkillsRoot}'", skills_root);
                }

                var results = new List<object>();
                foreach (var skillDir in Directory.EnumerateDirectories(sourceRoot))
                {
                    var skillName = Path.GetFileName(skillDir);
                    var skillManifest = Path.Combine(skillDir, "SKILL.md");
                    if (!File.Exists(skillManifest))
                    {
                        _logger.LogDebug("Skipping '{SkillDir}': no SKILL.md", skillDir);
                        continue;
                    }

                    var destDir = Path.Combine(skills_root, skillName);
                    try
                    {
                        CopyDirectory(skillDir, destDir);
                        results.Add(new
                        {
                            name = skillName,
                            path = destDir,
                            status = "installed"
                        });
                        _logger.LogInformation("Installed skill '{SkillName}' to '{DestDir}'", skillName, destDir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to install skill '{SkillName}'", skillName);
                        results.Add(new
                        {
                            name = skillName,
                            path = destDir,
                            status = "error",
                            error = ex.Message
                        });
                    }
                }

                var response = new
                {
                    skills_root,
                    created_skills_root = createdSkillsRoot,
                    warning = createdSkillsRoot
                        ? "skills_root did not exist and was just created. On Claude Code, restart the session once so live skill reload starts watching this directory. Subsequent installs will be picked up without restart."
                        : null,
                    results
                };

                return Task.FromResult(JsonSerializer.Serialize(response, jsonOptions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing skills to '{SkillsRoot}'", skills_root);
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    error = $"Failed to install skills: {ex.Message}"
                }, jsonOptions));
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.EnumerateFiles(source))
            {
                var destFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var subDir in Directory.EnumerateDirectories(source))
            {
                var destSubDir = Path.Combine(destination, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
