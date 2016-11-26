///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using wasSharp.Web.Utilities;

namespace wasOpenMetaverse
{
    public static class Mesh
    {
        /// <summary>
        ///     Creates a faceted mesh from a primitive.
        /// </summary>
        /// <param name="Client">the client to use for meshing</param>
        /// <param name="primitive">the primitive to convert</param>
        /// <param name="mesher">the mesher to use</param>
        /// <param name="facetedMesh">a reference to an output facted mesh object</param>
        /// <param name="millisecondsTimeout">the services timeout</param>
        /// <returns>true if the mesh could be created successfully</returns>
        public static bool MakeFacetedMesh(GridClient Client, Primitive primitive, MeshmerizerR mesher,
            ref FacetedMesh facetedMesh,
            uint millisecondsTimeout)
        {
            if (primitive.Sculpt == null || primitive.Sculpt.SculptTexture.Equals(UUID.Zero))
            {
                facetedMesh = mesher.GenerateFacetedMesh(primitive, DetailLevel.Highest);
                return true;
            }
            if (!primitive.Sculpt.Type.Equals(SculptType.Mesh))
            {
                byte[] assetData = null;
                switch (!Client.Assets.Cache.HasAsset(primitive.Sculpt.SculptTexture))
                {
                    case true:
                        lock (Locks.ClientInstanceAssetsLock)
                        {
                            var ImageDownloadedEvent = new ManualResetEvent(false);
                            Client.Assets.RequestImage(primitive.Sculpt.SculptTexture, (state, args) =>
                            {
                                if (!state.Equals(TextureRequestState.Finished)) return;
                                assetData = args.AssetData;
                                ImageDownloadedEvent.Set();
                            });
                            if (!ImageDownloadedEvent.WaitOne((int) millisecondsTimeout, false))
                                return false;
                        }
                        Client.Assets.Cache.SaveAssetToCache(primitive.Sculpt.SculptTexture, assetData);
                        break;
                    default:
                        assetData = Client.Assets.Cache.GetCachedAssetBytes(primitive.Sculpt.SculptTexture);
                        break;
                }
                Image image;
                ManagedImage managedImage;
                switch (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                {
                    case true:
                        return false;
                    default:
                        if ((managedImage.Channels & ManagedImage.ImageChannels.Alpha) != 0)
                        {
                            managedImage.ConvertChannels(managedImage.Channels & ~ManagedImage.ImageChannels.Alpha);
                        }
                        image = LoadTGAClass.LoadTGA(new MemoryStream(managedImage.ExportTGA()));
                        break;
                }
                facetedMesh = mesher.GenerateFacetedSculptMesh(primitive, (Bitmap) image, DetailLevel.Highest);
                return true;
            }
            FacetedMesh localFacetedMesh = null;
            var MeshDownloadedEvent = new ManualResetEvent(false);
            lock (Locks.ClientInstanceAssetsLock)
            {
                Client.Assets.RequestMesh(primitive.Sculpt.SculptTexture, (success, meshAsset) =>
                {
                    FacetedMesh.TryDecodeFromAsset(primitive, meshAsset, DetailLevel.Highest, out localFacetedMesh);
                    MeshDownloadedEvent.Set();
                });

                if (!MeshDownloadedEvent.WaitOne((int) millisecondsTimeout, false))
                    return false;
            }

            switch (localFacetedMesh != null)
            {
                case true:
                    facetedMesh = localFacetedMesh;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Generates a Collada DAE XML Document.
        /// </summary>
        /// <param name="facetedMeshSet">the faceted meshes</param>
        /// <param name="textures">a dictionary of UUID to texture names</param>
        /// <param name="imageFormat">the image export format</param>
        /// <returns>the DAE document</returns>
        /// <remarks>
        ///     This function is a branch-in of several functions of the Radegast Viewer with some changes by Wizardry and
        ///     Steamworks.
        /// </remarks>
        public static XmlDocument GenerateCollada(IEnumerable<FacetedMesh> facetedMeshSet,
            Dictionary<UUID, string> textures, string imageFormat)
        {
            var AllMeterials = new List<MaterialInfo>();

            var Doc = new XmlDocument();
            var root = Doc.AppendChild(Doc.CreateElement("COLLADA"));
            root.Attributes.Append(Doc.CreateAttribute("xmlns")).Value = "http://www.collada.org/2005/11/COLLADASchema";
            root.Attributes.Append(Doc.CreateAttribute("version")).Value = "1.4.1";

            var asset = root.AppendChild(Doc.CreateElement("asset"));
            var contributor = asset.AppendChild(Doc.CreateElement("contributor"));
            contributor.AppendChild(Doc.CreateElement("author")).InnerText = "Radegast User";
            contributor.AppendChild(Doc.CreateElement("authoring_tool")).InnerText = "Radegast Collada Export";

            asset.AppendChild(Doc.CreateElement("created")).InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            asset.AppendChild(Doc.CreateElement("modified")).InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            var unit = asset.AppendChild(Doc.CreateElement("unit"));
            unit.Attributes.Append(Doc.CreateAttribute("name")).Value = "meter";
            unit.Attributes.Append(Doc.CreateAttribute("meter")).Value = "1";

            asset.AppendChild(Doc.CreateElement("up_axis")).InnerText = "Z_UP";

            var images = root.AppendChild(Doc.CreateElement("library_images"));
            var geomLib = root.AppendChild(Doc.CreateElement("library_geometries"));
            var effects = root.AppendChild(Doc.CreateElement("library_effects"));
            var materials = root.AppendChild(Doc.CreateElement("library_materials"));
            var scene = root.AppendChild(Doc.CreateElement("library_visual_scenes"))
                .AppendChild(Doc.CreateElement("visual_scene"));
            scene.Attributes.Append(Doc.CreateAttribute("id")).InnerText = "Scene";
            scene.Attributes.Append(Doc.CreateAttribute("name")).InnerText = "Scene";

            foreach (var name in textures.Values)
            {
                var colladaName = name + "_" + imageFormat.ToLower();
                var image = images.AppendChild(Doc.CreateElement("image"));
                image.Attributes.Append(Doc.CreateAttribute("id")).InnerText = colladaName;
                image.Attributes.Append(Doc.CreateAttribute("name")).InnerText = colladaName;
                image.AppendChild(Doc.CreateElement("init_from")).InnerText =
                    (name + "." + imageFormat.ToLower()).URIUnescapeDataString();
            }

            Func<XmlNode, string, string, List<float>, bool> addSource = (mesh, src_id, param, vals) =>
            {
                var source = mesh.AppendChild(Doc.CreateElement("source"));
                source.Attributes.Append(Doc.CreateAttribute("id")).InnerText = src_id;
                var src_array = source.AppendChild(Doc.CreateElement("float_array"));

                src_array.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}", src_id,
                    "array");
                src_array.Attributes.Append(Doc.CreateAttribute("count")).InnerText = vals.Count.ToString();

                var sb = new StringBuilder();
                for (var i = 0; i < vals.Count; i++)
                {
                    sb.Append(vals[i].ToString(Utils.EnUsCulture));
                    if (i != vals.Count - 1)
                    {
                        sb.Append(" ");
                    }
                }
                src_array.InnerText = sb.ToString();

                var acc = source.AppendChild(Doc.CreateElement("technique_common"))
                    .AppendChild(Doc.CreateElement("accessor"));
                acc.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}", src_id,
                    "array");
                acc.Attributes.Append(Doc.CreateAttribute("count")).InnerText = (vals.Count/param.Length).ToString();
                acc.Attributes.Append(Doc.CreateAttribute("stride")).InnerText = param.Length.ToString();

                foreach (var c in param)
                {
                    var pX = acc.AppendChild(Doc.CreateElement("param"));
                    pX.Attributes.Append(Doc.CreateAttribute("name")).InnerText = c.ToString();
                    pX.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "float";
                }

                return true;
            };

