/*!
 * X-UniTMX: A tiled map editor file importer for Unity3d
 * https://bitbucket.org/Chaoseiro/x-unitmx
 * 
 * Copyright 2013 Guilherme "Chaoseiro" Maia
 * Released under the MIT license
 * Check LICENSE.MIT for more details.
 */

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.Globalization;
using System.IO;
using System.IO.Compression;
//using Ionic.Zlib;

namespace X_UniTMX
{
	/// <summary>
	/// A map layer containing tiles.
	/// </summary>
	public class TileLayer : Layer
	{
		// The data coming in combines flags for whether the tile is flipped as well as
		// the actual index. These flags are used to first figure out if it's flipped and
		// then to remove those flags and get us the actual ID.
		private const uint FlippedHorizontallyFlag = 0x80000000;
		private const uint FlippedVerticallyFlag = 0x40000000;

		/// <summary>
		/// Gets the layout of tiles on the layer.
		/// </summary>
		public TileGrid Tiles { get; private set; }

		/// <summary>
		/// Gets the number of vertices used to render this layer
		/// </summary>
		public int VertexCount { get; private set; }

		/// <summary>
		/// Gets this Layer's Mesh
		/// </summary>
		public Mesh LayerMesh { get; private set; }

		/// <summary>
		/// Gets this Layer's Mesh Filter
		/// </summary>
		public MeshFilter LayerMeshFilter { get; private set; }

		/// <summary>
		/// Gets this Layer's Mesh Renderer
		/// </summary>
		public MeshRenderer LayerMeshRenderer { get; private set; }

		/// <summary>
		/// Layer's Game Object
		/// </summary>
		public GameObject LayerGameObject { get; private set; }


		/*internal TileLayer(string name, int width, int height, float layerDepth, bool visible, float opacity, PropertyCollection properties, Map map, uint[] data, bool makeUnique)
			: base(name, width, height, layerDepth, visible, opacity, properties)
		{
			Initialize(map, data, makeUnique);
		}*/

		public uint[] Data;

		public TileLayer(XmlNode node, Map map, float layerDepth, bool makeUnique, List<Material> materials)
            : base(node)
        {
            XmlNode dataNode = node["data"];
            Data = new uint[Width * Height];
			LayerDepth = layerDepth;
			Debug.Log("Layer Depth: " + LayerDepth);
            // figure out what encoding is being used, if any, and process
            // the data appropriately
            if (dataNode.Attributes["encoding"] != null)
            {
                string encoding = dataNode.Attributes["encoding"].Value;

                if (encoding == "base64")
                {
                    ReadAsBase64(node, dataNode);
                }
                else if (encoding == "csv")
                {
                    ReadAsCsv(node, dataNode);
                }
                else
                {
                    throw new Exception("Unknown encoding: " + encoding);
                }
            }
            else
            {
                // XML format simply lays out a lot of <tile gid="X" /> nodes inside of data.

                int i = 0;
                foreach (XmlNode tileNode in dataNode.SelectNodes("tile"))
                {
                    Data[i] = uint.Parse(tileNode.Attributes["gid"].Value, CultureInfo.InvariantCulture);
                    i++;
                }

                if (i != Data.Length)
                    throw new Exception("Not enough tile nodes to fill data");
            }

			Initialize(map, Data, makeUnique, materials);
        }

        private void ReadAsCsv(XmlNode node, XmlNode dataNode)
        {
            // split the text up into lines
            string[] lines = node.InnerText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // iterate each line
            for (int i = 0; i < lines.Length; i++)
            {
                // split the line into individual pieces
                string[] indices = lines[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                // iterate the indices and store in our data
                for (int j = 0; j < indices.Length; j++)
                {
                    Data[i * Width + j] = uint.Parse(indices[j], CultureInfo.InvariantCulture);
                }
            }
        }

        private void ReadAsBase64(XmlNode node, XmlNode dataNode)
        {
            // get a stream to the decoded Base64 text
            Stream data = new MemoryStream(Convert.FromBase64String(node.InnerText), false);

            // figure out what, if any, compression we're using. the compression determines
            // if we need to wrap our data stream in a decompression stream
            if (dataNode.Attributes["compression"] != null)
            {
                string compression = dataNode.Attributes["compression"].Value;

                if (compression == "gzip")
                {
					data = new Ionic.Zlib.GZipStream(data, Ionic.Zlib.CompressionMode.Decompress, false);
                }
                else if (compression == "zlib")
                {
                    data = new Ionic.Zlib.ZlibStream(data, Ionic.Zlib.CompressionMode.Decompress, false);
                }
                else
                {
                    throw new InvalidOperationException("Unknown compression: " + compression);
                }
            }

            // simply read in all the integers
            using (data)
            {
                using (BinaryReader reader = new BinaryReader(data))
                {
                    for (int i = 0; i < Data.Length; i++)
                    {
                        Data[i] = reader.ReadUInt32();
                    }
                }
            }
        }

		private void Initialize(Map map, uint[] data, bool makeUnique, List<Material> materials)
		{
			Tiles = new TileGrid(Width, Height);
			
			// data is left-to-right, top-to-bottom
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					uint index = data[y * Width + x];

					// compute the SpriteEffects to apply to this tile
					SpriteEffects spriteEffects = SpriteEffects.None;
					if ((index & FlippedHorizontallyFlag) != 0)
						spriteEffects |= SpriteEffects.FlipHorizontally;
					if ((index & FlippedVerticallyFlag) != 0)
						spriteEffects |= SpriteEffects.FlipVertically;

					// strip out the flip flags to get the real ID
					int id = (int)(index & ~(FlippedVerticallyFlag | FlippedHorizontallyFlag));
					//Debug.Log("Tile ID: " + id + " (index : " + index + ")");
					// get the tile
					Tile t = null;
					map.Tiles.TryGetValue(id, out t);
					
					// if the tile is non-null...
					if (t != null)
					{
						// if we want unique instances, clone it
						if (makeUnique)
						{
							t = t.Clone();
							t.SpriteEffects = spriteEffects;
						}

						// otherwise we may need to clone if the tile doesn't have the correct effects
						// in this world a flipped tile is different than a non-flipped one; just because
						// they have the same source rect doesn't mean they're equal.
						else if (t.SpriteEffects != spriteEffects)
						{
							t = t.Clone();
							t.SpriteEffects = spriteEffects;
						}
					}

					// put that tile in our grid
					Tiles[x, y] = t;
				}
			}

