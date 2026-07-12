// Pack Engine sources (ADR-0002): compiled at the maintainer's pack time by
// RoslynCodeTaskFactory from AgentSkills.Sdk.targets, and by the unit-test
// project directly. Ships as source inside the SDK nupkg — never as a DLL,
// and never reaches a consumer's machine.
#nullable enable

using System;
using System.Text;

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
}