            Func<Primitive.TextureEntryFace, MaterialInfo> getMaterial = o =>
            {
                var ret = AllMeterials.FirstOrDefault(mat => mat.Matches(o));

                if (ret != null) return ret;
                ret = new MaterialInfo
                {
                    TextureID = o.TextureID,
                    Color = o.RGBA,
                    Name = string.Format("Material{0}", AllMeterials.Count)
                };
                AllMeterials.Add(ret);

                return ret;
            };

            Func<FacetedMesh, List<MaterialInfo>> getMaterials = o =>
            {
                var ret = new List<MaterialInfo>();

                for (var face_num = 0; face_num < o.Faces.Count; face_num++)
                {
                    var te = o.Faces[face_num].TextureFace;
                    if (te.RGBA.A < 0.01f)
                    {
                        continue;
                    }
                    var mat = getMaterial.Invoke(te);
                    if (!ret.Contains(mat))
                    {
                        ret.Add(mat);
                    }
                }
                return ret;
            };

            Func<XmlNode, string, string, FacetedMesh, List<int>, bool> addPolygons =
                (mesh, geomID, materialID, obj, faces_to_include) =>
                {
                    var polylist = mesh.AppendChild(Doc.CreateElement("polylist"));
                    polylist.Attributes.Append(Doc.CreateAttribute("material")).InnerText = materialID;

                    // Vertices semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "VERTEX";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "vertices");
                    }

