﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Data;

using Rhino.Geometry;
using System.Windows.Forms;
using System.Linq;
using Rhino.DocObjects;
using GrasshopperPRT.Properties;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.
namespace GrasshopperPRT
{

    public class GrasshopperPRTComponent : GH_Component, IGH_VariableParameterComponent
    {
        const int DEFAULT_INPUT_PARAM_COUNT = 2;
        const string RPK_INPUT_NAME = "Path to RPK";
        const string GEOM_INPUT_NAME = "Initial Shapes";
        const string GEOM_OUTPUT_NAME = "Generated Shapes";
        const string REPORTS_OUTPUT_NAME = "Reports";

        /// Stores the optional input parameters
        RuleAttribute[] mRuleAttributes;
        List<IGH_Param> mParams;

        //List<IGH_Param> mReportOutputs;

        string mCurrentRPK = "";

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public GrasshopperPRTComponent()
          : base("GrasshopperPRT", "GHPRT", "Version: " + PRTWrapper.GetVersion() + ". " +
              "Provide access to the CityEngine PRT engine in Grasshopper.",
              "Special", "Esri")
        {
            // Initialize PRT engine
            bool status = PRTWrapper.InitializeRhinoPRT();
            if (!status) throw new Exception("Fatal Error: PRT initialization failed.");

            mRuleAttributes = new RuleAttribute[0];
            mParams = new List<IGH_Param>();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // The default parameters are a rpk package and a set of geometries.
            pManager.AddTextParameter(RPK_INPUT_NAME, "RPK",
                "The path to a runtime package containing the rules to execute on the input geometry.",
                GH_ParamAccess.item);
            pManager.AddGeometryParameter(GEOM_INPUT_NAME, "Shape",
                "The initial geometry on which to execute the rules.",
                GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // The default output is the generated geometries.
            pManager.AddGeometryParameter(GEOM_OUTPUT_NAME, "o_shape",
                "The geometry generated by the rule set.",
                GH_ParamAccess.tree);

            pManager.AddGenericParameter("Materials", "M", "The cga materials for preview", GH_ParamAccess.tree);
            pManager.AddGenericParameter(REPORTS_OUTPUT_NAME, REPORTS_OUTPUT_NAME,
                "The cga reports. Each branch of the datatree contains the reports for a single initial shape.", 
                GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        { 
            // Get default inputs

            // RPK path is a single item.
            string rpk_file = "";
            if (!DA.GetData(RPK_INPUT_NAME, ref rpk_file)) { return; }
            if (rpk_file.Length == 0) { return; }

            // Once we have a rpk file, directly extract the rule attributes
            PRTWrapper.SetPackage(rpk_file);

            // Update the rule attributes only if the rpk is changed.
            if (mCurrentRPK != rpk_file)
            {
                mCurrentRPK = rpk_file;

                //if rule attributes input parameters are already existing, remove them.
                if(mRuleAttributes.Length > 0)
                {
                    foreach(var param in mParams)
                    {
                        Params.UnregisterInputParameter(param);
                    }

                    mParams.Clear();
                }

                mRuleAttributes = PRTWrapper.GetRuleAttributes();
                foreach (RuleAttribute attrib in mRuleAttributes)
                {
                    CreateInputParameter(attrib);
                }

                Params.OnParametersChanged();
                ExpireSolution(true);
                return;
            }

            PRTWrapper.ClearInitialShapes();

            // Get the initial shape inputs
            GH_Structure<IGH_GeometricGoo> shapeTree = null;
            if (!DA.GetDataTree<IGH_GeometricGoo>(GEOM_INPUT_NAME, out shapeTree)) { return; }

            // Transform each geometry to a mesh
            List<Mesh> meshes = new List<Mesh>();

            int initShapeIdx = 0;
            foreach(IGH_GeometricGoo geom in shapeTree.AllData(true))
            {
                Mesh mesh = convertToMesh(geom);

                if (mesh != null)
                {
                    mesh.SetUserString(PRTWrapper.INIT_SHAPE_IDX_KEY, initShapeIdx.ToString());
                    meshes.Add(mesh);
                }
                initShapeIdx++;
            }

            // No compatible mesh was given
            if (meshes.Count == 0) return;

            if(!PRTWrapper.AddMesh(meshes)) return;

            // Get all node input corresponding to the list of mRuleAttributes registered.
            fillAttributesFromNode(DA);

            var generatedMeshes = PRTWrapper.GenerateMesh();

            GH_Structure<GH_Material> materials = PRTWrapper.GetAllMaterialIds(generatedMeshes.DataCount);

            // Set cga report values to output
            OutputReports(DA, generatedMeshes);

            DA.SetDataTree(0, generatedMeshes);
            DA.SetDataTree(1, materials);
        }

        private void OutputReports(IGH_DataAccess DA, GH_Structure<GH_Mesh> gh_meshes)
        {
            GH_Structure<ReportAttribute> outputTree = new GH_Structure<ReportAttribute>();
            
            int count = gh_meshes.DataCount;
            for(int meshID = 0; meshID < count; ++meshID)
            {
                var reports = PRTWrapper.GetAllReports(meshID);

                // The new branch
                GH_Path path = new GH_Path(meshID);
                reports.ForEach(x => outputTree.Append(x, path));
            }

            DA.SetDataTree(2, outputTree);
        }

        /// <summary>
        /// Input object types supported are: GH_Mesh, GH_Brep, GH_Rectangle, GH_Surface, GH_Box, GH_Plane.
        /// </summary>
        /// <param name="shape">An initial shape</param>
        /// <returns>The shape converted to a Mesh</returns>
        private Mesh convertToMesh(IGH_GeometricGoo shape)
        {
            bool status = true;
            Mesh mesh = null;

            // Cast the shape to its actual Rhino.Geometry type.
            IGH_GeometricGoo geoGoo = shape; // copy

            if(geoGoo is GH_Mesh)
            {
                GH_Mesh m = geoGoo as GH_Mesh;
                if(!GH_Convert.ToMesh(m, ref mesh, GH_Conversion.Both)) return null;
            }
            else if (geoGoo is GH_Brep)
            {
                GH_Brep brep = geoGoo as GH_Brep;
                Brep brepShape = null;
                if(!GH_Convert.ToBrep(brep, ref brepShape, GH_Conversion.Both)) return null;

                mesh = new Mesh();
                mesh.Append(Mesh.CreateFromBrep(brepShape, MeshingParameters.DefaultAnalysisMesh));
                mesh.Compact();
            }
            else if (geoGoo is GH_Rectangle)
            {
                Rectangle3d rect = Rectangle3d.Unset;
                status = GH_Convert.ToRectangle3d(geoGoo as GH_Rectangle, ref rect, GH_Conversion.Both);

                if (!status) return null;

                mesh = Mesh.CreateFromClosedPolyline(rect.ToPolyline());
            }
            else if (geoGoo is GH_Surface)
            {
                Surface surf = null;
                if(!GH_Convert.ToSurface(geoGoo as GH_Surface, ref surf, GH_Conversion.Both)) return null;
                mesh = Mesh.CreateFromSurface(surf, MeshingParameters.QualityRenderMesh);
            }
            else if (geoGoo is GH_Box)
            {
                if(!GH_Convert.ToMesh(geoGoo as GH_Box, ref mesh, GH_Conversion.Both)) return null;
            }
            else if(geoGoo is GH_Plane)
            {
                if (!GH_Convert.ToMesh(geoGoo as GH_Plane, ref mesh, GH_Conversion.Both)) return null;
            }
            else
            {
                return null;
            }

            mesh.Vertices.UseDoublePrecisionVertices = true;
            mesh.Faces.ConvertTrianglesToQuads(Rhino.RhinoMath.ToRadians(2), .875);

            return mesh;
        }

        /// <summary>
        /// Add rule attributes inputs to the grasshopper component.
        /// </summary>
        /// <param name="attrib">A rule attribute to add as input</param>
        private void CreateInputParameter(RuleAttribute attrib)
        {
            var parameter = attrib.GetInputParameter();
            mParams.Add(parameter);

            // Check if the param already exists to avoid adding duplicates.
            if(Params.IndexOfInputParam(parameter.Name) == -1)
                Params.RegisterInputParam(parameter);
        }

        private void fillAttributesFromNode(IGH_DataAccess DA)
        {
            for (int idx = 0; idx < mRuleAttributes.Length; ++idx)
            {
                RuleAttribute attrib = mRuleAttributes[idx];

                switch (attrib.mAttribType)
                {
                    case AnnotationArgumentType.AAT_FLOAT:
                        {
                            GH_Number value = new GH_Number(0.0);
                            if (!DA.GetData<GH_Number>(attrib.mFullName, ref value)) continue;
                            PRTWrapper.SetRuleAttributeDouble(attrib.mRuleFile, attrib.mFullName, value.Value);
                            break;
                        }
                    case AnnotationArgumentType.AAT_BOOL:
                        {
                            GH_Boolean boolean = new GH_Boolean();
                            if (!DA.GetData<GH_Boolean>(attrib.mFullName, ref boolean)) continue;
                            PRTWrapper.SetRuleAttributeBoolean(attrib.mRuleFile, attrib.mFullName, boolean.Value);
                            break;
                        }
                    case AnnotationArgumentType.AAT_INT:
                        {
                            GH_Integer integer = null;
                            if (!DA.GetData<GH_Integer>(attrib.mFullName, ref integer)) continue;
                            PRTWrapper.SetRuleAttributeInteger(attrib.mRuleFile, attrib.mFullName, integer.Value);
                            break;
                        }
                    case AnnotationArgumentType.AAT_STR:
                        {
                            string text = null;
                            if (attrib.mAnnotations.Any(x => x.IsColor()))
                            {
                                GH_Colour color = null;
                                if (!DA.GetData<GH_Colour>(attrib.mFullName, ref color)) continue;
                                text = Utils.hexColor(color);
                            }
                            else
                            {
                                GH_String gH_String = null;
                                if (!DA.GetData<GH_String>(attrib.mFullName, ref gH_String)) continue;
                                text = gH_String.Value;
                            }

                            PRTWrapper.SetRuleAttributeString(attrib.mRuleFile, attrib.mFullName, text);
                            break;
                        }
                    case AnnotationArgumentType.AAT_FLOAT_ARRAY:
                        {
                            List<double> doubleList = new List<double>();
                            if (!DA.GetDataList(attrib.mFullName, doubleList)) continue;
                            PRTWrapper.SetRuleAttributeDoubleArray(attrib.mRuleFile, attrib.mFullName, doubleList);
                            break;
                        }
                    case AnnotationArgumentType.AAT_BOOL_ARRAY:
                        {
                            List<Boolean> boolList = new List<Boolean>();
                            if (!DA.GetDataList(attrib.mFullName, boolList)) continue;
                            PRTWrapper.SetRuleAttributeBoolArray(attrib.mRuleFile, attrib.mFullName, boolList);
                            break;
                        }
                    case AnnotationArgumentType.AAT_STR_ARRAY:
                        {
                            List<string> stringList = new List<string>();
                            if (!DA.GetDataList(attrib.mFullName, stringList)) continue;
                            PRTWrapper.SetRuleAttributeStringArray(attrib.mRuleFile, attrib.mFullName, stringList);
                            break;
                        }
                    default:
                        continue;
                }
            }

        }

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return false;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            return null;
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return false;
        }

        public void VariableParameterMaintenance()
        {
            return;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                return Resources.gh_prt_main_component;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("ad54a111-cdbc-4417-bddd-c2195c9482d8"); }
        }
    }
}
