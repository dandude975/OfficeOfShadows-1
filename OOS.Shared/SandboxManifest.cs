using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OOS.Shared
{
    public enum ItemKind { File, Directory, Shortcut }

    public class ManifestItem
    {
        public string Path { get; set; } = "";
        public ItemKind Kind { get; set; } = ItemKind.File;
        public bool Required { get; set; } = true;

        // Optional integrity signals (files only)
        public string? Sha256 { get; set; }
        public long? Size { get; set; }

        // Optional local source (relative to EXE base) for repair
        public string? Source { get; set; }
    }

    public class SandboxManifest
    {
        public string Version { get; set; } = "0.0";
        public List<ManifestItem> Items { get; set; } = new();

        public static SandboxManifest Load(string absolutePath)
        {
            var json = File.ReadAllText(absolutePath);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            // allow string enum values like "File", "Directory", "Shortcut" (case-insensitive)
            opts.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));

            return JsonSerializer.Deserialize<SandboxManifest>(json, opts)
                   ?? new SandboxManifest();
        }

        public IEnumerable<Discrepancy> Validate(string sandboxRoot)
        {
            var results = new List<Discrepancy>();

            foreach (var item in Items)
            {
                var full = Path.Combine(sandboxRoot, item.Path);
                bool exists = item.Kind switch
                {
                    ItemKind.Directory => Directory.Exists(full),
                    _ => File.Exists(full)
                };

                if (!exists)
                {
                    if (item.Required)
                        results.Add(Discrepancy.Missing(item, full));
                    continue;
                }

                if (item.Kind == ItemKind.File)
                {
                    try
                    {
                        var fi = new FileInfo(full);
                        if (item.Size.HasValue && fi.Length != item.Size.Value)
                            results.Add(Discrepancy.SizeMismatch(item, full, fi.Length));

                        if (!string.IsNullOrWhiteSpace(item.Sha256))
                        {
                            var actual = HashUtil.Sha256(full);
                            if (!actual.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
                                results.Add(Discrepancy.HashMismatch(item, full, actual));
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(Discrepancy.Error(item, full, ex.Message));
                    }
                }
            }

            return results;
        }
    }

    public class Discrepancy
    {
        public ManifestItem Item { get; }
        public string FullPath { get; }
        public string Kind { get; }
        public string? Details { get; }

        private Discrepancy(ManifestItem item, string fullPath, string kind, string? details = null)
        {
            Item = item; FullPath = fullPath; Kind = kind; Details = details;
        }

        public static Discrepancy Missing(ManifestItem i, string p) => new(i, p, "Missing");
        public static Discrepancy SizeMismatch(ManifestItem i, string p, long actual) =>
            new(i, p, "SizeMismatch", $"Actual={actual}, Expected={i.Size}");
        public static Discrepancy HashMismatch(ManifestItem i, string p, string actual) =>
            new(i, p, "HashMismatch", $"Actual={actual}, Expected={i.Sha256}");
        public static Discrepancy Error(ManifestItem i, string p, string msg) =>
            new(i, p, "Error", msg);
    }

    public static class HashUtil
    {
        public static string Sha256(string path)
        {
            using var s = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(s);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