                    // Normals semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "NORMAL";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "normals");
                    }

                    // UV semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "TEXCOORD";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "map0");
                    }

                    // Save indices
                    var vcount = polylist.AppendChild(Doc.CreateElement("vcount"));
                    var p = polylist.AppendChild(Doc.CreateElement("p"));
                    var index_offset = 0;
                    var num_tris = 0;
                    var pBuilder = new StringBuilder();
                    var vcountBuilder = new StringBuilder();

                    for (var face_num = 0; face_num < obj.Faces.Count; face_num++)
                    {
                        var face = obj.Faces[face_num];
                        if (faces_to_include == null || faces_to_include.Contains(face_num))
                        {
                            for (var i = 0; i < face.Indices.Count; i++)
                            {
                                var index = index_offset + face.Indices[i];
                                pBuilder.Append(index);
                                pBuilder.Append(" ");
                                if (i%3 == 0)
                                {
                                    vcountBuilder.Append("3 ");
                                    num_tris++;
                                }
                            }
                        }
                        index_offset += face.Vertices.Count;
                    }

                    p.InnerText = pBuilder.ToString().TrimEnd();
                    vcount.InnerText = vcountBuilder.ToString().TrimEnd();
                    polylist.Attributes.Append(Doc.CreateAttribute("count")).InnerText = num_tris.ToString();

                    return true;
                };

            Func<FacetedMesh, MaterialInfo, List<int>> getFacesWithMaterial = (obj, mat) =>
            {
                var ret = new List<int>();
                for (var face_num = 0; face_num < obj.Faces.Count; face_num++)
                {
                    if (mat == getMaterial.Invoke(obj.Faces[face_num].TextureFace))
                    {
                        ret.Add(face_num);
                    }
                }
                return ret;
            };

            Func<Vector3, Quaternion, Vector3, float[]> createSRTMatrix = (scale, q, pos) =>
            {
                var mat = new float[16];

                // Transpose the quaternion (don't ask me why)
                q.X = q.X*-1f;
                q.Y = q.Y*-1f;
                q.Z = q.Z*-1f;

                var x2 = q.X + q.X;
                var y2 = q.Y + q.Y;
                var z2 = q.Z + q.Z;
                var xx = q.X*x2;
                var xy = q.X*y2;
                var xz = q.X*z2;
                var yy = q.Y*y2;
                var yz = q.Y*z2;
                var zz = q.Z*z2;
                var wx = q.W*x2;
                var wy = q.W*y2;
                var wz = q.W*z2;

                mat[0] = (1.0f - (yy + zz))*scale.X;
                mat[1] = (xy - wz)*scale.X;
                mat[2] = (xz + wy)*scale.X;
                mat[3] = 0.0f;

                mat[4] = (xy + wz)*scale.Y;
                mat[5] = (1.0f - (xx + zz))*scale.Y;
                mat[6] = (yz - wx)*scale.Y;
                mat[7] = 0.0f;

                mat[8] = (xz - wy)*scale.Z;
                mat[9] = (yz + wx)*scale.Z;
                mat[10] = (1.0f - (xx + yy))*scale.Z;
                mat[11] = 0.0f;

                //Positional parts
                mat[12] = pos.X;
                mat[13] = pos.Y;
                mat[14] = pos.Z;
                mat[15] = 1.0f;

                return mat;
            };

            Func<XmlNode, bool> generateEffects = o =>
            {
                // Effects (face color, alpha)
                foreach (var mat in AllMeterials)
                {
                    var color = mat.Color;
                    var effect = effects.AppendChild(Doc.CreateElement("effect"));
                    effect.Attributes.Append(Doc.CreateAttribute("id")).InnerText = mat.Name + "-fx";
                    var profile = effect.AppendChild(Doc.CreateElement("profile_COMMON"));
                    string colladaName = null;

                    var kvp = textures.FirstOrDefault(p => p.Key.Equals(mat.TextureID));

                    if (!kvp.Equals(default(KeyValuePair<UUID, string>)))
                    {
                        var textID = kvp.Key;
                        colladaName = textures[textID] + "_" + imageFormat.ToLower();
                        var newparam = profile.AppendChild(Doc.CreateElement("newparam"));
                        newparam.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = colladaName + "-surface";
                        var surface = newparam.AppendChild(Doc.CreateElement("surface"));
                        surface.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "2D";
                        surface.AppendChild(Doc.CreateElement("init_from")).InnerText = colladaName;
                        newparam = profile.AppendChild(Doc.CreateElement("newparam"));
                        newparam.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = colladaName + "-sampler";
                        newparam.AppendChild(Doc.CreateElement("sampler2D"))
                            .AppendChild(Doc.CreateElement("source"))
                            .InnerText = colladaName + "-surface";
                    }

                    var t = profile.AppendChild(Doc.CreateElement("technique"));
                    t.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = "common";
                    var phong = t.AppendChild(Doc.CreateElement("phong"));

                    var diffuse = phong.AppendChild(Doc.CreateElement("diffuse"));
                    // Only one <color> or <texture> can appear inside diffuse element
                    if (colladaName != null)
                    {
                        var txtr = diffuse.AppendChild(Doc.CreateElement("texture"));
                        txtr.Attributes.Append(Doc.CreateAttribute("texture")).InnerText = colladaName + "-sampler";
                        txtr.Attributes.Append(Doc.CreateAttribute("texcoord")).InnerText = colladaName;
                    }
                    else
                    {
                        var diffuseColor = diffuse.AppendChild(Doc.CreateElement("color"));
                        diffuseColor.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = "diffuse";
                        diffuseColor.InnerText = string.Format("{0} {1} {2} {3}",
                            color.R.ToString(Utils.EnUsCulture),
                            color.G.ToString(Utils.EnUsCulture),
                            color.B.ToString(Utils.EnUsCulture),
                            color.A.ToString(Utils.EnUsCulture));
                    }

                    phong.AppendChild(Doc.CreateElement("transparency"))
                        .AppendChild(Doc.CreateElement("float"))
                        .InnerText = color.A.ToString(Utils.EnUsCulture);
                }

                return true;
            };

            var prim_nr = 0;
            foreach (var obj in facetedMeshSet)
            {
                var total_num_vertices = 0;
                var name = string.Format("prim{0}", prim_nr++);
                var geomID = name;

                var geom = geomLib.AppendChild(Doc.CreateElement("geometry"));
                geom.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}", geomID, "mesh");
                var mesh = geom.AppendChild(Doc.CreateElement("mesh"));

                var position_data = new List<float>();
                var normal_data = new List<float>();
                var uv_data = new List<float>();

                var num_faces = obj.Faces.Count;

                for (var face_num = 0; face_num < num_faces; face_num++)
                {
                    var face = obj.Faces[face_num];
                    total_num_vertices += face.Vertices.Count;

                    foreach (var v in face.Vertices)
                    {
                        position_data.Add(v.Position.X);
                        position_data.Add(v.Position.Y);
                        position_data.Add(v.Position.Z);

                        normal_data.Add(v.Normal.X);
                        normal_data.Add(v.Normal.Y);
                        normal_data.Add(v.Normal.Z);

                        uv_data.Add(v.TexCoord.X);
                        uv_data.Add(v.TexCoord.Y);
                    }
                }

                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "positions"), "XYZ", position_data);
                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "normals"), "XYZ", normal_data);
                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "map0"), "ST", uv_data);

                // Add the <vertices> element
                {
                    var verticesNode = mesh.AppendChild(Doc.CreateElement("vertices"));
                    verticesNode.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}",
                        geomID, "vertices");
                    var verticesInput = verticesNode.AppendChild(Doc.CreateElement("input"));
                    verticesInput.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "POSITION";
                    verticesInput.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format(
                        "#{0}-{1}", geomID, "positions");
                }

                var objMaterials = getMaterials.Invoke(obj);

                // Add triangles
                foreach (var objMaterial in objMaterials)
                {
                    addPolygons.Invoke(mesh, geomID, objMaterial.Name + "-material", obj,
                        getFacesWithMaterial.Invoke(obj, objMaterial));
                }

                var node = scene.AppendChild(Doc.CreateElement("node"));
                node.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "NODE";
                node.Attributes.Append(Doc.CreateAttribute("id")).InnerText = geomID;
                node.Attributes.Append(Doc.CreateAttribute("name")).InnerText = geomID;

                // Set tranform matrix (node position, rotation and scale)
                var matrix = node.AppendChild(Doc.CreateElement("matrix"));

                var srt = createSRTMatrix.Invoke(obj.Prim.Scale, obj.Prim.Rotation, obj.Prim.Position);
                var matrixVal = string.Empty;
                for (var i = 0; i < 4; i++)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        matrixVal += srt[j*4 + i].ToString(Utils.EnUsCulture) + " ";
                    }
                }
                matrix.InnerText = matrixVal.TrimEnd();

                // Geometry of the node
                var nodeGeometry = node.AppendChild(Doc.CreateElement("instance_geometry"));

                // Bind materials
                var tq = nodeGeometry.AppendChild(Doc.CreateElement("bind_material"))
                    .AppendChild(Doc.CreateElement("technique_common"));
                foreach (var objMaterial in objMaterials)
                {
                    var instanceMaterial = tq.AppendChild(Doc.CreateElement("instance_material"));
                    instanceMaterial.Attributes.Append(Doc.CreateAttribute("symbol")).InnerText =
                        string.Format("{0}-{1}", objMaterial.Name, "material");
                    instanceMaterial.Attributes.Append(Doc.CreateAttribute("target")).InnerText =
                        string.Format("#{0}-{1}", objMaterial.Name, "material");
                }

                nodeGeometry.Attributes.Append(Doc.CreateAttribute("url")).InnerText = string.Format("#{0}-{1}", geomID,
                    "mesh");
            }

            generateEffects.Invoke(effects);

            // Materials
            foreach (var objMaterial in AllMeterials)
            {
                var mat = materials.AppendChild(Doc.CreateElement("material"));
                mat.Attributes.Append(Doc.CreateAttribute("id")).InnerText = objMaterial.Name + "-material";
                var matEffect = mat.AppendChild(Doc.CreateElement("instance_effect"));
                matEffect.Attributes.Append(Doc.CreateAttribute("url")).InnerText = string.Format("#{0}-{1}",
                    objMaterial.Name, "fx");
            }

            root.AppendChild(Doc.CreateElement("scene"))
                .AppendChild(Doc.CreateElement("instance_visual_scene"))
                .Attributes.Append(Doc.CreateAttribute("url")).InnerText = "#Scene";

            return Doc;
        }
    }

    /// <summary>
    ///     Material information for Collada DAE Export.
    /// </summary>
    /// <remarks>This class is taken from the Radegast Viewer with changes by Wizardry and Steamworks.</remarks>
    public class MaterialInfo
    {
        public Color4 Color;
        public string Name;
        public UUID TextureID;

        public bool Matches(Primitive.TextureEntryFace TextureEntry)
        {
            return TextureID.Equals(TextureEntry.TextureID) && Color.Equals(TextureEntry.RGBA);
        }
    }
}