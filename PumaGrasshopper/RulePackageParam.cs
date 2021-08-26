﻿using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PumaGrasshopper.Properties;
using System.IO;

namespace PumaGrasshopper
{
    public class RulePackageParam : GH_PersistentParam<GH_String>
    {
        public RulePackageParam()
            : base("Rule Package", "RPK", "References a Rule Package (RPK). (Version " + PRTWrapper.GetVersion() + ")",
                ComponentLibraryInfo.MainCategory, ComponentLibraryInfo.PumaSubCategory)
        { }

        public override GH_Exposure Exposure { get; }

        public override Guid ComponentGuid
        {
            get { return new Guid("ed287f2d-82c2-417d-9fc4-5b1a5c268032"); }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Resources.gh_prt_rpk_parameter;
            }
        }

        protected override void Menu_AppendPromptMore(ToolStripDropDown menu)
        {
        }

        protected override void Menu_AppendManageCollection(ToolStripDropDown menu)
        {
        }

        protected override GH_GetterResult Prompt_Singular(ref GH_String value)
        {
            GH_Document doc = OnPingDocument();
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unable to access current document.");
                return GH_GetterResult.cancel;
            }

            if (doc.FilePath == null)
            {
                Rhino.UI.Dialogs.ShowMessage("Puma needs to save the current document first...", "Relative Rule Package Path");
                GH_DocumentIO docIO = new GH_DocumentIO(doc);
                if (!docIO.Save())
                    return GH_GetterResult.cancel;
            }

            var fd = new Rhino.UI.OpenFileDialog { 
                MultiSelect = false,
                Filter = "Rule Package (*.rpk)|*.rpk"
            };

            if (fd.ShowOpenDialog())
            {
                string relPath = GetRelativeRulePackagePath(doc, fd.FileName);
                if (relPath == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to determine relative path to document.");
                    return GH_GetterResult.cancel;
                }

                value = new GH_String(relPath);
                return GH_GetterResult.success;
            }
            return GH_GetterResult.cancel;
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_String> value)
        {
            return GH_GetterResult.cancel;
        }

        public static string GetRelativeRulePackagePath(GH_Document doc, String absRulePackagePath)
        {
            return MakeRelativePath(doc.FilePath, absRulePackagePath);
        }

        public static string GetAbsoluteRulePackagePath(GH_Document doc, String relRulePackagePath)
        {
            string docFilePath = doc.FilePath;
            string docDirPath = Path.GetDirectoryName(docFilePath);
            return Path.Combine(docDirPath, relRulePackagePath);
        }

        private static String MakeRelativePath(String fromPath, String toPath)
        {
            if (String.IsNullOrEmpty(fromPath)) return null;
            if (String.IsNullOrEmpty(toPath)) return null;

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }
    }
}