			GenerateLayerMesh(map, materials);
		}

		/*private GameObject GenerateSingleMesh(List<Vector3> vertices, Tile t)
		{
			GameObject obj = new GameObject();
			MeshFilter mfilter = obj.AddComponent<MeshFilter>();
			MeshRenderer mrender = obj.AddComponent<MeshRenderer>();

			Vector3 TopLeft = new Vector3(Mathf.Infinity, Mathf.NegativeInfinity, Mathf.Infinity);
			Vector3 TopRight = new Vector3(Mathf.NegativeInfinity, Mathf.NegativeInfinity, Mathf.Infinity);
			Vector3 BottomLeft = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
			Vector3 BottomRight = new Vector3(Mathf.NegativeInfinity, Mathf.Infinity, Mathf.Infinity);

			foreach (Vector3 vertex in vertices)
			{
				if (vertex.x < TopLeft.x && -vertex.y > TopLeft.y)
					TopLeft = vertex;
				if (vertex.x > TopRight.x && -vertex.y > TopRight.y)
					TopRight = vertex;
				if (vertex.x < BottomLeft.x && -vertex.y < BottomLeft.y)
					BottomLeft = vertex;
				if (vertex.x > BottomRight.x && -vertex.y < BottomRight.y)
					BottomRight = vertex;
			}

			Debug.Log("TopLeft: " + TopLeft + "\nTopRight: " + TopRight + "\nBottomLeft: " + BottomLeft + "\nBottomRight: " + BottomRight);

			Mesh mesh = new Mesh();
			mesh.vertices = vertices.ToArray();

			float uTileWidth = (float)t.TileSet.TileWidth / (float)t.TileSet.Texture.width;
			float vTileHeight = (float)t.TileSet.TileHeight / (float)t.TileSet.Texture.height;
			float u = t.Source.x / (float)t.TileSet.Texture.width;
			float v = 1.0f - t.Source.y / (float)t.TileSet.Texture.height;
			mesh.uv = new Vector2[] {
				new Vector2(u + uTileWidth, v),
				new Vector2(u + uTileWidth, v - vTileHeight),
				new Vector2(u, v),
				new Vector2(u, v - vTileHeight)
			};
			mesh.triangles = new int[] {
				0, 1, 2,
				2, 1,  3,
			};
			mesh.RecalculateNormals();

			mfilter.mesh = mesh;
			Material mat = new Material(Shader.Find("Unlit/Transparent"));
			mat.mainTexture = t.TileSet.Texture;
			mrender.material = mat;

			return obj;
		}
		*/
		// Renders the tile vertices.
		// Basically, it reads the tiles and creates its 4 vertexes (forming a rectangle or square according to settings) 
		private void GenerateLayerMesh(Map map, List<Material> materials)
		{
			LayerGameObject = new GameObject(Name);//(GameObject)GameObject.Instantiate(Resources.Load("Tilemap"));//
			LayerMesh = new Mesh();

			LayerMeshFilter = (MeshFilter)LayerGameObject.AddComponent("MeshFilter");
			LayerMeshRenderer = (MeshRenderer)LayerGameObject.AddComponent("MeshRenderer");

			List<Vector3> vertices = new List<Vector3>();
			List<Vector2> uv = new List<Vector2>();
			List<int> triangles = new List<int>();
			List<Texture2D> textures = new List<Texture2D>();
			Tile t;
			/*List<GameObject> meshes = new List<GameObject>();
			Tile lastTile = null;
			int lastGID = -1;
			//int j = 0;
			for (int i = 0; i < Width; i++)
			{
				for (int j = 0; j < Height; j++)
				{
					t = Tiles[i, j];
					if (t != null)
					{
						Debug.Log(t.GID);
						if (lastGID == t.GID)
						{
							vertices.AddRange(new Vector3[] {
								new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * -j, 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * (-j - 1), 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * -j, 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * (-j - 1), 0)
							});
							lastTile = t;
						}
						else
						{
							lastGID = t.GID;
							if(vertices.Count > 0)
								meshes.Add(GenerateSingleMesh(vertices, lastTile));
							vertices.Clear();
							vertices.AddRange(new Vector3[] {
								new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * -j, 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * (-j - 1), 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * -j, 0),
								new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * (-j - 1), 0)
							});
							lastTile = t;
						}
					}

					j++;
				}
			}
			
			CombineInstance[] combine = new CombineInstance[meshes.Count];
			List<Material> materials = new List<Material>();
			for (int i = 0; i < meshes.Count; i++)
			{
				combine[i].mesh = meshes[i].GetComponent<MeshFilter>().mesh;
				combine[i].transform = meshes[i].GetComponent<MeshFilter>().transform.localToWorldMatrix;
				materials.Add(meshes[i].GetComponent<MeshRenderer>().material);
			}

			LayerMesh.CombineMeshes(combine);
			LayerMeshFilter.mesh = LayerMesh;
			LayerMeshRenderer.materials = materials.ToArray();
			*/

			for (int i = 0; i < Width; i++)
			{
				for (int j = 0; j < Height; j++)
				{
					t = Tiles[i, j];
					if(t != null) {

						// Add Tile's vertices to layer's mesh
						vertices.AddRange(new Vector3[] {
							new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * -j, 0),
							new Vector3 (t.Source.width / t.TileSet.TileWidth * (i + 1), t.Source.height / t.TileSet.TileHeight * (-j - 1), 0),
							new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * -j, 0),
							new Vector3 (t.Source.width / t.TileSet.TileWidth * i, t.Source.height / t.TileSet.TileHeight * (-j - 1), 0)
						});
						// Generate this Tile's Triangles on Layer's mesh
						triangles.AddRange(new int[] {
							VertexCount, VertexCount + 1, VertexCount + 2,
							VertexCount + 2, VertexCount + 1, VertexCount + 3,
						});

						VertexCount += 4;

						// Add Tile's texture source to textures list, to create the materials
						if (textures.Find(text => text.name == t.TileSet.Texture.name) == null)
							textures.Add(t.TileSet.Texture);

						// Save UV mapping of this Tile on it's texture
						//Debug.Log(t.Source);
						float uTileWidth = (float)t.TileSet.TileWidth / (float)t.TileSet.Texture.width;
						float vTileHeight = (float)t.TileSet.TileHeight / (float)t.TileSet.Texture.height;
						//float uBorderWidth = (float)t.TileSet.Spacing / t.TileSet.Texture.width;
						//float vBorderHeight = (float)t.TileSet.Spacing / t.TileSet.Texture.height;
						float u = t.Source.x / (float)t.TileSet.Texture.width;//(uTileWidth + uBorderWidth) * t.Source.x + uBorderWidth / 2;
						float v = 1.0f - t.Source.y / (float)t.TileSet.Texture.height;//1.0f - (vTileHeight - vBorderHeight) * t.Source.y - vBorderHeight / 2;
						uv.AddRange(new Vector2[] {
							new Vector2(u + uTileWidth, v),
							new Vector2(u + uTileWidth, v - vTileHeight),
							new Vector2(u, v),
							new Vector2(u, v - vTileHeight)
						});
					}
				}
			}
			//t = null;
			LayerMesh.vertices = vertices.ToArray();
			LayerMesh.uv = uv.ToArray();
			LayerMesh.triangles = triangles.ToArray();
			LayerMesh.RecalculateNormals();
			
			LayerMeshFilter.mesh = LayerMesh;
			List<Material> layerMaterials = new List<Material>();
			// Generate Materials
			for (int i = 0; i < textures.Count; i++)
			{
				//Material layerMat = new Material(Shader.Find("Unlit/Transparent"));
				//layerMat.mainTexture = textures[i];
				//materials.Add(layerMat);
				for (int j = 0; j < materials.Count; j++)
				{
					if (materials[j].mainTexture.name == textures[i].name)
						layerMaterials.Add(materials[j]);
				}
			}
			LayerMeshRenderer.sharedMaterials = layerMaterials.ToArray();
			
			
			LayerGameObject.transform.parent = map.Parent.transform;
			LayerGameObject.transform.position = new Vector3(0, 0, this.LayerDepth);
			LayerGameObject.isStatic = true;
		}
	}
}