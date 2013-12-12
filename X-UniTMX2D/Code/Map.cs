/*!
 * X-UniTMX: A tiled map editor file importer for Unity3d
 * https://bitbucket.org/Chaoseiro/x-unitmx
 * 
 * Copyright 2013 Guilherme "Chaoseiro" Maia
 * Released under the MIT license
 * Check LICENSE.MIT for more details.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
//using UnityEditor;

namespace X_UniTMX
{
	/// <summary>
	/// Defines the possible orientations for a Map.
	/// </summary>
	public enum Orientation : byte
	{
		/// <summary>
		/// The tiles of the map are orthogonal.
		/// </summary>
		Orthogonal,

		/// <summary>
		/// The tiles of the map are isometric.
		/// </summary>
		Isometric,
	}

	/// <summary>
	/// A delegate used for searching for map objects.
	/// </summary>
	/// <param name="layer">The current layer.</param>
	/// <param name="mapObj">The current object.</param>
	/// <returns>True if this is the map object desired, false otherwise.</returns>
	public delegate bool MapObjectFinder(MapObjectLayer layer, MapObject mapObj);

	/// <summary>
	/// A full map from Tiled.
	/// </summary>
	public class Map
	{
		/// <summary>
		/// The difference in layer depth between layers.
		/// </summary>
		/// <remarks>
		/// The algorithm for creating the LayerDepth for each layer when enumerating from
		/// back to front is:
		/// float layerDepth = 1f - (LayerDepthSpacing * i);</remarks>
		public const float LayerDepthSpacing = 1.0f;

		private readonly Dictionary<string, Layer> namedLayers = new Dictionary<string, Layer>();

		/// <summary>
		/// Gets the version of Tiled used to create the Map.
		/// </summary>
		public string Version { get; private set; }

		/// <summary>
		/// Gets the orientation of the map.
		/// </summary>
		public Orientation Orientation { get; private set; }

		/// <summary>
		/// Gets the width (in tiles) of the map.
		/// </summary>
		public int Width { get; private set; }

		/// <summary>
		/// Gets the height (in tiles) of the map.
		/// </summary>
		public int Height { get; private set; }

		/// <summary>
		/// Gets the width of a tile in the map.
		/// </summary>
		public int TileWidth { get; private set; }

		/// <summary>
		/// Gets the height of a tile in the map.
		/// </summary>
		public int TileHeight { get; private set; }

		/// <summary>
		/// Gets a list of the map's properties.
		/// </summary>
		public PropertyCollection Properties { get; private set; }

		/// <summary>
		/// Gets a collection of all of the tiles in the map.
		/// </summary>
		public Dictionary<int, Tile> Tiles { get; private set; }

		/// <summary>
		/// Gets a collection of all of the layers in the map.
		/// </summary>
		public List<Layer> Layers { get; private set; }

		/// <summary>
		/// Gets a collection of all of the tile sets in the map.
		/// </summary>
		public List<TileSet> TileSets { get; private set; }

		/// <summary>
		/// Gets this map's Game Object Parent
		/// </summary>
		public GameObject Parent { get; private set; }

		public Map(TextAsset mapText, bool makeUnique, string mapPath, GameObject parent)
		{
			/*string fullPath = Application.dataPath + "/Resources/Maps";//Path.GetDirectoryName(AssetDatabase.GetAssetPath(mapText));
			Debug.Log(fullPath);
			string mapPath = "";
			string[] splittedFullPath = fullPath.Split(new string[] { "Assets/Resources/" }, StringSplitOptions.None);
			if (splittedFullPath.Length > 1)
				mapPath = splittedFullPath[1] + "/";*/
			mapPath = mapPath + "/";

			XmlDocument document = new XmlDocument();
			document.LoadXml(mapText.text);

			Parent = parent;

			//Initialize(document, makeUnique, fullPath, mapPath);
			Initialize(document, makeUnique, "", mapPath);//"Maps/");
		}

		public Map(XmlDocument document, bool makeUnique, string fullPath, string mapPath, GameObject parent)//, MeshRenderer MeshRendererPrefab)
		{
			Parent = parent;

			Initialize(document, makeUnique, fullPath, mapPath);
		}

		void Initialize(XmlDocument document, bool makeUnique, string fullPath, string mapPath)
		{
			XmlNode mapNode = document["map"];
			Version = mapNode.Attributes["version"].Value;
			Orientation = (Orientation)Enum.Parse(typeof(Orientation), mapNode.Attributes["orientation"].Value, true);
			Width = int.Parse(mapNode.Attributes["width"].Value, CultureInfo.InvariantCulture);
			Height = int.Parse(mapNode.Attributes["height"].Value, CultureInfo.InvariantCulture);
			TileWidth = int.Parse(mapNode.Attributes["tilewidth"].Value, CultureInfo.InvariantCulture);
			TileHeight = int.Parse(mapNode.Attributes["tileheight"].Value, CultureInfo.InvariantCulture);

			XmlNode propertiesNode = document.SelectSingleNode("map/properties");
			if (propertiesNode != null)
			{
				Properties = new PropertyCollection(propertiesNode);//Property.ReadProperties(propertiesNode);
			}

			TileSets = new List<TileSet>();
			Tiles = new Dictionary<int, Tile>();
			foreach (XmlNode tileSet in document.SelectNodes("map/tileset"))
			{
				if (tileSet.Attributes["source"] != null)
				{
					//TileSets.Add(new ExternalTileSetContent(tileSet, context));
					XmlDocument externalTileSet = new XmlDocument();

					TextAsset externalTileSetTextAsset = (TextAsset)Resources.Load(mapPath + Path.GetFileNameWithoutExtension(tileSet.Attributes["source"].Value));

					//externalTileSet.Load(fullPath + "/" + tileSet.Attributes["source"].Value);
					externalTileSet.LoadXml(externalTileSetTextAsset.text);
					XmlNode externalTileSetNode = externalTileSet["tileset"];
					//Debug.Log(externalTileSet.Value);
					TileSet t = new TileSet(externalTileSetNode, mapPath);
					TileSets.Add(t);
					foreach (KeyValuePair<int, Tile> item in t.Tiles)
					{
						this.Tiles.Add(item.Key, item.Value);
					}
					//this.Tiles.AddRange(t.Tiles);
				}
				else
				{
					TileSet t = new TileSet(tileSet, mapPath);
					TileSets.Add(t);
					foreach (KeyValuePair<int, Tile> item in t.Tiles)
					{
						this.Tiles.Add(item.Key, item.Value);
					}
				}
			}
			// Generate Materials for Map batching
			List<Material> materials = new List<Material>();
			// Generate Materials
			int i = 0;
			for (i = 0; i < TileSets.Count; i++)
			{
				Material layerMat = new Material(Shader.Find("Unlit/Transparent"));
				layerMat.mainTexture = TileSets[i].Texture;
				materials.Add(layerMat);
			}

			Layers = new List<Layer>();
			i = 0;
			foreach (XmlNode layerNode in document.SelectNodes("map/layer|map/objectgroup"))
			{
				Layer layerContent;

				float layerDepth = 1f - (LayerDepthSpacing * i);

				if (layerNode.Name == "layer")
				{
					layerContent = new TileLayer(layerNode, this, layerDepth, makeUnique, materials);
					//((TileLayer)layerContent).GenerateLayerMesh(MeshRendererPrefab);
				}
				else if (layerNode.Name == "objectgroup")
				{
					layerContent = new MapObjectLayer(layerNode, TileWidth, TileHeight);
				}
				else
				{
					throw new Exception("Unknown layer name: " + layerNode.Name);
				}

				// Layer names need to be unique for our lookup system, but Tiled
				// doesn't require unique names.
				string layerName = layerContent.Name;
				int duplicateCount = 2;

				// if a layer already has the same name...
				if (Layers.Find(l => l.Name == layerName) != null)
				{
					// figure out a layer name that does work
					do
					{
						layerName = string.Format("{0}{1}", layerContent.Name, duplicateCount);
						duplicateCount++;
					} while (Layers.Find(l => l.Name == layerName) != null);

					// log a warning for the user to see
					Debug.Log("Renaming layer \"" + layerContent.Name + "\" to \"" + layerName + "\" to make a unique name.");

					// save that name
					layerContent.Name = layerName;
				}
				layerContent.LayerDepth = layerDepth;
				Layers.Add(layerContent);
				namedLayers.Add(layerName, layerContent);
				i++;
			}
		}

		/// <summary>
		/// Converts a point in world space into tile indices that can be used to index into a TileLayer.
		/// </summary>
		/// <param name="worldPoint">The point in world space to convert into tile indices.</param>
		/// <returns>A Point containing the X/Y indices of the tile that contains the point.</returns>
		public Vector2 WorldPointToTileIndex(Vector2 worldPoint)
		{
			if (worldPoint.x < 0 || worldPoint.y < 0 || worldPoint.x > Width * TileWidth || worldPoint.y > Height * TileHeight)
			{
				throw new ArgumentOutOfRangeException("worldPoint");
			}

			Vector2 p = new Vector2();

			// simple conversion to tile indices
			p.x = (int)Math.Floor(worldPoint.x / TileWidth);
			p.y = (int)Math.Floor(worldPoint.y / TileHeight);

			// check the upper limit edges. if we are on the edge, we need to decrement the index to keep in bounds.
			if (worldPoint.x == Width * TileWidth)
			{
				p.x--;
			}
			if (worldPoint.y == Height * TileHeight)
			{
				p.y--;
			}

			return p;
		}

		/// <summary>
		/// Returns the set of all objects in the map.
		/// </summary>
		/// <returns>A new set of all objects in the map.</returns>
		public IEnumerable<MapObject> GetAllObjects()
		{
			foreach (var layer in Layers)
			{
				MapObjectLayer objLayer = layer as MapObjectLayer;
				if (objLayer == null)
					continue;

				foreach (var obj in objLayer.Objects)
				{
					yield return obj;
				}
			}
		}

		/// <summary>
		/// Finds an object in the map using a delegate.
		/// </summary>
		/// <remarks>
		/// This method is used when an object is desired, but there is no specific
		/// layer to find the object on. The delegate allows the caller to create 
		/// any logic they want for finding the object. A simple example for finding
		/// the first object named "goal" in any layer would be this:
		/// 
		/// var goal = map.FindObject((layer, obj) => return obj.Name.Equals("goal"));
		/// 
		/// You could also use the layer name or any other logic to find an object.
		/// The first object for which the delegate returns true is the object returned
		/// to the caller. If the delegate never returns true, the method returns null.
		/// </remarks>
		/// <param name="finder">The delegate used to search for the object.</param>
		/// <returns>The MapObject if the delegate returned true, null otherwise.</returns>
		public MapObject FindObject(MapObjectFinder finder)
		{
			foreach (var layer in Layers)
			{
				MapObjectLayer objLayer = layer as MapObjectLayer;
				if (objLayer == null)
					continue;

				foreach (var obj in objLayer.Objects)
				{
					if (finder(objLayer, obj))
						return obj;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds a collection of objects in the map using a delegate.
		/// </summary>
		/// <remarks>
		/// This method performs basically the same process as FindObject, but instead
		/// of returning the first object for which the delegate returns true, it returns
		/// a collection of all objects for which the delegate returns true.
		/// </remarks>
		/// <param name="finder">The delegate used to search for the object.</param>
		/// <returns>A collection of all MapObjects for which the delegate returned true.</returns>
		public IEnumerable<MapObject> FindObjects(MapObjectFinder finder)
		{
			foreach (var layer in Layers)
			{
				MapObjectLayer objLayer = layer as MapObjectLayer;
				if (objLayer == null)
					continue;

				foreach (var obj in objLayer.Objects)
				{
					if (finder(objLayer, obj))
						yield return obj;
				}
			}
		}

		/// <summary>
		/// Gets a layer by name.
		/// </summary>
		/// <param name="name">The name of the layer to retrieve.</param>
		/// <returns>The layer with the given name.</returns>
		public Layer GetLayer(string name)
		{
			if (namedLayers.ContainsKey(name))
				return namedLayers[name];
			return null;
		}

		/// <summary>
		/// Gets a string property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property, String.Empty if property not found</returns>
		public string GetPropertyAsString(string property)
		{
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return str;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			return str;
		}
		/// <summary>
		/// Gets a boolean property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public bool GetPropertyAsBoolean(string property)
		{
			bool b = false;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			Boolean.TryParse(str, out b);

			return b;
		}
		/// <summary>
		/// Gets an integer property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public int GetPropertyAsInt(string property)
		{
			int b = 0;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			Int32.TryParse(str, out b);

			return b;
		}
		/// <summary>
		/// Gets a float property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public float GetPropertyAsFloat(string property)
		{
			float b = 0;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			float.TryParse(str, out b);

			return b;
		}

		/// <summary>
		/// Generate a Box collider mesh
		/// </summary>
		/// <param name="obj">Object which properties will be used to generate this collider.</param>
		/// <param name="zDepth">Z Depth of the collider.</param>
		/// <returns>Generated Game Object containing the Collider.</returns>
		public GameObject GenerateBoxCollider(MapObject obj, float zDepth = 0, float colliderWidth = 1.0f)
		{
			GameObject boxCollider = new GameObject(obj.Name);
			BoxCollider2D bx = boxCollider.AddComponent<BoxCollider2D>();
			//boxCollider.transform.position.Set(obj.Bounds.x, obj.Bounds.y, zDepth);
			boxCollider.transform.parent = this.Parent.transform;

			bx.center = new Vector3(obj.Bounds.center.x, -obj.Bounds.center.y, zDepth);
			bx.size = new Vector3(obj.Bounds.width, obj.Bounds.height, colliderWidth);

			boxCollider.isStatic = true;

			return boxCollider;
		}

		/// <summary>
		/// Generate an Ellipse Collider mesh. To mimic Tiled's Ellipse Object properties, a Capsule collider is created.
		/// </summary>
		/// <param name="obj">Object which properties will be used to generate this collider.</param>
		/// <param name="zDepth">Z Depth of the collider.</param>
		/// <returns>Generated Game Object containing the Collider.</returns>
		public GameObject GenerateEllipseCollider(MapObject obj, float zDepth = 0, float colliderWidth = 1.0f)
		{
			GameObject capsuleCollider = new GameObject(obj.Name);
			CapsuleCollider cc = capsuleCollider.AddComponent<CapsuleCollider>();
			capsuleCollider.transform.parent = this.Parent.transform;

			cc.center = new Vector3(obj.Bounds.center.x / TileWidth, -obj.Bounds.center.y, zDepth);
			
			cc.direction = 2;
			cc.radius = obj.Bounds.width / TileWidth / 2;
			cc.height = obj.Bounds.height * colliderWidth;

			capsuleCollider.isStatic = true;

			return capsuleCollider;
		}

		/// <summary>
		/// Generate a Polygon collider mesh
		/// </summary>
		/// <param name="obj">Object which properties will be used to generate this collider.</param>
		/// <param name="zDepth">Z Depth of the collider.</param>
		/// <param name="colliderWidth">Width of the collider.</param>
		/// <param name="innerCollision">If true, calculate normals facing the center of the collider (inside collisions), else, outside collisions.</param>
		/// <returns>Generated Game Object containing the Collider.</returns>
		public GameObject GeneratePolygonCollider(MapObject obj, float zDepth = 0, float colliderWidth = 1.0f, bool innerCollision = false)
		{
			GameObject polygonCollider = new GameObject(obj.Name);
			polygonCollider.transform.parent = this.Parent.transform;

			Mesh colliderMesh = new Mesh();
			colliderMesh.name = "Collider_" + obj.Name;
			MeshCollider mc = polygonCollider.AddComponent<MeshCollider>();

			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();

			Vector3 firstPoint = (Vector3)obj.Points[0];
			Vector3 secondPoint, firstFront, firstBack, secondFront, secondBack;
			for (int i = 1; i < obj.Points.Count; i++)
			{
				secondPoint = (Vector3)obj.Points[i];
				firstFront = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth - colliderWidth);
				firstBack = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth + colliderWidth);
				secondFront = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth - colliderWidth);
				secondBack = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth + colliderWidth);
				if (innerCollision)
				{
					vertices.Add(firstBack); // 3
					vertices.Add(firstFront); // 2
					vertices.Add(secondBack); // 1
					vertices.Add(secondFront); // 0
				}
				else
				{
					vertices.Add(firstFront); // 3
					vertices.Add(firstBack); // 2
					vertices.Add(secondFront); // 1
					vertices.Add(secondBack); // 2
				}
				triangles.Add((i - 1) * 4 + 3);
				triangles.Add((i - 1) * 4 + 2);
				triangles.Add((i - 1) * 4 + 0);

				triangles.Add((i - 1) * 4 + 0);
				triangles.Add((i - 1) * 4 + 1);
				triangles.Add((i - 1) * 4 + 3);

				firstPoint = secondPoint;
			}
			// Connect last point with first point
			secondPoint = (Vector3)obj.Points[0];
			firstFront = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth - colliderWidth);
			firstBack = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth + colliderWidth);
			secondFront = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth - colliderWidth);
			secondBack = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth + colliderWidth);
			if (innerCollision)
			{
				vertices.Add(firstBack); // 3
				vertices.Add(firstFront); // 2
				vertices.Add(secondBack); // 1
				vertices.Add(secondFront); // 0
			}
			else
			{
				vertices.Add(firstFront); // 3
				vertices.Add(firstBack); // 2
				vertices.Add(secondFront); // 1
				vertices.Add(secondBack); // 2
			}

			triangles.Add((obj.Points.Count - 1) * 4 + 3);
			triangles.Add((obj.Points.Count - 1) * 4 + 2);
			triangles.Add((obj.Points.Count - 1) * 4 + 0);

			triangles.Add((obj.Points.Count - 1) * 4 + 0);
			triangles.Add((obj.Points.Count - 1) * 4 + 1);
			triangles.Add((obj.Points.Count - 1) * 4 + 3);

			colliderMesh.vertices = vertices.ToArray();
			colliderMesh.triangles = triangles.ToArray();
			colliderMesh.RecalculateNormals();

			mc.sharedMesh = colliderMesh;

			polygonCollider.isStatic = true;

			return polygonCollider;
		}

		/// <summary>
		/// Generate a Polyline collider mesh
		/// </summary>
		/// <param name="obj">Object which properties will be used to generate this collider.</param>
		/// <param name="zDepth">Z Depth of the collider.</param>
		/// <param name="colliderWidth">Width of the collider.</param>
		/// <param name="innerCollision">If true, calculate normals facing the center of the collider (inside collisions), else, outside collisions.</param>
		/// <returns>Generated Game Object containing the Collider.</returns>
		public GameObject GeneratePolylineCollider(MapObject obj, float zDepth = 0, float colliderWidth = 1.0f, bool innerCollision = false)
		{
			GameObject polylineCollider = new GameObject(obj.Name);
			polylineCollider.transform.parent = this.Parent.transform;

			Mesh colliderMesh = new Mesh();
			MeshCollider mc = polylineCollider.AddComponent<MeshCollider>();

			List<Vector3> vertices = new List<Vector3>();
			List<int> triangles = new List<int>();

			Vector3 firstPoint = (Vector3)obj.Points[0];
			for(int i = 1; i < obj.Points.Count; i++)
			{
				Vector3 secondPoint = (Vector3)obj.Points[i];
				Vector3 firstFront = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth - colliderWidth);
				Vector3 firstBack = new Vector3(obj.Bounds.center.x + firstPoint.x, -obj.Bounds.center.y - firstPoint.y, zDepth + colliderWidth);
				Vector3 secondFront = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth - colliderWidth);
				Vector3 secondBack = new Vector3(obj.Bounds.center.x + secondPoint.x, -obj.Bounds.center.y - secondPoint.y, zDepth + colliderWidth);
				if (innerCollision)
				{
					vertices.Add(firstBack); // 3
					vertices.Add(firstFront); // 2
					vertices.Add(secondBack); // 1
					vertices.Add(secondFront); // 0
				}
				else
				{
					vertices.Add(firstFront); // 3
					vertices.Add(firstBack); // 2
					vertices.Add(secondFront); // 1
					vertices.Add(secondBack); // 2
				}

				triangles.Add((i - 1) * 4 + 3);
				triangles.Add((i - 1) * 4 + 2);
				triangles.Add((i - 1) * 4 + 0);

				triangles.Add((i - 1) * 4 + 0);
				triangles.Add((i - 1) * 4 + 1);
				triangles.Add((i - 1) * 4 + 3);

				firstPoint = secondPoint;
			}
			colliderMesh.vertices = vertices.ToArray();
			colliderMesh.triangles = triangles.ToArray();
			colliderMesh.RecalculateNormals();

			mc.sharedMesh = colliderMesh;

			polylineCollider.isStatic = true;

			return polylineCollider;
		}

		public override string ToString()
		{
			string str = "Map Size (" + Width + ", " + Height + ")";
			str += "\nTile Size (" + TileWidth + ", " + TileHeight + ")";
			str += "\nOrientation: " + Orientation.ToString();
			str += "\nTiled Version: " + Version;
			return str;
		}
	}
}
