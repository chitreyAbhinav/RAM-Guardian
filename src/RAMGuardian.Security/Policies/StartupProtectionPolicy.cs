using System;
using System.Collections.Generic;

namespace RAMGuardian.Security.Policies
{
    public class StartupProtectionPolicy
    {
        // Entries explicitly approved by user preferences for safe, reversible startup disabling
        private static readonly HashSet<string> ApprovedOptimizationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OneDrive",
            "utweb",
            "Spotify",
            "CloudflareWARP"
        };

        // Entries strictly protected from modification
        private static readonly HashSet<string> StrictlyProtectedStartupEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SecurityHealth",
            "ShortcutSwap.ahk",
            "ShortcutSwap.ahk - Shortcut",
            "WindowButtonDisable.ahk",
            "WindowButtonDisable.ahk - Shortcut",
            "IDMan",
            "Filee"
        };

        public bool IsApprovedForOptimization(string entryName, string command, out string reason)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                reason = "Unknown startup entry (Default-DENY: Protected)";
                return false;
            }

            string clean = entryName.Trim();

            // 1. Check if strictly protected
            if (StrictlyProtectedStartupEntries.Contains(clean) ||
                clean.StartsWith("MicrosoftEdgeAutoLaunch", StringComparison.OrdinalIgnoreCase) ||
                clean.IndexOf(".ahk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Protected entry: AutoHotkey script, Microsoft Defender, Edge session, or Filee.";
                return false;
            }

            // 2. Check if explicitly approved
            if (ApprovedOptimizationTargets.Contains(clean))
            {
                reason = "Approved for safe, reversible startup optimization per user preference.";
                return true;
            }

            // 3. Telegram: preserved by default unless individually requested
            if (clean.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Preserved per user preference (Telegram available on demand).";
                return false;
            }

            // 4. Default: DENY unknown entries
            reason = "Unrecognized startup entry (Default-DENY policy: Untouched).";
            return false;
        }

        public bool IsEntryProtected(string entryName)
        {
            string reason;
            return !IsApprovedForOptimization(entryName, null, out reason);
        }
    }
}
