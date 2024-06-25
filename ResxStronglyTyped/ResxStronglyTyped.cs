using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources.NetStandard;
using System.Text;
using System.Text.RegularExpressions;

namespace ResxStronglyTyped
{
    public class ResxStronglyTyped : Task
    {
        [Required]
        public string ProjectDirectory { get; set; }

        [Required]
        public string RootNamespace { get; set; }

        [Output]
        public ITaskItem[] ClassFilePaths { get; set; }

        [Output]
        public ITaskItem[] ResxFilePaths { get; set; }

        private const string _PatternToReplace = @"^[^a-zA-Z]|[ \u00A0\u005C\u005D.,;|~@#%$^&*+-/<>?[(){}""':!]";
        private const string _ReplacementChar = "_";

        private static string Normalize(string str)
        {
            return Regex.Replace(str, _PatternToReplace, _ReplacementChar);
        }
        private static string GetRelativeDirectoryPath(string fromPath, string toPath)
        {
            if (toPath.StartsWith(fromPath))
                return toPath.Substring(fromPath.Length).Trim(Path.DirectorySeparatorChar);
            return toPath;
        }
        private static IEnumerable<(string Key, string Value, string Comment)> GetResxFileEntries(string filePath)
        {
            using (var reader = new ResXResourceReader(filePath) { UseResXDataNodes = true })
            {
                return reader.Cast<DictionaryEntry>().Select(node =>
                {
                    var key = node.Key.ToString();
                    var value = ((ResXDataNode)node.Value).GetValue((ITypeResolutionService)null).ToString();
                    var comment = ((ResXDataNode)node.Value).Comment;
                    return (key, value, comment);
                });
            }
        }
        public override bool Execute()
        {
            try
            {
                var resxFilePaths = Directory.GetFiles(ProjectDirectory, "*.resx", SearchOption.AllDirectories);
                var resxFiles = resxFilePaths.Select(path =>
                {
                    string culture = null, className = null;
                    var fileParentDirectoryPath = Directory.GetParent(path).FullName;
                    var resxFileDirectoryRelativePath = GetRelativeDirectoryPath(ProjectDirectory, fileParentDirectoryPath);
                    var classRelativeNamespace = string.Join(".", resxFileDirectoryRelativePath.Split(Path.DirectorySeparatorChar).Select(d => Normalize(d)));
                    var classNamespace = RootNamespace + (!string.IsNullOrEmpty(classRelativeNamespace) ? $".{classRelativeNamespace}" : string.Empty);
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path).Split(Path.DirectorySeparatorChar).LastOrDefault();

                    if (fileNameWithoutExtension.IndexOf('.') >= 0)
                    {
                        try
                        {
                            var c = fileNameWithoutExtension.Split('.').Last();
                            System.Globalization.CultureInfo.GetCultureInfo(c);
                            culture = c;
                        }
                        catch (System.Globalization.CultureNotFoundException)
                        {
                        }
                    }
                    var fileNameWithoutExtensionAndCulture = fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - (string.IsNullOrEmpty(culture) ? 0 : $".{culture}".Length));
                    var classFilePath = $"{fileParentDirectoryPath}{Path.DirectorySeparatorChar}{fileNameWithoutExtensionAndCulture}.Designer.cs";
                    className = Normalize(fileNameWithoutExtensionAndCulture);
                    var entries = GetResxFileEntries(path);
                    return new { ResxFilePath = path, Culture = culture, ClassName = className, ClassFilePath = classFilePath, ClassNamespace = classNamespace, Entries = entries };
                });
                var classFiles = from resxFile in resxFiles
                                 group resxFile by resxFile.ClassFilePath into g
                                 select new
                                 {
                                     ClassFilePath = g.Key,
                                     ClassName = g.Select(x => x.ClassName).Max(),
                                     ClassNamespace = g.Select(x => x.ClassNamespace).Max(),
                                     Entries = g.SelectMany(x => x.Entries).GroupBy(x => x.Key).Select(e => new { e.Key, Comment = e.Select(x => x.Comment).Max() }).OrderBy(x => x.Key)
                                 };


                foreach (var file in classFiles)
                {
                    File.Delete(file.ClassFilePath);

                    StringBuilder content = new StringBuilder();
                    content.AppendLine($@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the ResxStronglyTyped tool.
//     Package Version:{Assembly.GetExecutingAssembly().GetName().Version}
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace {file.ClassNamespace}
{{
    using Microsoft.Extensions.Localization;
    using static ResxStronglyTyped.Extensions.LocalizationManager;
    public partial class {file.ClassName}
    {{
        public static IStringLocalizer Localizer => GetStringLocalizer<{file.ClassName}>();
        {string.Join(@"", file.Entries.Select(x => $@"
        /// <summary>
        /// {x.Comment}
        /// </summary>
        public static string {Normalize(x.Key)} => Localizer[""{x.Key}""];
        "))}
    }}
}}
");
                    File.WriteAllText(file.ClassFilePath, content.ToString());
                }
                ResxFilePaths = resxFilePaths.Select(path => new TaskItem(path)).ToArray();
                ClassFilePaths = classFiles.Select(classFile => new TaskItem(classFile.ClassFilePath)).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
            return true;
        }
    }
}