// Pack Engine sources (ADR-0002): compiled at the maintainer's pack time by
// RoslynCodeTaskFactory from AgentSkills.Sdk.targets, and by the unit-test
// project directly. Ships as source inside the SDK nupkg — never as a DLL,
// and never reaches a consumer's machine.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AgentSkills.Sdk.Tasks
{
    /// <summary>
    /// Frontmatter inputs for one Skill, already resolved (no defaults applied here).
    /// </summary>
    public sealed class SkillFrontmatter
    {
        /// <summary>Creates a frontmatter value set.</summary>
        public SkillFrontmatter(
            string name,
            string description,
            string packageId,
            string packageVersion,
            string? userInvocable,
            string? contextStrategy)
        {
            Name = name;
            Description = description;
            PackageId = packageId;
            PackageVersion = packageVersion;
            UserInvocable = userInvocable;
            ContextStrategy = contextStrategy;
        }

        /// <summary>Validated Skill Name (spec §3).</summary>
        public string Name { get; }

        /// <summary>Raw description; YAML quoting is applied at composition time.</summary>
        public string Description { get; }

        /// <summary>Original PackageId, unsanitized.</summary>
        public string PackageId { get; }

        /// <summary>Original package version, unsanitized (build metadata intact).</summary>
        public string PackageVersion { get; }

        /// <summary>Raw AgentSkillUserInvocable value, or null when unset.</summary>
        public string? UserInvocable { get; }

        /// <summary>Raw AgentSkillContextStrategy value, or null when unset.</summary>
        public string? ContextStrategy { get; }
    }

    /// <summary>
    /// Pure pack-time logic: naming, validation, YAML quoting and SKILL.md
    /// variant composition (spec §3–§4). No MSBuild types, fully unit-testable.
    /// </summary>
    public static class AgentSkillsCore
    {
        /// <summary>agentskills.io name length limit.</summary>
        public const int MaxSkillNameLength = 64;

        /// <summary>agentskills.io description length limit.</summary>
        public const int MaxDescriptionLength = 1024;

        /// <summary>
        /// Sanitize pipeline steps 1, 3 and 4 (spec §3): lowercase, collapse every
        /// run outside [a-z0-9] to a single hyphen, trim hyphens at both ends.
        /// </summary>
        public static string Sanitize(string value)
        {
            string lower = value.ToLowerInvariant();
            StringBuilder result = new StringBuilder(lower.Length);
            bool pendingHyphen = false;
            foreach (char c in lower)
            {
                bool alphanumeric = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (alphanumeric)
                {
                    if (pendingHyphen && result.Length > 0)
                    {
                        result.Append('-');
                    }
                    pendingHyphen = false;
                    result.Append(c);
                }
                else
                {
                    pendingHyphen = true;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Sanitize pipeline step 2 (spec §3): drop semver build metadata —
        /// <c>+</c> and everything after it.
        /// </summary>
        public static string StripBuildMetadata(string version)
        {
            int plus = version.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }

        /// <summary>
        /// Computes the Skill Name <c>use-&lt;sanitized-packageid&gt;-v&lt;sanitized-version&gt;</c>.
        /// The result is NOT length-validated here; callers raise AGSK001 when it
        /// exceeds <see cref="MaxSkillNameLength"/> (ADR-0009).
        /// </summary>
        public static string ComputeSkillName(string packageId, string packageVersion)
        {
            string id = Sanitize(StripBuildMetadata(packageId));
            string version = Sanitize(StripBuildMetadata(packageVersion));
            return "use-" + id + "-v" + version;
        }

        /// <summary>
        /// Derives the default Consumer Flag name (spec §5): PackageId with every
        /// non-alphanumeric stripped, plus "AgentSkills". MSBuild property names
        /// cannot contain dots.
        /// </summary>
        public static string DeriveConsumerFlagName(string packageId)
        {
            StringBuilder result = new StringBuilder(packageId.Length);
            foreach (char c in packageId)
            {
                bool alphanumeric = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (alphanumeric)
                {
                    result.Append(c);
                }
            }
            result.Append("AgentSkills");
            return result.ToString();
        }

        /// <summary>
        /// Full agentskills.io name rules: 1–64 chars of [a-z0-9-], no leading,
        /// trailing or consecutive hyphens. Used for AGSK002 (never rewrites).
        /// </summary>
        public static bool IsValidSkillName(string name)
        {
            if (name.Length < 1 || name.Length > MaxSkillNameLength)
            {
                return false;
            }
            char previous = '-'; // rejects a leading hyphen
            foreach (char c in name)
            {
                bool alphanumeric = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!alphanumeric && c != '-')
                {
                    return false;
                }
                if (c == '-' && previous == '-')
                {
                    return false;
                }
                previous = c;
            }
            return previous != '-'; // rejects a trailing hyphen
        }

        /// <summary>Default description when no override is given (spec §3).</summary>
        public static string DefaultDescription(string packageId)
        {
            return "Guidance and rules for integrating " + packageId + " APIs.";
        }

        /// <summary>
        /// YAML single-quoted scalar (spec §4): embedded newlines become spaces,
        /// single quotes are doubled, result is wrapped in single quotes.
        /// </summary>
        public static string QuoteYamlSingle(string value)
        {
            string flat = value
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("'", "''");
            return "'" + flat + "'";
        }

        /// <summary>
        /// Scaffolded Skill Body used when AgentSkillBodyFile is unset (spec §5
        /// step 2): heading from PackageId, the package description, and a pointer
        /// to the references/ bucket.
        /// </summary>
        public static string ScaffoldBody(string packageId, string description)
        {
            StringBuilder body = new StringBuilder();
            body.Append("# ").Append(packageId).Append(" guide\n\n");
            if (!string.IsNullOrWhiteSpace(description))
            {
                body.Append(description.Trim()).Append("\n\n");
            }
            body.Append("See the `references/` directory for detailed documentation.\n");
            return body.ToString();
        }

        /// <summary>
        /// claude Variant (ADR-0003): Claude Code extension keys live top-level so
        /// Claude Code actually honors them.
        /// </summary>
        public static string ComposeClaudeVariant(SkillFrontmatter frontmatter, string body)
        {
            StringBuilder skill = new StringBuilder();
            skill.Append("---\n");
            AppendIdentity(skill, frontmatter);
            if (!string.IsNullOrWhiteSpace(frontmatter.UserInvocable))
            {
                skill.Append("user-invocable: ").Append(frontmatter.UserInvocable!.Trim()).Append('\n');
            }
            if (!string.IsNullOrWhiteSpace(frontmatter.ContextStrategy))
            {
                skill.Append("context: ").Append(frontmatter.ContextStrategy!.Trim()).Append('\n');
            }
            skill.Append("---\n\n");
            skill.Append(NormalizeBody(body));
            return skill.ToString();
        }

        /// <summary>
        /// agents Variant (ADR-0003): spec-pure — extensions live under the
        /// <c>metadata:</c> string map, values double-quoted per the spec's
        /// "metadata values are strings" rule.
        /// </summary>
        public static string ComposeAgentsVariant(SkillFrontmatter frontmatter, string body)
        {
            StringBuilder skill = new StringBuilder();
            skill.Append("---\n");
            AppendIdentity(skill, frontmatter);
            skill.Append("metadata:\n");
            skill.Append("  package-id: ").Append(frontmatter.PackageId).Append('\n');
            skill.Append("  package-version: ").Append(frontmatter.PackageVersion).Append('\n');
            if (!string.IsNullOrWhiteSpace(frontmatter.UserInvocable))
            {
                skill.Append("  user-invocable: \"").Append(frontmatter.UserInvocable!.Trim()).Append("\"\n");
            }
            if (!string.IsNullOrWhiteSpace(frontmatter.ContextStrategy))
            {
                skill.Append("  context: \"").Append(frontmatter.ContextStrategy!.Trim()).Append("\"\n");
            }
            skill.Append("---\n\n");
            skill.Append(NormalizeBody(body));
            return skill.ToString();
        }

        private static void AppendIdentity(StringBuilder skill, SkillFrontmatter frontmatter)
        {
            skill.Append("name: ").Append(frontmatter.Name).Append('\n');
            skill.Append("description: ").Append(QuoteYamlSingle(frontmatter.Description)).Append('\n');
        }

        private static string NormalizeBody(string body)
        {
            string normalized = body.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
            return normalized + "\n";
        }
    }

    /// <summary>
    /// The Pack Engine (ADR-0002): runs at the maintainer's pack time, per the
    /// first target framework only (spec §5). Validates identity, composes both
    /// SKILL.md Variants, renders the Consumer Targets, and returns every file
    /// to embed in the nupkg as items with a PackagePath metadata value.
    /// </summary>
    public sealed class AgentSkillsGeneratePackTask : Task
    {
        /// <summary>Package identity; validated upstream by NuGet.</summary>
        [Required]
        public string PackageId { get; set; } = "";

        /// <summary>Full package version (may carry build metadata).</summary>
        [Required]
        public string PackageVersion { get; set; } = "";

        /// <summary>Directory where generated files are staged (spec §5 step 5).</summary>
        [Required]
        public string StagingDirectory { get; set; } = "";

        /// <summary>Project directory; anchors relative maintainer paths.</summary>
        [Required]
        public string ProjectDirectory { get; set; } = "";

        /// <summary>Consumer Flag property name (derived or overridden).</summary>
        [Required]
        public string ConsumerFlagName { get; set; } = "";

        /// <summary>The maintainer's &lt;Description&gt;; used only for the scaffolded body.</summary>
        public string PackageDescription { get; set; } = "";

        /// <summary>AgentSkillNameOverride (AGSK002 when invalid).</summary>
        public string NameOverride { get; set; } = "";

        /// <summary>AgentSkillDescriptionOverride (AGSK004 when over limit).</summary>
        public string DescriptionOverride { get; set; } = "";

        /// <summary>AgentSkillBodyFile (AGSK003 when set but missing).</summary>
        public string BodyFile { get; set; } = "";

        /// <summary>AgentSkillFullFile (AGSK005 when set but missing, AGSK006 with BodyFile).</summary>
        public string FullFile { get; set; } = "";

        /// <summary>AgentSkillUserInvocable raw value.</summary>
        public string UserInvocable { get; set; } = "";

        /// <summary>AgentSkillContextStrategy raw value.</summary>
        public string ContextStrategy { get; set; } = "";

        /// <summary>PackageReadmeFile; added as references/README.md unless taken (spec §2).</summary>
        public string ReadmeFile { get; set; } = "";

        /// <summary>@(AgentSkillReferenceFiles); RecursiveDir structure preserved.</summary>
        public ITaskItem[] ReferenceFiles { get; set; } = new ITaskItem[0];

        /// <summary>@(AgentSkillAssetFiles).</summary>
        public ITaskItem[] AssetFiles { get; set; } = new ITaskItem[0];

        /// <summary>@(AgentSkillScriptFiles).</summary>
        public ITaskItem[] ScriptFiles { get; set; } = new ITaskItem[0];

        /// <summary>Every file to pack, with PackagePath metadata (spec §4 layout).</summary>
        [Output]
        public ITaskItem[] PackageFiles { get; set; } = new ITaskItem[0];

        /// <inheritdoc />
        public override bool Execute()
        {
            if (!string.IsNullOrWhiteSpace(FullFile) && !string.IsNullOrWhiteSpace(BodyFile))
            {
                LogAgskError("AGSK006", "AgentSkillFullFile and AgentSkillBodyFile are mutually exclusive; set only one.");
                return false;
            }

            string skillName;
            if (!string.IsNullOrWhiteSpace(NameOverride))
            {
                skillName = NameOverride.Trim();
                if (!AgentSkillsCore.IsValidSkillName(skillName))
                {
                    LogAgskError("AGSK002", $"AgentSkillNameOverride '{skillName}' violates agentskills.io name rules (1-64 chars, [a-z0-9-], no leading/trailing/consecutive hyphens).");
                    return false;
                }
            }
            else
            {
                skillName = AgentSkillsCore.ComputeSkillName(PackageId, PackageVersion);
                if (skillName.Length > AgentSkillsCore.MaxSkillNameLength)
                {
                    LogAgskError("AGSK001", $"Computed Skill Name '{skillName}' is {skillName.Length} chars (max {AgentSkillsCore.MaxSkillNameLength}); set AgentSkillNameOverride.");
                    return false;
                }
            }

            string description = string.IsNullOrWhiteSpace(DescriptionOverride)
                ? AgentSkillsCore.DefaultDescription(PackageId)
                : DescriptionOverride.Trim();
            if (description.Length > AgentSkillsCore.MaxDescriptionLength)
            {
                LogAgskError("AGSK004", $"Skill description is {description.Length} chars (max {AgentSkillsCore.MaxDescriptionLength}).");
                return false;
            }

            string claudeVariant;
            string agentsVariant;
            if (!string.IsNullOrWhiteSpace(FullFile))
            {
                string fullPath = ResolvePath(FullFile);
                if (!File.Exists(fullPath))
                {
                    LogAgskError("AGSK005", $"AgentSkillFullFile '{fullPath}' not found.");
                    return false;
                }
                // Blind passthrough (spec §2): maintainer owns frontmatter correctness.
                string full = File.ReadAllText(fullPath);
                claudeVariant = full;
                agentsVariant = full;
            }
            else
            {
                string body;
                if (!string.IsNullOrWhiteSpace(BodyFile))
                {
                    string bodyPath = ResolvePath(BodyFile);
                    if (!File.Exists(bodyPath))
                    {
                        LogAgskError("AGSK003", $"AgentSkillBodyFile '{bodyPath}' not found.");
                        return false;
                    }
                    body = File.ReadAllText(bodyPath);
                }
                else
                {
                    body = AgentSkillsCore.ScaffoldBody(PackageId, PackageDescription);
                }

                SkillFrontmatter frontmatter = new SkillFrontmatter(
                    skillName,
                    description,
                    PackageId,
                    PackageVersion,
                    string.IsNullOrWhiteSpace(UserInvocable) ? null : UserInvocable,
                    string.IsNullOrWhiteSpace(ContextStrategy) ? null : ContextStrategy);
                claudeVariant = AgentSkillsCore.ComposeClaudeVariant(frontmatter, body);
                agentsVariant = AgentSkillsCore.ComposeAgentsVariant(frontmatter, body);
            }

            Directory.CreateDirectory(StagingDirectory);
            string claudePath = Path.Combine(StagingDirectory, "SKILL.claude.md");
            string agentsPath = Path.Combine(StagingDirectory, "SKILL.agents.md");
            string targetsPath = Path.Combine(StagingDirectory, PackageId + ".targets");
            // gitignore and stamp travel as packed files so the consumer side
            // needs only Copy (which has Retries); WriteLinesToFile races on
            // concurrent first syncs. Extension-bearing, non-dot names: NuGet treats an
            // extension-less PackagePath as a directory, and extraction is not
            // reliable for dot-prefixed entries — Copy renames at destination.
            string gitignorePath = Path.Combine(StagingDirectory, "skill.gitignore");
            string stampPath = Path.Combine(StagingDirectory, "skill.stamp");
            File.WriteAllText(claudePath, claudeVariant);
            File.WriteAllText(agentsPath, agentsVariant);
            File.WriteAllText(targetsPath, ConsumerTargetsGenerator.Render(
                PackageId, PackageVersion, skillName, ConsumerFlagName));
            File.WriteAllText(gitignorePath, "*\n");
            File.WriteAllText(stampPath, PackageVersion + "\n");

            List<ITaskItem> files = new List<ITaskItem>();
            files.Add(PackageFile(claudePath, "agent-assets/SKILL.claude.md"));
            files.Add(PackageFile(agentsPath, "agent-assets/SKILL.agents.md"));
            files.Add(PackageFile(gitignorePath, "agent-assets/skill.gitignore"));
            files.Add(PackageFile(stampPath, "agent-assets/skill.stamp"));
            files.Add(PackageFile(targetsPath, "build/" + PackageId + ".targets"));

            HashSet<string> payloadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPayload(files, payloadPaths, ReferenceFiles, "references");
            AddPayload(files, payloadPaths, AssetFiles, "assets");
            AddPayload(files, payloadPaths, ScriptFiles, "scripts");

            // PackageReadmeFile joins references/ unless the maintainer already
            // put a file at that exact payload path (spec §2 implicit behavior).
            if (!string.IsNullOrWhiteSpace(ReadmeFile))
            {
                string readmePath = ResolvePath(ReadmeFile);
                string readmeDestination = "agent-assets/payload/references/README.md";
                if (File.Exists(readmePath) && !payloadPaths.Contains(readmeDestination))
                {
                    files.Add(PackageFile(readmePath, readmeDestination));
                }
            }

            PackageFiles = files.ToArray();
            return true;
        }

        private void AddPayload(List<ITaskItem> files, HashSet<string> payloadPaths, ITaskItem[] items, string bucket)
        {
            foreach (ITaskItem item in items)
            {
                string recursiveDir = item.GetMetadata("RecursiveDir").Replace('\\', '/');
                string fileName = item.GetMetadata("Filename") + item.GetMetadata("Extension");
                string packagePath = "agent-assets/payload/" + bucket + "/" + recursiveDir + fileName;
                if (payloadPaths.Add(packagePath))
                {
                    files.Add(PackageFile(item.GetMetadata("FullPath"), packagePath));
                }
            }
        }

        private static ITaskItem PackageFile(string sourcePath, string packagePath)
        {
            TaskItem item = new TaskItem(sourcePath);
            item.SetMetadata("PackagePath", packagePath);
            return item;
        }

        private string ResolvePath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.Combine(ProjectDirectory, path);
        }

        private void LogAgskError(string code, string message)
        {
            Log.LogError(null, code, null, null, 0, 0, 0, 0, message);
        }
    }

    /// <summary>
    /// Renders the Consumer Targets (spec §6): the only thing that executes on
    /// a consumer's machine. Pure declarative MSBuild — every consumer-time
    /// <c>$(...)</c> below is literal text in a C# string, so the historical
    /// pack-time-expansion bug cannot occur (ADR-0002).
    /// </summary>
    public static class ConsumerTargetsGenerator
    {
        /// <summary>
        /// Agent Tokens routed to <c>.agents/skills/</c> with the agents Variant
        /// (ADR-0005, vercel-labs/skills --agent ids). claude-code is special-cased
        /// to <c>.claude/skills/</c>; tokens containing a path separator are custom
        /// destinations; anything else warns AGSK101.
        /// </summary>
        public static readonly string[] AgentsFamilyTokens =
        {
            "universal", "opencode", "codex", "cursor", "gemini-cli",
            "github-copilot", "amp", "cline", "zed", "warp",
        };

        /// <summary>
        /// Suffix appended to every generated property/item/target name so that
        /// several packages' Consumer Targets coexist in one consumer project.
        /// MSBuild property names allow no hyphens, hence the underscores.
        /// </summary>
        public static string NameTag(string packageId, string packageVersion)
        {
            string safeId = AgentSkillsCore.Sanitize(AgentSkillsCore.StripBuildMetadata(packageId)).Replace('-', '_');
            string safeVersion = AgentSkillsCore.Sanitize(AgentSkillsCore.StripBuildMetadata(packageVersion)).Replace('-', '_');
            return safeId + "_" + safeVersion;
        }

        /// <summary>Renders the generated build/[PackageId].targets content.</summary>
        public static string Render(string packageId, string packageVersion, string skillName, string consumerFlagName)
        {
            string tag = NameTag(packageId, packageVersion);
            string escapedId = XmlEscape(packageId);

            StringBuilder agentsCondition = new StringBuilder();
            foreach (string token in AgentsFamilyTokens)
            {
                if (agentsCondition.Length > 0)
                {
                    agentsCondition.Append(" Or ");
                }
                agentsCondition.Append("'@(_AgsTokensNorm_").Append(tag)
                    .Append("->AnyHaveMetadataValue('Identity', '").Append(token).Append("'))' == 'true'");
            }

            StringBuilder unknownCondition = new StringBuilder();
            foreach (string token in AgentsFamilyTokens)
            {
                unknownCondition.Append("'%(Identity)' != '").Append(token).Append("' And ");
            }
            unknownCondition.Append("'%(Identity)' != 'claude-code'");
            unknownCondition.Append(" And !$([System.String]::Copy('%(Identity)').Contains('/'))");
            unknownCondition.Append(" And !$([System.String]::Copy('%(Identity)').Contains('\\'))");

            string template = @"<Project>
  <!-- Generated by AgentSkills.Sdk for {{COMMENTID}} {{COMMENTVERSION}}. Do not edit.
       Syncs the packaged agent skill into the consumer workspace when the
       Consumer Flag $({{FLAG}}) opts in. Everything here evaluates on the
       consumer's machine; nothing was expanded at pack time except the
       {{TAG}} name suffix, the skill name and the token map. -->

  <Target Name=""AgentSkillsSync_{{TAG}}""
          BeforeTargets=""BeforeBuild""
          Condition=""'$({{FLAG}})' != '' AND '$(DesignTimeBuild)' != 'true'"">

    <!-- 1. Workspace Root (ADR-0007): AgentSkillsRoot → SolutionDir → .git walk-up → project dir. -->
    <PropertyGroup>
      <_AgsRoot_{{TAG}} Condition=""'$(AgentSkillsRoot)' != ''"">$([MSBuild]::EnsureTrailingSlash('$(AgentSkillsRoot)'))</_AgsRoot_{{TAG}}>
      <_AgsRoot_{{TAG}} Condition=""'$(_AgsRoot_{{TAG}})' == '' AND '$(SolutionDir)' != '' AND '$(SolutionDir)' != '*Undefined*'"">$([MSBuild]::EnsureTrailingSlash('$(SolutionDir)'))</_AgsRoot_{{TAG}}>
{{WALKUP}}      <_AgsFallback_{{TAG}} Condition=""'$(_AgsRoot_{{TAG}})' == ''"">true</_AgsFallback_{{TAG}}>
      <_AgsRoot_{{TAG}} Condition=""'$(_AgsRoot_{{TAG}})' == ''"">$(MSBuildProjectDirectory)/</_AgsRoot_{{TAG}}>
    </PropertyGroup>
    <Warning Code=""AGSK102"" Condition=""'$(_AgsFallback_{{TAG}})' == 'true'""
             Text=""[{{ID}}] No AgentSkillsRoot, SolutionDir or .git found; skills placed next to the project."" />

    <!-- 2. Agent Token → destination map (ADR-0005): exact match, case-insensitive,
         duplicates collapse; tokens with a path separator are custom destinations. -->
    <ItemGroup>
      <_AgsTokensRaw_{{TAG}} Include=""$({{FLAG}})"" />
      <!-- Lowercased per item: a property-function result is escaped, so
           $({{FLAG}}.ToLowerInvariant()) would not split on semicolons. -->
      <_AgsTokensNorm_{{TAG}} Include=""$([System.String]::Copy('%(_AgsTokensRaw_{{TAG}}.Identity)').ToLowerInvariant())"" />
      <_AgsDest_{{TAG}} Include="".agents/skills"" Variant=""agents""
                Condition=""{{AGENTSCOND}}"" />
      <_AgsDest_{{TAG}} Include="".claude/skills"" Variant=""claude""
                Condition=""'@(_AgsTokensNorm_{{TAG}}->AnyHaveMetadataValue('Identity', 'claude-code'))' == 'true'"" />
      <_AgsDest_{{TAG}} Include=""%(_AgsTokensRaw_{{TAG}}.Identity)"" Variant=""agents""
                Condition=""$([System.String]::Copy('%(_AgsTokensRaw_{{TAG}}.Identity)').Contains('/')) Or $([System.String]::Copy('%(_AgsTokensRaw_{{TAG}}.Identity)').Contains('\'))"" />
    </ItemGroup>
    <Warning Code=""AGSK101""
             Text=""[{{ID}}] Unknown Agent Token '%(_AgsTokensNorm_{{TAG}}.Identity)' skipped.""
             Condition=""{{UNKNOWNCOND}}"" />

    <!-- 3. Per-destination sync (ADR-0008): no-op once the Version Stamp exists —
         the directory name is version-unique and its content immutable. The stamp
         is written last so an interrupted sync re-runs.
         The batched includes below run once per destination (stamp checked per
         destination) and expand the full payload into per-destination copy items;
         a single cross-batched Copy would put _AgsDest and payload metadata in
         disjoint buckets and copy nothing. -->
    <ItemGroup>
      <_AgsPayload_{{TAG}} Include=""$(MSBuildThisFileDirectory)../agent-assets/payload/**/*"" />
      <_AgsCopy_{{TAG}} Include=""@(_AgsPayload_{{TAG}})"" AgsDest=""%(_AgsDest_{{TAG}}.Identity)""
          Condition=""!Exists('$(_AgsRoot_{{TAG}})%(_AgsDest_{{TAG}}.Identity)/{{SKILLNAME}}/.agentskills-stamp')"" />
      <_AgsSkill_{{TAG}} Include=""$(MSBuildThisFileDirectory)../agent-assets/SKILL.%(_AgsDest_{{TAG}}.Variant).md"" AgsDest=""%(_AgsDest_{{TAG}}.Identity)""
          Condition=""!Exists('$(_AgsRoot_{{TAG}})%(_AgsDest_{{TAG}}.Identity)/{{SKILLNAME}}/.agentskills-stamp')"" />
      <!-- The synced directory gitignores itself. Both markers ship as packed
           files and arrive via Copy: unlike WriteLinesToFile, Copy retries and
           skips identical content, which makes concurrent first syncs safe. -->
      <_AgsGitignore_{{TAG}} Include=""$(MSBuildThisFileDirectory)../agent-assets/skill.gitignore"" AgsDest=""%(_AgsDest_{{TAG}}.Identity)""
          Condition=""!Exists('$(_AgsRoot_{{TAG}})%(_AgsDest_{{TAG}}.Identity)/{{SKILLNAME}}/.agentskills-stamp')"" />
      <_AgsStamp_{{TAG}} Include=""$(MSBuildThisFileDirectory)../agent-assets/skill.stamp"" AgsDest=""%(_AgsDest_{{TAG}}.Identity)""
          Condition=""!Exists('$(_AgsRoot_{{TAG}})%(_AgsDest_{{TAG}}.Identity)/{{SKILLNAME}}/.agentskills-stamp')"" />
    </ItemGroup>

    <Copy SourceFiles=""@(_AgsCopy_{{TAG}})""
          DestinationFiles=""@(_AgsCopy_{{TAG}}->'$(_AgsRoot_{{TAG}})%(AgsDest)/{{SKILLNAME}}/%(RecursiveDir)%(Filename)%(Extension)')""
          SkipUnchangedFiles=""true"" Retries=""3"" RetryDelayMilliseconds=""200""
          Condition=""'@(_AgsDest_{{TAG}})' != '' AND '@(_AgsCopy_{{TAG}})' != ''"" />

    <Copy SourceFiles=""@(_AgsSkill_{{TAG}})""
          DestinationFiles=""@(_AgsSkill_{{TAG}}->'$(_AgsRoot_{{TAG}})%(AgsDest)/{{SKILLNAME}}/SKILL.md')""
          SkipUnchangedFiles=""true"" Retries=""3"" RetryDelayMilliseconds=""200""
          Condition=""'@(_AgsDest_{{TAG}})' != '' AND '@(_AgsSkill_{{TAG}})' != ''"" />

    <Copy SourceFiles=""@(_AgsGitignore_{{TAG}})""
          DestinationFiles=""@(_AgsGitignore_{{TAG}}->'$(_AgsRoot_{{TAG}})%(AgsDest)/{{SKILLNAME}}/.gitignore')""
          SkipUnchangedFiles=""true"" Retries=""3"" RetryDelayMilliseconds=""200""
          Condition=""'@(_AgsDest_{{TAG}})' != '' AND '@(_AgsGitignore_{{TAG}})' != ''"" />

    <!-- Stamp lands last so an interrupted sync re-runs (ADR-0008). -->
    <Copy SourceFiles=""@(_AgsStamp_{{TAG}})""
          DestinationFiles=""@(_AgsStamp_{{TAG}}->'$(_AgsRoot_{{TAG}})%(AgsDest)/{{SKILLNAME}}/.agentskills-stamp')""
          SkipUnchangedFiles=""true"" Retries=""3"" RetryDelayMilliseconds=""200""
          Condition=""'@(_AgsDest_{{TAG}})' != '' AND '@(_AgsStamp_{{TAG}})' != ''"" />

  </Target>
</Project>
";

            StringBuilder walkUp = new StringBuilder();
            for (int level = 0; level < 8; level++)
            {
                StringBuilder relative = new StringBuilder("$(MSBuildProjectDirectory)/");
                for (int i = 0; i < level; i++)
                {
                    relative.Append("../");
                }
                walkUp.Append("      <_AgsRoot_").Append(tag)
                    .Append(" Condition=\"'$(_AgsRoot_").Append(tag).Append(")' == '' AND Exists('")
                    .Append(relative).Append(".git')\">").Append(relative)
                    .Append("</_AgsRoot_").Append(tag).Append(">\n");
            }

            return template
                .Replace("\r\n", "\n")
                .Replace("{{WALKUP}}", walkUp.ToString())
                .Replace("{{AGENTSCOND}}", agentsCondition.ToString())
                .Replace("{{UNKNOWNCOND}}", unknownCondition.ToString())
                .Replace("{{COMMENTID}}", CommentSafe(packageId))
                .Replace("{{COMMENTVERSION}}", CommentSafe(packageVersion))
                .Replace("{{ID}}", escapedId)
                .Replace("{{VERSION}}", XmlEscape(packageVersion))
                .Replace("{{FLAG}}", consumerFlagName)
                .Replace("{{SKILLNAME}}", skillName)
                .Replace("{{TAG}}", tag);
        }

        private static string CommentSafe(string value)
        {
            // XML comments cannot contain "--" or end with "-".
            string safe = value;
            while (safe.Contains("--"))
            {
                safe = safe.Replace("--", "-");
            }
            return safe.TrimEnd('-');
        }

        private static string XmlEscape(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
